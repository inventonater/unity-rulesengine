using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    public struct EventData
    {
        public string Name { get; }
        public float Timestamp { get; }
        public Dictionary<string, object> Payload { get; }
        
        public EventData(string name, float timestamp = -1f, Dictionary<string, object> payload = null)
        {
            Name = name;
            Timestamp = timestamp >= 0 ? timestamp : Time.time;
            Payload = payload;
        }
    }
    
    /// <summary>
    /// Fixed event bus using traditional pub-sub pattern instead of Channels
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<string, List<Action<EventData>>> _handlers = new();
        private readonly object _lock = new();
        private static EventBus _instance;
        
        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EventBus();
                }
                return _instance;
            }
        }
        
        public void Publish(string eventName, Dictionary<string, object> payload = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            
            var data = new EventData(eventName, Time.time, payload);
            
            List<Action<EventData>> handlers = null;
            
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventName, out var list))
                {
                    // Create a copy to avoid modification during iteration
                    handlers = new List<Action<EventData>>(list);
                }
            }
            
            if (handlers != null)
            {
                Debug.Log($"[EventBus] Publishing: {eventName} at {data.Timestamp:F2} to {handlers.Count} handlers");
                
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler?.Invoke(data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error in event handler for {eventName}: {e}");
                    }
                }
            }
        }
        
        public IDisposable Subscribe(string eventName, Action<EventData> handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
                return new DisposableAction(() => { });
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventName, out var list))
                {
                    list = new List<Action<EventData>>();
                    _handlers[eventName] = list;
                }
                list.Add(handler);
                
                Debug.Log($"[EventBus] Subscribed to {eventName}. Total handlers: {list.Count}");
            }
            
            return new DisposableAction(() => Unsubscribe(eventName, handler));
        }
        
        private void Unsubscribe(string eventName, Action<EventData> handler)
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
                    
                    Debug.Log($"[EventBus] Unsubscribed from {eventName}. Remaining handlers: {list.Count}");
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
        
        public void Reset()
        {
            Clear();
        }
        
        private class DisposableAction : IDisposable
        {
            private Action _action;
            
            public DisposableAction(Action action)
            {
                _action = action;
            }
            
            public void Dispose()
            {
                _action?.Invoke();
                _action = null;
            }
        }
    }
}