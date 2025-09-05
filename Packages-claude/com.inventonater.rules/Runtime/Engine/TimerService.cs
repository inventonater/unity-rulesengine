using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class TimerService : MonoBehaviour
    {
        private class TimerEntry
        {
            public int IntervalMs { get; set; }
            public float NextTriggerTime { get; set; }
        }

        private readonly Dictionary<int, TimerEntry> _timers = new Dictionary<int, TimerEntry>();
        private CancellationTokenSource _cts;

        private void OnEnable()
        {
            _cts = new CancellationTokenSource();
            RunTimerLoop(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void RegisterTimer(int intervalMs)
        {
            intervalMs = Mathf.Clamp(intervalMs, 10, 600000);
            
            if (!_timers.ContainsKey(intervalMs))
            {
                _timers[intervalMs] = new TimerEntry
                {
                    IntervalMs = intervalMs,
                    NextTriggerTime = Time.time + (intervalMs / 1000f)
                };
                
                Debug.Log($"[TimerService] Registered timer: {intervalMs}ms");
            }
        }

        public void ClearTimers()
        {
            _timers.Clear();
            Debug.Log("[TimerService] Cleared all timers");
        }

        private async UniTaskVoid RunTimerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                float currentTime = Time.time;
                
                foreach (var kvp in _timers)
                {
                    var timer = kvp.Value;
                    
                    if (currentTime >= timer.NextTriggerTime)
                    {
                        // Trigger the timer event
                        EventBus.Publish($"timer:{timer.IntervalMs}");
                        
                        // Schedule next trigger
                        timer.NextTriggerTime = currentTime + (timer.IntervalMs / 1000f);
                    }
                }
                
                // Check timers every frame
                await UniTask.Yield(ct);
            }
        }

        public void RefreshTimersFromRules(IEnumerable<RuleDto> rules)
        {
            ClearTimers();
            
            foreach (var rule in rules)
            {
                if (rule.triggers == null) continue;
                
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "time_schedule" && trigger.every_ms_10_to_600000 > 0)
                    {
                        RegisterTimer(trigger.every_ms_10_to_600000);
                    }
                }
            }
        }
    }
}