using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class RuleEngine : IDisposable
    {
        private readonly IRuleRepository _repository;
        private readonly IEntityStore _store;
        private readonly IEventBus _eventBus;
        private readonly IServices _services;
        private readonly IActionRunner _actionRunner;
        private readonly IConditionEvaluator _conditionEvaluator;
        
        private readonly List<IDisposable> _disposables = new();
        private readonly Dictionary<string, CancellationTokenSource> _runningRules = new();
        private readonly Dictionary<string, PatternSequenceWatcher> _patternWatchers = new();
        
        public RuleEngine(
            IRuleRepository repository,
            IEntityStore store,
            IEventBus eventBus,
            IServices services,
            IActionRunner actionRunner = null,
            IConditionEvaluator conditionEvaluator = null)
        {
            _repository = repository;
            _store = store;
            _eventBus = eventBus;
            _services = services;
            _actionRunner = actionRunner ?? new ActionRunner(services, store);
            _conditionEvaluator = conditionEvaluator ?? new ConditionEvaluator(store);
        }

        public void Initialize()
        {
            var rules = _repository.GetAllRules();
            foreach (var rule in rules)
            {
                RegisterRule(rule);
            }
        }

        private void RegisterRule(RuleDto rule)
        {
            foreach (var trigger in rule.triggers ?? Enumerable.Empty<TriggerDto>())
            {
                switch (trigger.type)
                {
                    case "event":
                        RegisterEventTrigger(rule, trigger);
                        break;
                    
                    case "time_schedule":
                        RegisterTimeTrigger(rule, trigger);
                        break;
                    
                    case "numeric_threshold":
                        RegisterThresholdTrigger(rule, trigger);
                        break;
                    
                    case "pattern_sequence":
                        RegisterPatternTrigger(rule, trigger);
                        break;
                }
            }
        }

        private void RegisterEventTrigger(RuleDto rule, TriggerDto trigger)
        {
            var subscription = _eventBus.Subscribe(trigger.name, () => ExecuteRule(rule));
            _disposables.Add(subscription);
        }

        private void RegisterTimeTrigger(RuleDto rule, TriggerDto trigger)
        {
            int intervalMs = trigger.every_ms_10_to_600000 ?? 1000;
            intervalMs = Mathf.Clamp(intervalMs, 10, 600000);
            
            var timer = new TimerService(intervalMs, () => ExecuteRule(rule));
            timer.Start();
            _disposables.Add(timer);
        }

        private void RegisterThresholdTrigger(RuleDto rule, TriggerDto trigger)
        {
            void OnEntityChanged(string key, object value)
            {
                if (key != trigger.entity) return;
                
                if (!double.TryParse(value?.ToString(), out double numValue)) return;
                
                bool shouldTrigger = false;
                
                if (!string.IsNullOrEmpty(trigger.above) && 
                    double.TryParse(trigger.above, out double aboveValue))
                {
                    shouldTrigger |= numValue > aboveValue;
                }
                
                if (!string.IsNullOrEmpty(trigger.below) && 
                    double.TryParse(trigger.below, out double belowValue))
                {
                    shouldTrigger |= numValue < belowValue;
                }
                
                if (shouldTrigger)
                {
                    ExecuteRule(rule);
                }
            }
            
            _store.OnChanged += OnEntityChanged;
            _disposables.Add(new DisposableAction(() => _store.OnChanged -= OnEntityChanged));
        }

        private void RegisterPatternTrigger(RuleDto rule, TriggerDto trigger)
        {
            if (trigger.sequence == null || trigger.sequence.Count == 0) return;
            
            var eventNames = trigger.sequence
                .Where(s => !string.IsNullOrEmpty(s.name))
                .Select(s => s.name)
                .ToList();
            
            if (eventNames.Count == 0) return;
            
            int withinMs = Mathf.Clamp(trigger.within_ms_10_to_5000, 10, 5000);
            
            var watcher = new PatternSequenceWatcher(eventNames, withinMs, _eventBus);
            watcher.OnPatternCompleted += () => ExecuteRule(rule);
            
            _patternWatchers[rule.id + "_" + trigger.name] = watcher;
            _disposables.Add(watcher);
        }

        private void ExecuteRule(RuleDto rule)
        {
            if (!_conditionEvaluator.Evaluate(rule.conditions ?? new List<ConditionDto>()))
                return;
            
            if (rule.mode == "single" && _runningRules.ContainsKey(rule.id))
                return;
            
            var cts = new CancellationTokenSource();
            
            if (rule.mode == "single")
            {
                if (_runningRules.TryGetValue(rule.id, out var existingCts))
                {
                    existingCts.Cancel();
                }
                _runningRules[rule.id] = cts;
            }
            
            ExecuteActionsAsync(rule, cts.Token).Forget();
        }

        private async UniTaskVoid ExecuteActionsAsync(RuleDto rule, CancellationToken ct)
        {
            try
            {
                await _actionRunner.RunAsync(rule.actions ?? new List<ActionDto>(), ct);
            }
            finally
            {
                if (rule.mode == "single")
                {
                    _runningRules.Remove(rule.id);
                }
            }
        }

        public void Dispose()
        {
            foreach (var cts in _runningRules.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            _runningRules.Clear();
            
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
            _disposables.Clear();
            
            _patternWatchers.Clear();
        }
    }

    internal class DisposableAction : IDisposable
    {
        private Action _action;
        
        public DisposableAction(Action action)
        {
            _action = action;
        }
        
        public void Dispose()
        {
            _action?.Invoke();
            _action = null;
        }
    }
}