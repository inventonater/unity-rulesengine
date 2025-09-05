using System;
using System.Timers;

namespace Inventonater.RulesEngine.Engine
{
    public class TimerService : IDisposable
    {
        private readonly Timer _timer;
        private readonly Action _callback;

        public TimerService(int intervalMs, Action callback)
        {
            _callback = callback;
            _timer = new Timer(intervalMs);
            _timer.AutoReset = true;
            _timer.Elapsed += (_, __) => _callback();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
        public void Dispose() => _timer.Dispose();
    }
}
