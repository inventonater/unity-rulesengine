using System;
using System.Collections.Generic;

namespace Inventonater.Rules
{
    public class EventBus : IEventBus
    {
        private readonly Dictionary<string, List<Action>> _handlers = new();
        private readonly object _lock = new();

        public void Fire(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            
            List<Action> handlers = null;
            
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventName, out var list))
                {
                    handlers = new List<Action>(list);
                }
            }
            
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler?.Invoke();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Error in event handler for {eventName}: {e}");
                    }
                }
            }
        }

        public IDisposable Subscribe(string eventName, Action handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
                return new DisposableAction(() => { });
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventName, out var list))
                {
                    list = new List<Action>();
                    _handlers[eventName] = list;
                }
                list.Add(handler);
            }
            
            return new DisposableAction(() => Unsubscribe(eventName, handler));
        }

        private void Unsubscribe(string eventName, Action handler)
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventName, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _handlers.Remove(eventName);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }
    }
}