using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Manages scheduled/periodic triggers
    /// </summary>
    public class TimerService : MonoBehaviour
    {
        private class TimerEntry
        {
            public string EventName { get; set; }
            public float IntervalMs { get; set; }
            public float NextFireTime { get; set; }
            public CancellationTokenSource Cts { get; set; }
        }
        
        private readonly Dictionary<string, TimerEntry> _timers = new Dictionary<string, TimerEntry>();
        private CancellationTokenSource _mainCts;
        
        private void Awake()
        {
            _mainCts = new CancellationTokenSource();
        }
        
        private void OnDestroy()
        {
            StopAllTimers();
            _mainCts?.Cancel();
            _mainCts?.Dispose();
        }
        
        public void RegisterTimer(string eventName, int intervalMs)
        {
            if (intervalMs <= 0) return;
            
            // Stop existing timer if any
            if (_timers.TryGetValue(eventName, out var existing))
            {
                existing.Cts?.Cancel();
            }
            
            var entry = new TimerEntry
            {
                EventName = eventName,
                IntervalMs = intervalMs,
                NextFireTime = Time.time + (intervalMs / 1000f),
                Cts = CancellationTokenSource.CreateLinkedTokenSource(_mainCts.Token)
            };
            
            _timers[eventName] = entry;
            
            // Start the timer loop
            RunTimerLoop(entry).Forget();
            
            Debug.Log($"[TimerService] Registered timer: {eventName} every {intervalMs}ms");
        }
        
        private async UniTaskVoid RunTimerLoop(TimerEntry entry)
        {
            try
            {
                while (!entry.Cts.Token.IsCancellationRequested)
                {
                    await UniTask.Delay((int)entry.IntervalMs, cancellationToken: entry.Cts.Token);
                    
                    if (!entry.Cts.Token.IsCancellationRequested)
                    {
                        EventBus.Publish(entry.EventName);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // Timer was cancelled, this is expected
            }
        }
        
        public void UnregisterTimer(string eventName)
        {
            if (_timers.TryGetValue(eventName, out var entry))
            {
                entry.Cts?.Cancel();
                _timers.Remove(eventName);
                Debug.Log($"[TimerService] Unregistered timer: {eventName}");
            }
        }
        
        public void StopAllTimers()
        {
            foreach (var entry in _timers.Values)
            {
                entry.Cts?.Cancel();
            }
            _timers.Clear();
        }
        
        /// <summary>
        /// Process all time_schedule triggers from rules
        /// </summary>
        public void ProcessScheduleTriggers(IEnumerable<RuleDto> rules)
        {
            StopAllTimers();
            
            var processedTimers = new HashSet<string>();
            
            foreach (var rule in rules)
            {
                if (rule.triggers == null) continue;
                
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type != "time_schedule") continue;
                    
                    var intervalMs = trigger.every_ms_10_to_600000;
                    if (intervalMs <= 0) continue;
                    
                    var timerKey = $"timer:{intervalMs}";
                    
                    if (!processedTimers.Contains(timerKey))
                    {
                        processedTimers.Add(timerKey);
                        RegisterTimer($"time:{intervalMs}", intervalMs);
                    }
                }
            }
        }
    }
}
