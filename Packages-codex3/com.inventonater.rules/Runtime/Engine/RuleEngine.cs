using System;
using System.Collections.Generic;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public class RuleEngine : IDisposable
    {
        private readonly EventBus _bus;
        private readonly TimerService _timers;
        private readonly PatternSequenceWatcher _patterns;
        private readonly ConditionEval _conditions;
        private readonly ActionRunner _actions;
        private readonly EntityStore _store;
        private readonly List<IDisposable> _subs = new();

        public RuleEngine(EventBus bus, TimerService timers, PatternSequenceWatcher patterns, ConditionEval conditions, ActionRunner actions, EntityStore store)
        {
            _bus = bus;
            _timers = timers;
            _patterns = patterns;
            _conditions = conditions;
            _actions = actions;
            _store = store;
        }

        public void Load(IEnumerable<RuleDto> rules)
        {
            foreach (var rule in rules)
            {
                foreach (var trig in rule.triggers)
                {
                    switch (trig.type)
                    {
                        case "event":
                            _subs.Add(_bus.Subscribe(trig.name, () => OnRule(rule)));
                            break;
                        case "time_schedule":
                            int interval = trig.every_ms_10_to_600000 ?? 1000;
                            _timers.Every(interval, () => OnRule(rule));
                            break;
                        case "pattern_sequence":
                            _subs.Add(_patterns.Watch(trig, () => OnRule(rule)));
                            break;
                        case "numeric_threshold":
                            _timers.Every(100, () =>
                            {
                                double val = _store.Get<double>(trig.entity);
                                bool above = trig.above != null && double.TryParse(trig.above, out var a) && val > a;
                                bool below = trig.below != null && double.TryParse(trig.below, out var b) && val < b;
                                if (above || below) OnRule(rule);
                            });
                            break;
                    }
                }
            }
        }

        private void OnRule(RuleDto rule)
        {
            if (_conditions.Evaluate(rule.conditions))
                _actions.Run(rule.actions);
        }

        public void Dispose()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _timers.Dispose();
        }
    }
}
