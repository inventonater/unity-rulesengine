using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    public class PatternSequenceWatcher : IDisposable
    {
        private readonly List<string> _expectedSequence;
        private readonly int _withinMs;
        private readonly IEventBus _eventBus;
        private readonly List<IDisposable> _subscriptions = new();
        
        private int _currentIndex = 0;
        private float _sequenceStartTime = -1f;
        
        public event Action OnPatternCompleted;

        public PatternSequenceWatcher(List<string> sequence, int withinMs, IEventBus eventBus)
        {
            _expectedSequence = sequence ?? new List<string>();
            _withinMs = Mathf.Max(10, withinMs);
            _eventBus = eventBus;
            
            Initialize();
        }

        private void Initialize()
        {
            foreach (var eventName in _expectedSequence)
            {
                var subscription = _eventBus.Subscribe(eventName, () => OnEventFired(eventName));
                _subscriptions.Add(subscription);
            }
        }

        private void OnEventFired(string eventName)
        {
            if (_currentIndex >= _expectedSequence.Count) return;
            
            float currentTime = Time.time * 1000f;
            
            if (_currentIndex == 0)
            {
                if (_expectedSequence[0] == eventName)
                {
                    _sequenceStartTime = currentTime;
                    _currentIndex = 1;
                    
                    if (_expectedSequence.Count == 1)
                    {
                        CompletePattern();
                    }
                }
            }
            else
            {
                float elapsedMs = currentTime - _sequenceStartTime;
                
                if (elapsedMs > _withinMs)
                {
                    Reset();
                    
                    if (_expectedSequence[0] == eventName)
                    {
                        _sequenceStartTime = currentTime;
                        _currentIndex = 1;
                    }
                }
                else if (_expectedSequence[_currentIndex] == eventName)
                {
                    _currentIndex++;
                    
                    if (_currentIndex >= _expectedSequence.Count)
                    {
                        CompletePattern();
                    }
                }
                else if (_expectedSequence[0] == eventName)
                {
                    _sequenceStartTime = currentTime;
                    _currentIndex = 1;
                }
                else
                {
                    Reset();
                }
            }
        }

        private void CompletePattern()
        {
            Reset();
            
            try
            {
                OnPatternCompleted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Pattern completion handler error: {e}");
            }
        }

        private void Reset()
        {
            _currentIndex = 0;
            _sequenceStartTime = -1f;
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription?.Dispose();
            }
            _subscriptions.Clear();
            OnPatternCompleted = null;
        }
    }
}