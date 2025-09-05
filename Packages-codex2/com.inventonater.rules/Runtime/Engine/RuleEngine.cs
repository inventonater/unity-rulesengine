using System;
using System.Collections.Generic;
using Inventonater.RulesEngine.Authoring;
using Inventonater.RulesEngine.Desktop;

namespace Inventonater.RulesEngine.Engine
{
    public class RuleEngine : IDisposable
    {
        private readonly EventBus _bus;
        private readonly Services _services;
        private readonly EntityStore _store;
        private readonly List<IDisposable> _disposables = new();

        public RuleEngine(EventBus bus, Services services, EntityStore store)
        {
            _bus = bus;
            _services = services;
            _store = store;
        }

        public void AddRule(RuleDto rule)
        {
            foreach (var trigger in rule.Triggers)
            {
                switch (trigger)
                {
                    case EventTriggerDto evt:
                        _bus.Subscribe(evt.Name, () => Execute(rule));
                        break;
                    case TimeScheduleTriggerDto time:
                        var timer = new TimerService(time.EveryMs, () => Execute(rule));
                        timer.Start();
                        _disposables.Add(timer);
                        break;
                    case NumericThresholdTriggerDto num:
                        void OnChanged(string key, object value)
                        {
                            if (key != num.Entity) return;
                            var d = Convert.ToDouble(value);
                            if (num.Above.HasValue && d <= num.Above.Value) return;
                            if (num.Below.HasValue && d >= num.Below.Value) return;
                            Execute(rule);
                        }
                        _store.OnChanged += OnChanged;
                        _disposables.Add(new DelegateDisposer(() => _store.OnChanged -= OnChanged));
                        break;
                    case PatternSequenceTriggerDto pat:
                        var seq = new List<string>();
                        foreach (var s in pat.Sequence) seq.Add(s.Name);
                        var watcher = new PatternSequenceWatcher(_bus, seq, pat.WithinMs, () => Execute(rule));
                        _disposables.Add(watcher);
                        break;
                }
            }
        }

        private void Execute(RuleDto rule)
        {
            if (!ConditionEval.Evaluate(rule.Conditions, _store)) return;
            ActionRunner.Run(rule.Actions, _services, _store);
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
            _disposables.Clear();
        }

        private class DelegateDisposer : IDisposable
        {
            private readonly Action _dispose;
            public DelegateDisposer(Action dispose) { _dispose = dispose; }
            public void Dispose() => _dispose();
        }
    }
}
