using System;
using System.Collections.Generic;
using System.Linq;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public class PatternSequenceWatcher
    {
        private readonly EventBus _bus;

        public PatternSequenceWatcher(EventBus bus) => _bus = bus;

        public IDisposable Watch(TriggerDto trigger, Action callback)
        {
            var sequence = trigger.sequence.Select(s => s.name).ToList();
            var window = trigger.within_ms_10_to_5000 ?? 1000;
            var events = new List<(string name, DateTime time)>();

            List<IDisposable> subs = new();
            foreach (var step in sequence)
            {
                subs.Add(_bus.Subscribe(step, () =>
                {
                    events.Add((step, DateTime.UtcNow));
                    // remove old events
                    events.RemoveAll(e => (DateTime.UtcNow - e.time).TotalMilliseconds > window);
                    // check sequence
                    if (IsMatch(events, sequence, window))
                        callback();
                }));
            }
            return new Subscription(subs);
        }

        private bool IsMatch(List<(string name, DateTime time)> events, List<string> sequence, int window)
        {
            if (events.Count < sequence.Count) return false;
            var recent = events.TakeLast(sequence.Count).ToList();
            for (int i = 0; i < sequence.Count; i++)
            {
                if (recent[i].name != sequence[i]) return false;
                if ((recent.Last().time - recent[0].time).TotalMilliseconds > window) return false;
            }
            return true;
        }

        private class Subscription : IDisposable
        {
            private readonly List<IDisposable> _subs;
            public Subscription(List<IDisposable> subs) => _subs = subs;
            public void Dispose() { foreach (var s in _subs) s.Dispose(); }
        }
    }
}
