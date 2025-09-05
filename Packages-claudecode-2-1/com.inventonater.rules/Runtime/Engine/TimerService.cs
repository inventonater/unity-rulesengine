using System;
using UnityEngine;

namespace Inventonater.Rules
{
    public class TimerService : IDisposable
    {
        private readonly int _intervalMs;
        private readonly Action _callback;
        private float _lastTriggerTime;
        private bool _isRunning;
        private GameObject _timerObject;
        private TimerComponent _timerComponent;

        public TimerService(int intervalMs, Action callback)
        {
            _intervalMs = Mathf.Max(10, intervalMs);
            _callback = callback;
        }

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _lastTriggerTime = Time.time;
            
            _timerObject = new GameObject($"Timer_{_intervalMs}ms");
            GameObject.DontDestroyOnLoad(_timerObject);
            _timerComponent = _timerObject.AddComponent<TimerComponent>();
            _timerComponent.Initialize(this);
        }

        public void Stop()
        {
            _isRunning = false;
            
            if (_timerObject != null)
            {
                GameObject.Destroy(_timerObject);
                _timerObject = null;
                _timerComponent = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Update()
        {
            if (!_isRunning) return;
            
            float currentTime = Time.time;
            float elapsed = (currentTime - _lastTriggerTime) * 1000f;
            
            if (elapsed >= _intervalMs)
            {
                _lastTriggerTime = currentTime;
                try
                {
                    _callback?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Timer callback error: {e}");
                }
            }
        }

        private class TimerComponent : MonoBehaviour
        {
            private TimerService _service;
            
            public void Initialize(TimerService service)
            {
                _service = service;
            }
            
            private void Update()
            {
                _service?.Update();
            }
            
            private void OnDestroy()
            {
                _service = null;
            }
        }
    }
}