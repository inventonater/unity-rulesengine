using System;
using System.Collections.Generic;

namespace Inventonater.RulesEngine.Engine
{
    public class EventBus
    {
        private readonly Dictionary<string, Action> _listeners = new();

        public void Subscribe(string eventName, Action handler)
        {
            if (_listeners.ContainsKey(eventName))
                _listeners[eventName] += handler;
            else
                _listeners[eventName] = handler;
        }

        public void Unsubscribe(string eventName, Action handler)
        {
            if (_listeners.ContainsKey(eventName))
            {
                _listeners[eventName] -= handler;
                if (_listeners[eventName] == null)
                    _listeners.Remove(eventName);
            }
        }

        public void Publish(string eventName)
        {
            if (_listeners.TryGetValue(eventName, out var handler))
            {
                handler?.Invoke();
            }
        }
    }
}
