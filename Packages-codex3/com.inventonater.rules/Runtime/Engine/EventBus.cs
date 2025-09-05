using System;
using System.Collections.Generic;

namespace Inventonater.Rules.Engine
{
    /// <summary>Simple in-memory event bus for string event names.</summary>
    public class EventBus
    {
        private readonly Dictionary<string, List<Action>> _listeners = new();

        public void Emit(string name)
        {
            if (_listeners.TryGetValue(name, out var list))
            {
                foreach (var cb in list.ToArray()) cb();
            }
        }

        public IDisposable Subscribe(string name, Action callback)
        {
            if (!_listeners.TryGetValue(name, out var list))
            {
                list = new List<Action>();
                _listeners[name] = list;
            }
            list.Add(callback);
            return new Subscription(() => list.Remove(callback));
        }

        private class Subscription : IDisposable
        {
            private readonly Action _onDispose;
            public Subscription(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
