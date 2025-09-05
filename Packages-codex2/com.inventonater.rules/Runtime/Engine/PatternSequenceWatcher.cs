using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Inventonater.RulesEngine.Engine
{
    public class PatternSequenceWatcher : IDisposable
    {
        private readonly EventBus _bus;
        private readonly List<string> _sequence;
        private readonly int _withinMs;
        private readonly Action _callback;
        private readonly List<(string name, long time)> _recent = new();
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly Dictionary<string, Action> _handlers = new();

        public PatternSequenceWatcher(EventBus bus, List<string> sequence, int withinMs, Action callback)
        {
            _bus = bus;
            _sequence = sequence;
            _withinMs = withinMs;
            _callback = callback;
            foreach (var evt in sequence)
            {
                Action h = () => OnEvent(evt);
                _handlers[evt] = h;
                _bus.Subscribe(evt, h);
            }
        }

        private void OnEvent(string name)
        {
            var now = _watch.ElapsedMilliseconds;
            _recent.Add((name, now));
            _recent.RemoveAll(e => now - e.time > _withinMs);
            if (_recent.Count < _sequence.Count) return;
            for (int i = 0; i < _sequence.Count; i++)
            {
                var idx = _recent.Count - _sequence.Count + i;
                if (_recent[idx].name != _sequence[i]) return;
            }
            _callback();
            _recent.Clear();
        }

        public void Dispose()
        {
            foreach (var pair in _handlers)
            {
                _bus.Unsubscribe(pair.Key, pair.Value);
            }
            _handlers.Clear();
        }
    }
}
