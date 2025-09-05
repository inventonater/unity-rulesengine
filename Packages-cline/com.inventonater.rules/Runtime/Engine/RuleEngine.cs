using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Core rule engine that processes triggers and executes actions
    /// </summary>
    public class RuleEngine : MonoBehaviour
    {
        private IRuleRepository _repo;
        private EntityStore _store;
        private IServices _services;
        private TimerService _timerService;
        private ActionRunner _runner;
        private ConditionEval _conditionEval;
        
        private List<(RuleDto rule, PatternSequenceWatcher watcher)> _patternWatchers;
        private Dictionary<string, CancellationTokenSource> _runningRules;
        private Dictionary<string, List<UniTask>> _queuedRules;
        private Dictionary<string, float> _thresholdStartTimes;
        
        private CancellationTokenSource _loopCts;
        
        public void Initialize(IRuleRepository repo, EntityStore store, IServices services, TimerService timerService)
        {
            _repo = repo;
            _store = store;
            _services = services;
            _timerService = timerService;
            _runner = new ActionRunner(_services);
            _conditionEval = new ConditionEval(_store);
            
            _runningRules = new Dictionary<string, CancellationTokenSource>();
            _queuedRules = new Dictionary<string, List<UniTask>>();
            _thresholdStartTimes = new Dictionary<string, float>();
            
            // Set up pattern watchers
            InitializePatternWatchers();
            
            // Set up timers
            _timerService.ProcessScheduleTriggers(_repo.GetAllRules());
            
            // Start main event loop
            _loopCts = new CancellationTokenSource();
            Run(_loopCts.Token).Forget();
            
            Debug.Log("[RuleEngine] Initialized");
        }
        
        private void OnDestroy()
        {
            _loopCts?.Cancel();
            foreach (var cts in _runningRules.Values)
            {
                cts?.Cancel();
            }
        }
        
        private void InitializePatternWatchers()
        {
            _patternWatchers = new List<(RuleDto, PatternSequenceWatcher)>();
            
            foreach (var rule in _repo.GetPatternRules())
            {
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "pattern_sequence" && trigger.sequence != null)
                    {
                        var names = trigger.sequence.Select(s => s.name).Where(n => !string.IsNullOrEmpty(n));
                        var withinMs = Mathf.Clamp(trigger.within_ms_10_to_5000, 10, 5000);
                        var watcher = new PatternSequenceWatcher(names, withinMs);
                        _patternWatchers.Add((rule, watcher));
                    }
                }
            }
            
            Debug.Log($"[RuleEngine] Initialized {_patternWatchers.Count} pattern watchers");
        }
        
        private async UniTaskVoid Run(CancellationToken ct)
        {
            await foreach (var e in EventBus.GetStream(ct))
            {
                ProcessEvent(e, ct);
            }
        }
        
        private void ProcessEvent(EventData e, CancellationToken ct)
        {
            // Check direct event triggers
            foreach (var rule in _repo.GetCandidatesFor($"event:{e.Name}"))
            {
                TryStartRule(rule, ct);
            }
            
            // Check pattern sequences
            foreach (var (rule, watcher) in _patternWatchers)
            {
                if (watcher.OnEvent(e.Name, e.Timestamp))
                {
                    Debug.Log($"[RuleEngine] Pattern sequence completed for rule: {rule.id}");
                    TryStartRule(rule, ct);
                }
            }
            
            // Check timer events
            if (e.Name.StartsWith("time:"))
            {
                foreach (var rule in _repo.GetCandidatesFor(e.Name))
                {
                    TryStartRule(rule, ct);
                }
            }
        }
        
        private void TryStartRule(RuleDto rule, CancellationToken globalCt)
        {
            // Check numeric thresholds
            if (!CheckNumericThresholds(rule))
                return;
            
            // Evaluate conditions
            if (!_conditionEval.EvaluateAll(rule.conditions))
            {
                Debug.Log($"[RuleEngine] Conditions not met for rule: {rule.id}");
                return;
            }
            
            // Execute based on mode
            var mode = string.IsNullOrEmpty(rule.mode) ? "single" : rule.mode.ToLower();
            
            switch (mode)
            {
                case "single":
                    ExecuteSingleMode(rule, globalCt);
                    break;
                    
                case "restart":
                    ExecuteRestartMode(rule, globalCt);
                    break;
                    
                case "queued":
                    ExecuteQueuedMode(rule, globalCt);
                    break;
                    
                case "parallel":
                    ExecuteParallelMode(rule, globalCt);
                    break;
                    
                default:
                    Debug.LogWarning($"Unknown mode '{mode}' for rule {rule.id}, using single");
                    ExecuteSingleMode(rule, globalCt);
                    break;
            }
        }
        
        private bool CheckNumericThresholds(RuleDto rule)
        {
            if (rule.triggers == null) return true;
            
            foreach (var trigger in rule.triggers)
            {
                if (trigger.type != "numeric_threshold") continue;
                if (trigger.entity == null || trigger.entity.Count == 0) continue;
                
                var entityKey = trigger.entity[0];
                var checkAbove = trigger.above != 0;
                var threshold = checkAbove ? trigger.above : trigger.below;
                var forMs = trigger.for_ms_0_to_60000;
                
                if (!_store.IsThresholdMet(entityKey, threshold, checkAbove, forMs / 1000f))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private void ExecuteSingleMode(RuleDto rule, CancellationToken globalCt)
        {
            if (_runningRules.ContainsKey(rule.id))
            {
                Debug.Log($"[RuleEngine] Rule {rule.id} already running (single mode)");
                return;
            }
            
            var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
            _runningRules[rule.id] = cts;
            
            RunRuleActions(rule, cts.Token).Forget();
        }
        
        private void ExecuteRestartMode(RuleDto rule, CancellationToken globalCt)
        {
            // Cancel existing execution if any
            if (_runningRules.TryGetValue(rule.id, out var existingCts))
            {
                existingCts.Cancel();
                _runningRules.Remove(rule.id);
                Debug.Log($"[RuleEngine] Restarting rule {rule.id}");
            }
            
            var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
            _runningRules[rule.id] = cts;
            
            RunRuleActions(rule, cts.Token).Forget();
        }
        
        private void ExecuteQueuedMode(RuleDto rule, CancellationToken globalCt)
        {
            if (!_queuedRules.ContainsKey(rule.id))
            {
                _queuedRules[rule.id] = new List<UniTask>();
            }
            
            var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
            var task = RunRuleActions(rule, cts.Token);
            _queuedRules[rule.id].Add(task);
            
            ProcessQueue(rule.id).Forget();
        }
        
        private void ExecuteParallelMode(RuleDto rule, CancellationToken globalCt)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
            RunRuleActions(rule, cts.Token).Forget();
        }
        
        private async UniTaskVoid ProcessQueue(string ruleId)
        {
            if (!_queuedRules.TryGetValue(ruleId, out var queue))
                return;
            
            while (queue.Count > 0)
            {
                var task = queue[0];
                queue.RemoveAt(0);
                
                try
                {
                    await task;
                }
                catch (System.OperationCanceledException)
                {
                    // Rule was cancelled
                }
            }
        }
        
        private async UniTask RunRuleActions(RuleDto rule, CancellationToken ct)
        {
            Debug.Log($"[RuleEngine] Starting rule: {rule.id}");
            
            try
            {
                await _runner.RunActionsAsync(rule.actions, ct);
                Debug.Log($"[RuleEngine] Completed rule: {rule.id}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log($"[RuleEngine] Cancelled rule: {rule.id}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RuleEngine] Error in rule {rule.id}: {ex.Message}");
            }
            finally
            {
                _runningRules.Remove(rule.id);
            }
        }
        
        public void ReloadRules()
        {
            // Cancel all running rules
            foreach (var cts in _runningRules.Values)
            {
                cts?.Cancel();
            }
            _runningRules.Clear();
            _queuedRules.Clear();
            
            // Reinitialize
            InitializePatternWatchers();
            _timerService.ProcessScheduleTriggers(_repo.GetAllRules());
            
            Debug.Log("[RuleEngine] Rules reloaded");
        }
    }
}
