using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public class RuleEngine : IDisposable
    {
        private readonly EventBus _bus;
        private readonly TimerService _timer;
        private readonly EntityStore _store;
        private readonly IServiceExecutor _services;
        private readonly List<IDisposable> _disposables = new();

        public RuleEngine(EventBus bus, TimerService timer, EntityStore store, IServiceExecutor services)
        {
            _bus = bus; _timer = timer; _store = store; _services = services;
        }

        public void Load(IEnumerable<RuleDto> rules)
        {
            foreach (var rule in rules)
                RegisterRule(rule);
        }

        private void RegisterRule(RuleDto rule)
        {
            var runner = new ActionRunner(_services);
            Action fire = () =>
            {
                if (ConditionEval.Evaluate(rule.Conditions, _store))
                    Task.Run(() => runner.RunAsync(rule.Actions, _store));
            };

            foreach (var t in rule.Triggers)
            {
                switch (t.Type)
                {
                    case "event":
                        Action<EventData> h = _ => fire();
                        _bus.Subscribe(t.Name, h);
                        _disposables.Add(new ActionDisposable(() => _bus.Unsubscribe(t.Name, h)));
                        break;
                    case "time_schedule":
                        var disp = _timer.Register(t.EveryMs ?? 1000, fire);
                        _disposables.Add(disp);
                        break;
                    case "numeric_threshold":
                        void NumHandler(string key, double value)
                        {
                            if (key != t.Entity) return;
                            bool ok = true;
                            if (t.Above.HasValue && !(value > t.Above.Value)) ok = false;
                            if (t.Below.HasValue && !(value < t.Below.Value)) ok = false;
                            if (ok) fire();
                        }
                        _store.NumberChanged += NumHandler;
                        _disposables.Add(new ActionDisposable(() => _store.NumberChanged -= NumHandler));
                        break;
                    case "pattern_sequence":
                        var names = new List<string>();
                        foreach (var p in t.Sequence) names.Add(p.Name);
                        var watcher = new PatternSequenceWatcher(_bus, names, t.WithinMs ?? 500, fire);
                        _disposables.Add(watcher);
                        break;
                }
            }
        }

        public void Dispose()
        {
            foreach (var d in _disposables)
                d.Dispose();
            _disposables.Clear();
        }

        private class ActionDisposable : IDisposable
        {
            private readonly Action _action;
            public ActionDisposable(Action action) { _action = action; }
            public void Dispose() { _action(); }
        }
    }
}
