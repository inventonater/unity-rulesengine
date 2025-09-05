using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class RuleEngine : MonoBehaviour
    {
        private IRuleRepository _repo;
        private EntityStore _store;
        private Services _services;
        private TimerService _timerService;
        private ActionRunner _runner;
        
        private List<(RuleDto rule, PatternSequenceWatcher watcher)> _patterns;
        private readonly Dictionary<string, CancellationTokenSource> _runningRules = new Dictionary<string, CancellationTokenSource>();
        
        private CancellationTokenSource _loopCts;

        public void Initialize(IRuleRepository repo, EntityStore store, Services services, TimerService timerService)
        {
            _repo = repo;
            _store = store;
            _services = services;
            _timerService = timerService;
            _runner = new ActionRunner(_services);
            
            // Initialize pattern watchers
            _patterns = new List<(RuleDto, PatternSequenceWatcher)>();
            foreach (var rule in _repo.GetPatternRules())
            {
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "pattern_sequence" && trigger.sequence != null)
                    {
                        var names = new List<string>();
                        foreach (var step in trigger.sequence)
                        {
                            if (!string.IsNullOrEmpty(step.name))
                            {
                                names.Add(step.name);
                            }
                        }
                        
                        if (names.Count > 0)
                        {
                            int withinMs = Mathf.Clamp(trigger.within_ms_10_to_5000, 10, 5000);
                            var watcher = new PatternSequenceWatcher(names, withinMs);
                            _patterns.Add((rule, watcher));
                            
                            Debug.Log($"[RuleEngine] Registered pattern sequence for rule '{rule.id}' with {names.Count} steps");
                        }
                    }
                }
            }
            
            // Initialize timers
            _timerService.RefreshTimersFromRules(_repo.GetAllRules());
            
            // Start the main event loop
            _loopCts = new CancellationTokenSource();
            Run(_loopCts.Token).Forget();
            
            Debug.Log($"[RuleEngine] Initialized with {_patterns.Count} pattern watchers");
        }

        private void OnDisable()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
            
            // Cancel all running rules
            foreach (var cts in _runningRules.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            _runningRules.Clear();
        }

        private async UniTaskVoid Run(CancellationToken ct)
        {
            await foreach (var evt in EventBus.GetStream(ct))
            {
                Debug.Log($"[RuleEngine] Processing event: {evt.Name}");
                
                // Check event triggers
                foreach (var rule in _repo.GetCandidatesFor($"event:{evt.Name}"))
                {
                    TryStartRule(rule, ct).Forget();
                }
                
                // Check pattern sequences
                foreach (var (rule, watcher) in _patterns)
                {
                    if (watcher.OnEvent(evt.Name, evt.Timestamp))
                    {
                        Debug.Log($"[RuleEngine] Pattern sequence completed for rule '{rule.id}'");
                        TryStartRule(rule, ct).Forget();
                    }
                }
                
                // Check timer events
                if (evt.Name.StartsWith("timer:"))
                {
                    string timerKey = evt.Name.Replace("timer:", "time:");
                    foreach (var rule in _repo.GetCandidatesFor(timerKey))
                    {
                        TryStartRule(rule, ct).Forget();
                    }
                }
                
                // Check threshold events
                if (evt.Name.StartsWith("threshold:"))
                {
                    // Parse threshold event (e.g., "threshold:sensor.mouse_speed:above:600:sustained")
                    var parts = evt.Name.Split(':');
                    if (parts.Length >= 4)
                    {
                        string entity = parts[1];
                        string direction = parts[2]; // "above" or "below"
                        string key = $"num:{direction}:{entity}";
                        
                        foreach (var rule in _repo.GetCandidatesFor(key))
                        {
                            // Only trigger if this was a sustained threshold (has "sustained" suffix) or no duration required
                            bool isSustained = evt.Name.EndsWith(":sustained");
                            bool requiresDuration = false;
                            
                            foreach (var trigger in rule.triggers)
                            {
                                if (trigger.type == "numeric_threshold" && 
                                    trigger.entity?.Count > 0 && 
                                    trigger.entity[0] == entity)
                                {
                                    requiresDuration = trigger.for_ms_0_to_60000 > 0;
                                    break;
                                }
                            }
                            
                            if (!requiresDuration || isSustained)
                            {
                                TryStartRule(rule, ct).Forget();
                            }
                        }
                    }
                }
            }
        }

        private async UniTaskVoid TryStartRule(RuleDto rule, CancellationToken globalCt)
        {
            if (rule == null || string.IsNullOrEmpty(rule.id))
            {
                Debug.LogWarning("[RuleEngine] Invalid rule (null or missing id)");
                return;
            }
            
            // Check conditions
            if (!ConditionEval.EvaluateAll(rule.conditions, _store))
            {
                Debug.Log($"[RuleEngine] Conditions not met for rule '{rule.id}'");
                return;
            }
            
            // Handle rule modes
            string mode = rule.mode ?? "single";
            
            switch (mode)
            {
                case "single":
                    if (_runningRules.ContainsKey(rule.id))
                    {
                        Debug.Log($"[RuleEngine] Rule '{rule.id}' already running (single mode)");
                        return;
                    }
                    break;
                    
                case "restart":
                    if (_runningRules.TryGetValue(rule.id, out var existingCts))
                    {
                        Debug.Log($"[RuleEngine] Cancelling existing instance of rule '{rule.id}' (restart mode)");
                        existingCts.Cancel();
                        existingCts.Dispose();
                        _runningRules.Remove(rule.id);
                    }
                    break;
                    
                case "parallel":
                    // Allow multiple instances
                    break;
                    
                default:
                    Debug.LogWarning($"[RuleEngine] Unknown mode '{mode}' for rule '{rule.id}', using 'single'");
                    if (_runningRules.ContainsKey(rule.id))
                    {
                        return;
                    }
                    break;
            }
            
            // Start the rule
            Debug.Log($"[RuleEngine] Starting rule '{rule.id}' in mode '{mode}'");
            
            var ruleCts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
            
            if (mode != "parallel")
            {
                _runningRules[rule.id] = ruleCts;
            }
            
            try
            {
                await _runner.RunActionsAsync(rule.actions, ruleCts.Token);
                Debug.Log($"[RuleEngine] Rule '{rule.id}' completed");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log($"[RuleEngine] Rule '{rule.id}' cancelled");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RuleEngine] Rule '{rule.id}' failed: {ex.Message}");
            }
            finally
            {
                if (mode != "parallel" && _runningRules.ContainsKey(rule.id))
                {
                    _runningRules.Remove(rule.id);
                }
                ruleCts.Dispose();
            }
        }
    }
}