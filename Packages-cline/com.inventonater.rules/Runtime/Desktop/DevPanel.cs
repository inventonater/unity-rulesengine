using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Developer panel for runtime rule management
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        private RuleRepository _repository;
        private RuleEngine _engine;
        private EntityStore _store;
        
        private bool _showPanel = true;
        private string _jsonInput = "";
        private Vector2 _scrollPosition;
        private string _eventInput = "";
        
        private Rect _panelRect;
        private bool _isDragging;
        private Vector2 _dragOffset;
        
        // Stats
        private int _ruleCount = 0;
        private string _currentMode = "normal";
        
        private void Awake()
        {
            _repository = GetComponent<RuleRepository>();
            if (_repository == null)
            {
                _repository = gameObject.AddComponent<RuleRepository>();
            }
            
            _engine = GetComponent<RuleEngine>();
            if (_engine == null)
            {
                _engine = gameObject.AddComponent<RuleEngine>();
            }
            
            _store = GetComponent<EntityStore>();
            if (_store == null)
            {
                _store = gameObject.AddComponent<EntityStore>();
            }
        }
        
        private void Start()
        {
            // Set initial panel position
            _panelRect = new Rect(10, 10, 400, 500);
            
            // Try to load default rules from samples
            LoadDefaultRules();
        }
        
        private void Update()
        {
            // Toggle panel with F2
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _showPanel = !_showPanel;
            }
            
            // Update stats
            _ruleCount = _repository.GetAllRules().Count();
            _currentMode = _store.GetState("ui.mode", "normal");
        }
        
        private void OnGUI()
        {
            if (!_showPanel) return;
            
            // Handle dragging
            HandleDragging();
            
            // Draw panel
            GUI.Box(_panelRect, "");
            
            GUILayout.BeginArea(_panelRect);
            GUILayout.Space(5);
            
            // Title
            GUILayout.Label("Rules Engine DevPanel", GUI.skin.box);
            GUILayout.Label($"Press F2 to toggle | F1 for debug mode");
            
            // Stats
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Rules: {_ruleCount}");
            GUILayout.Label($"Mode: {_currentMode}");
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // JSON Input
            GUILayout.Label("Paste JSON Rule:");
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            _jsonInput = GUILayout.TextArea(_jsonInput, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            
            // Load buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Rule"))
            {
                LoadRuleFromInput();
            }
            if (GUILayout.Button("Clear"))
            {
                _jsonInput = "";
            }
            if (GUILayout.Button("Reload All"))
            {
                _engine.ReloadRules();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Event emitter
            GUILayout.Label("Emit Event:");
            GUILayout.BeginHorizontal();
            _eventInput = GUILayout.TextField(_eventInput);
            if (GUILayout.Button("Emit", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_eventInput))
                {
                    EventBus.Publish(_eventInput);
                    Debug.Log($"[DevPanel] Emitted event: {_eventInput}");
                }
            }
            GUILayout.EndHorizontal();
            
            // Quick event buttons
            GUILayout.Label("Quick Events:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Click"))
            {
                EventBus.Publish("mouse.left.down");
            }
            if (GUILayout.Button("Space"))
            {
                EventBus.Publish("key.space.down");
            }
            if (GUILayout.Button("Timer"))
            {
                EventBus.Publish("time:2000");
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Sample rules loader
            GUILayout.Label("Load Samples:");
            if (GUILayout.Button("Load All Sample Rules"))
            {
                LoadAllSampleRules();
            }
            
            GUILayout.Space(10);
            
            // Instructions
            GUILayout.Label("Controls:", GUI.skin.box);
            GUILayout.Label("• Click: Beep");
            GUILayout.Label("• Hold LMB: Toast");
            GUILayout.Label("• Double-click: Loud beep");
            GUILayout.Label("• Space x2: Triple beep");
            GUILayout.Label("• Konami: ↑↑↓↓←→←→BA");
            GUILayout.Label("• F1: Toggle debug mode");
            
            GUILayout.EndArea();
        }
        
        private void HandleDragging()
        {
            var e = Event.current;
            var titleRect = new Rect(_panelRect.x, _panelRect.y, _panelRect.width, 25);
            
            if (e.type == EventType.MouseDown && titleRect.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = e.mousePosition - new Vector2(_panelRect.x, _panelRect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _panelRect.x = e.mousePosition.x - _dragOffset.x;
                _panelRect.y = e.mousePosition.y - _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }
        }
        
        private void LoadRuleFromInput()
        {
            if (string.IsNullOrEmpty(_jsonInput))
            {
                Debug.LogWarning("[DevPanel] No JSON input to load");
                return;
            }
            
            _repository.LoadSingleRuleFromJson(_jsonInput);
            _engine.ReloadRules();
            _jsonInput = "";
            Debug.Log("[DevPanel] Rule loaded");
        }
        
        private void LoadDefaultRules()
        {
            // Try loading from a default file if it exists
            var defaultPath = Path.Combine(Application.dataPath, "Rules", "default.json");
            if (File.Exists(defaultPath))
            {
                var json = File.ReadAllText(defaultPath);
                _repository.LoadFromJson(json);
                _engine.ReloadRules();
                Debug.Log($"[DevPanel] Loaded default rules from {defaultPath}");
            }
        }
        
        private void LoadAllSampleRules()
        {
            var rules = new List<RuleDto>();
            
            // Add all sample rules programmatically
            rules.Add(CreateClickBeepRule());
            rules.Add(CreateHoldToastRule());
            rules.Add(CreatePeriodicHintRule());
            rules.Add(CreateSpeedGateRule());
            rules.Add(CreateDoubleClickRule());
            rules.Add(CreateKonamiRule());
            rules.Add(CreateHeartbeatRule());
            rules.Add(CreateSpaceComboRule());
            
            _repository.ReplaceAll(rules);
            _engine.ReloadRules();
            
            Debug.Log($"[DevPanel] Loaded {rules.Count} sample rules");
        }
        
        private RuleDto CreateClickBeepRule()
        {
            return new RuleDto
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
                        data = new Dictionary<string, object>
                        {
                            { "clip", "beep" },
                            { "volume_0_to_1", 0.6 }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateHoldToastRule()
        {
            return new RuleDto
            {
                id = "hold_to_toast",
                mode = "single",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto
                    {
                        type = "numeric_threshold",
                        entity = new List<string> { "sensor.mouse_button_left" },
                        above = 0.5,
                        for_ms_0_to_60000 = 300
                    }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "service_call",
                        service = "ui.toast",
                        data = new Dictionary<string, object>
                        {
                            { "text", "Held!" },
                            { "duration_ms_0_to_10000", 1000 }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreatePeriodicHintRule()
        {
            return new RuleDto
            {
                id = "periodic_hint",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto { type = "time_schedule", every_ms_10_to_600000 = 5000 }
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
                        data = new Dictionary<string, object>
                        {
                            { "message", "Debug hint tick" }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateSpeedGateRule()
        {
            return new RuleDto
            {
                id = "speed_gate",
                mode = "restart",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto
                    {
                        type = "numeric_threshold",
                        entity = new List<string> { "sensor.mouse_speed" },
                        above = 600,
                        for_ms_0_to_60000 = 100
                    }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "repeat_count",
                        count_1_to_20 = 3,
                        actions = new List<ActionDto>
                        {
                            new ActionDto
                            {
                                type = "service_call",
                                service = "audio.play",
                                data = new Dictionary<string, object>
                                {
                                    { "clip", "beep" },
                                    { "volume_0_to_1", 0.8 }
                                }
                            },
                            new ActionDto
                            {
                                type = "wait_duration",
                                duration_ms_0_to_60000 = 150
                            }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateDoubleClickRule()
        {
            return new RuleDto
            {
                id = "double_click_pattern",
                mode = "single",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto
                    {
                        type = "pattern_sequence",
                        within_ms_10_to_5000 = 250,
                        sequence = new List<PatternStep>
                        {
                            new PatternStep { name = "mouse.left.down" },
                            new PatternStep { name = "mouse.left.down" }
                        }
                    }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "service_call",
                        service = "audio.play",
                        data = new Dictionary<string, object>
                        {
                            { "clip", "beep" },
                            { "volume_0_to_1", 1.0 }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateKonamiRule()
        {
            return new RuleDto
            {
                id = "konami_pattern",
                mode = "single",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto
                    {
                        type = "pattern_sequence",
                        within_ms_10_to_5000 = 3000,
                        sequence = new List<PatternStep>
                        {
                            new PatternStep { name = "key.arrow_up.down" },
                            new PatternStep { name = "key.arrow_up.down" },
                            new PatternStep { name = "key.arrow_down.down" },
                            new PatternStep { name = "key.arrow_down.down" },
                            new PatternStep { name = "key.arrow_left.down" },
                            new PatternStep { name = "key.arrow_right.down" },
                            new PatternStep { name = "key.arrow_left.down" },
                            new PatternStep { name = "key.arrow_right.down" },
                            new PatternStep { name = "key.b.down" },
                            new PatternStep { name = "key.a.down" }
                        }
                    }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "service_call",
                        service = "ui.toast",
                        data = new Dictionary<string, object>
                        {
                            { "text", "KONAMI CODE ACTIVATED!" },
                            { "duration_ms_0_to_10000", 2000 }
                        }
                    },
                    new ActionDto
                    {
                        type = "repeat_count",
                        count_1_to_20 = 5,
                        actions = new List<ActionDto>
                        {
                            new ActionDto
                            {
                                type = "service_call",
                                service = "audio.play",
                                data = new Dictionary<string, object>
                                {
                                    { "clip", "beep" },
                                    { "volume_0_to_1", 0.9 }
                                }
                            },
                            new ActionDto
                            {
                                type = "wait_duration",
                                duration_ms_0_to_60000 = 100
                            }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateHeartbeatRule()
        {
            return new RuleDto
            {
                id = "heartbeat_log",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto { type = "time_schedule", every_ms_10_to_600000 = 10000 }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "service_call",
                        service = "debug.log",
                        data = new Dictionary<string, object>
                        {
                            { "message", "Heartbeat tick" }
                        }
                    }
                }
            };
        }
        
        private RuleDto CreateSpaceComboRule()
        {
            return new RuleDto
            {
                id = "space_combo_triple_beep",
                triggers = new List<TriggerDto>
                {
                    new TriggerDto
                    {
                        type = "pattern_sequence",
                        within_ms_10_to_5000 = 300,
                        sequence = new List<PatternStep>
                        {
                            new PatternStep { name = "key.space.down" },
                            new PatternStep { name = "key.space.down" }
                        }
                    }
                },
                conditions = new List<ConditionDto>(),
                actions = new List<ActionDto>
                {
                    new ActionDto
                    {
                        type = "repeat_count",
                        count_1_to_20 = 3,
                        actions = new List<ActionDto>
                        {
                            new ActionDto
                            {
                                type = "service_call",
                                service = "audio.play",
                                data = new Dictionary<string, object>
                                {
                                    { "clip", "beep" },
                                    { "volume_0_to_1", 0.8 }
                                }
                            },
                            new ActionDto
                            {
                                type = "wait_duration",
                                duration_ms_0_to_60000 = 120
                            }
                        }
                    }
                }
            };
        }
    }
}
