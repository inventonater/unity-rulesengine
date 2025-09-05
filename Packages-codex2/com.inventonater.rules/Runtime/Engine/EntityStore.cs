using System;
using System.Collections.Generic;

namespace Inventonater.RulesEngine.Engine
{
    public class EntityStore
    {
        private readonly Dictionary<string, object> _values = new();
        public event Action<string, object>? OnChanged;

        public void Set(string key, object value)
        {
            _values[key] = value;
            OnChanged?.Invoke(key, value);
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_values.TryGetValue(key, out var v) && v is T t)
                return t;
            return defaultValue;
        }
    }
}
