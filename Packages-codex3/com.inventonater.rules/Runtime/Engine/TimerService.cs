using System;
using System.Collections.Generic;
using System.Timers;

namespace Inventonater.Rules.Engine
{
    public class TimerService : IDisposable
    {
        private readonly List<Timer> _timers = new();

        public void Every(int intervalMs, Action callback)
        {
            var timer = new Timer(intervalMs);
            timer.Elapsed += (_, __) => callback();
            timer.AutoReset = true;
            timer.Start();
            _timers.Add(timer);
        }

        public void Dispose()
        {
            foreach (var t in _timers) t.Dispose();
            _timers.Clear();
        }
    }
}
