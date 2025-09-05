using System;
using System.Timers;

namespace Inventonater.Rules.Engine
{
    public class TimerService
    {
        public IDisposable Register(int intervalMs, Action callback)
        {
            var timer = new Timer(intervalMs);
            timer.Elapsed += (s, e) => callback();
            timer.AutoReset = true;
            timer.Start();
            return timer;
        }
    }
}
