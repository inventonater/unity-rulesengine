using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Inventonater.Rules
{
    public class DevPanel : MonoBehaviour
    {
        private bool _showPanel = true;
        private string _jsonInput = "";
        private string _eventToEmit = "test.event";
        private Vector2 _scrollPosition;
        private List<string> _recentEvents = new List<string>();
        private const int MAX_RECENT_EVENTS = 10;
        
        private RuleRepository _repository;
        private RuleEngine _engine;
        private EntityStore _store;
        private Services _services;
        private TimerService _timerService;
        
        private string _rulesDirectory = "";

        private void Start()
        {
            // Find or create components
            _repository = FindObjectOfType<RuleRepository>();
            if (_repository == null)
            {
                _repository = gameObject.AddComponent<RuleRepository>();
            }
            
            _engine = FindObjectOfType<RuleEngine>();
            if (_engine == null)
            {
                _engine = gameObject.AddComponent<RuleEngine>();
            }
            
            _store = FindObjectOfType<EntityStore>();
            if (_store == null)
            {
                _store = gameObject.AddComponent<EntityStore>();
            }
            
            _services = FindObjectOfType<Services>();
            if (_services == null)
            {
                _services = gameObject.AddComponent<Services>();
            }
            
            _timerService = FindObjectOfType<TimerService>();
            if (_timerService == null)
            {
                _timerService = gameObject.AddComponent<TimerService>();
            }
            
            // Initialize the engine
            _engine.Initialize(_repository, _store, _services, _timerService);
            
            // Try to load sample rules
            LoadSampleRules();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _showPanel = !_showPanel;
            }
        }

        private void OnGUI()
        {
            if (!_showPanel) return;
            
            // Create a dark background
            GUI.Box(new Rect(10, 10, 400, 600), "Rules Engine Dev Panel (F2 to toggle)");
            
            GUILayout.BeginArea(new Rect(20, 40, 380, 560));
            
            // Rules directory
            GUILayout.Label("Rules Directory:", GUI.skin.label);
            _rulesDirectory = GUILayout.TextField(_rulesDirectory);
            
            if (GUILayout.Button("Load Rules from Directory"))
            {
                LoadRulesFromDirectory(_rulesDirectory);
            }
            
            if (GUILayout.Button("Load Sample Rules"))
            {
                LoadSampleRules();
            }
            
            GUILayout.Space(10);
            
            // JSON Input
            GUILayout.Label("Paste JSON Rule:", GUI.skin.label);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            _jsonInput = GUILayout.TextArea(_jsonInput, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            
            if (GUILayout.Button("Add Rule from JSON"))
            {
                AddRuleFromJson(_jsonInput);
            }
            
            if (GUILayout.Button("Replace All Rules with JSON"))
            {
                ReplaceAllRulesFromJson(_jsonInput);
            }
            
            GUILayout.Space(10);
            
            // Event Emitter
            GUILayout.Label("Emit Event:", GUI.skin.label);
            _eventToEmit = GUILayout.TextField(_eventToEmit);
            
            if (GUILayout.Button("Emit Event"))
            {
                EventBus.Publish(_eventToEmit);
                AddRecentEvent($"Emitted: {_eventToEmit}");
            }
            
            GUILayout.Space(10);
            
            // State Display
            GUILayout.Label("Current State:", GUI.skin.label);
            GUILayout.Label($"UI Mode: {_store?.GetState("ui.mode") ?? "unknown"}", GUI.skin.label);
            GUILayout.Label($"Mouse Speed: {_store?.GetNumeric("sensor.mouse_speed") ?? 0:F1}", GUI.skin.label);
            GUILayout.Label($"Mouse Button: {(_store?.GetNumeric("sensor.mouse_button_left") ?? 0) > 0 ? "Down" : "Up"}", GUI.skin.label);
            
            GUILayout.Space(10);
            
            // Recent Events
            GUILayout.Label("Recent Events:", GUI.skin.label);
            foreach (var evt in _recentEvents)
            {
                GUILayout.Label(evt, GUI.skin.label);
            }
            
            GUILayout.Space(10);
            
            // Quick Actions
            if (GUILayout.Button("Clear All Rules"))
            {
                _repository.ReplaceAll(new List<RuleDto>());
                AddRecentEvent("Cleared all rules");
            }
            
            if (GUILayout.Button("Clear Events"))
            {
                _recentEvents.Clear();
            }
            
            GUILayout.EndArea();
        }

        private void LoadSampleRules()
        {
            // Try to find sample rules in the package
            string[] searchPaths = new[]
            {
                "Packages/com.inventonater.rules/Samples~/Demo/Rules",
                Application.dataPath + "/../Packages/com.inventonater.rules/Samples~/Demo/Rules",
                Path.Combine(Application.dataPath, "../Packages/com.inventonater.rules/Samples~/Demo/Rules")
            };
            
            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    LoadRulesFromDirectory(path);
                    return;
                }
            }
            
            // If no sample rules found, create some defaults
            Debug.LogWarning("[DevPanel] Sample rules directory not found, creating default rules");
            CreateDefaultRules();
        }

        private void LoadRulesFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Debug.LogWarning($"[DevPanel] Directory not found: {directory}");
                return;
            }
            
            var rules = new List<RuleDto>();
            var jsonFiles = Directory.GetFiles(directory, "*.json");
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var rule = JsonConvert.DeserializeObject<RuleDto>(json);
                    if (rule != null)
                    {
                        rules.Add(rule);
                        Debug.Log($"[DevPanel] Loaded rule from {Path.GetFileName(file)}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[DevPanel] Failed to load {file}: {ex.Message}");
                }
            }
            
            if (rules.Count > 0)
            {
                _repository.ReplaceAll(rules);
                AddRecentEvent($"Loaded {rules.Count} rules from {directory}");
            }
            else
            {
                Debug.LogWarning($"[DevPanel] No valid rules found in {directory}");
            }
        }

        private void AddRuleFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[DevPanel] JSON input is empty");
                return;
            }
            
            try
            {
                var rule = JsonConvert.DeserializeObject<RuleDto>(json);
                if (rule != null)
                {
                    var currentRules = _repository.GetAllRules().ToList();
                    currentRules.Add(rule);
                    _repository.ReplaceAll(currentRules);
                    AddRecentEvent($"Added rule: {rule.id ?? "unnamed"}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DevPanel] Failed to parse JSON: {ex.Message}");
            }
        }

        private void ReplaceAllRulesFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[DevPanel] JSON input is empty");
                return;
            }
            
            try
            {
                // Try to parse as array first
                var rules = JsonConvert.DeserializeObject<List<RuleDto>>(json);
                if (rules != null)
                {
                    _repository.ReplaceAll(rules);
                    AddRecentEvent($"Replaced with {rules.Count} rules");
                    return;
                }
            }
            catch { }
            
            try
            {
                // Try to parse as single rule
                var rule = JsonConvert.DeserializeObject<RuleDto>(json);
                if (rule != null)
                {
                    _repository.ReplaceAll(new[] { rule });
                    AddRecentEvent($"Replaced with 1 rule: {rule.id ?? "unnamed"}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DevPanel] Failed to parse JSON: {ex.Message}");
            }
        }

        private void CreateDefaultRules()
        {
            var rules = new List<RuleDto>
            {
                // Click beep rule
                new RuleDto
                {
                    id = "click_beep",
                    triggers = new List<TriggerDto>
                    {
                        new TriggerDto { type = "event", name = "mouse.left.down" }
                    },
                    conditions = new List<ConditionDto>(),
                    actions = new List<ActionDto>
                    {
                        new ActionDto
                        {
                            type = "service_call",
                            service = "audio.play",
                            data = new Dictionary<string, object> { { "clip", "beep" }, { "volume_0_to_1", 0.6 } }
                        }
                    }
                },
                
                // Debug log timer
                new RuleDto
                {
                    id = "debug_timer",
                    triggers = new List<TriggerDto>
                    {
                        new TriggerDto { type = "time_schedule", every_ms_10_to_600000 = 3000 }
                    },
                    conditions = new List<ConditionDto>
                    {
                        new ConditionDto 
                        { 
                            type = "state_equals", 
                            entity = new List<string> { "ui.mode" }, 
                            equals = new List<string> { "debug" } 
                        }
                    },
                    actions = new List<ActionDto>
                    {
                        new ActionDto
                        {
                            type = "service_call",
                            service = "debug.log",
                            data = new Dictionary<string, object> { { "message", "Debug timer tick" } }
                        }
                    }
                }
            };
            
            _repository.ReplaceAll(rules);
            AddRecentEvent($"Created {rules.Count} default rules");
        }

        private void AddRecentEvent(string eventText)
        {
            _recentEvents.Insert(0, $"{Time.time:F1}: {eventText}");
            if (_recentEvents.Count > MAX_RECENT_EVENTS)
            {
                _recentEvents.RemoveAt(_recentEvents.Count - 1);
            }
        }
    }
}