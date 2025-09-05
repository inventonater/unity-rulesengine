using System;
using System.Collections.Generic;

namespace Inventonater.Rules.Engine
{
    public class PatternSequenceWatcher : IDisposable
    {
        private readonly EventBus _bus;
        private readonly List<string> _sequence;
        private readonly int _windowMs;
        private readonly Action _callback;
        private readonly List<Action<EventData>> _subs = new();
        private int _index = 0;
        private DateTime _first;

        public PatternSequenceWatcher(EventBus bus, IEnumerable<string> sequence, int withinMs, Action callback)
        {
            _bus = bus; _sequence = new List<string>(sequence); _windowMs = withinMs; _callback = callback;
            foreach (var name in _sequence)
            {
                Action<EventData> h = OnEvent;
                _bus.Subscribe(name, h);
                _subs.Add(h);
            }
        }

        private void OnEvent(EventData evt)
        {
            if (_index == 0)
                _first = DateTime.UtcNow;

            if (evt.Name == _sequence[_index])
            {
                _index++;
                if (_index >= _sequence.Count)
                {
                    var elapsed = (DateTime.UtcNow - _first).TotalMilliseconds;
                    if (elapsed <= _windowMs)
                        _callback();
                    _index = 0;
                }
            }
            else
            {
                // restart if current matches first
                _index = evt.Name == _sequence[0] ? 1 : 0;
                _first = DateTime.UtcNow;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _sequence.Count; i++)
                _bus.Unsubscribe(_sequence[i], _subs[i]);
        }
    }
}
