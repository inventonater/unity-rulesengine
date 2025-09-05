using System;
using System.Collections.Generic;

namespace Inventonater.Rules.Engine
{
    public class EventData
    {
        public string Name;
        public object Payload;
        public EventData(string name, object payload = null)
        {
            Name = name; Payload = payload;
        }
    }

    public class EventBus
    {
        private readonly Dictionary<string, List<Action<EventData>>> _handlers = new();

        public void Subscribe(string name, Action<EventData> handler)
        {
            if (!_handlers.TryGetValue(name, out var list))
            {
                list = new List<Action<EventData>>();
                _handlers[name] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe(string name, Action<EventData> handler)
        {
            if (_handlers.TryGetValue(name, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0) _handlers.Remove(name);
            }
        }

        public void Emit(EventData evt)
        {
            if (_handlers.TryGetValue(evt.Name, out var list))
            {
                foreach (var h in list.ToArray())
                {
                    h(evt);
                }
            }
        }
    }
}
