using System.Collections.Generic;

namespace Inventonater.Rules.Engine
{
    /// <summary>Simple entity store used by conditions and state.set service.</summary>
    public class EntityStore
    {
        private readonly Dictionary<string, object> _values = new();

        public void Set(string key, object value) => _values[key] = value;

        public T Get<T>(string key, T defaultValue = default)
        {
            if (_values.TryGetValue(key, out var obj) && obj is T t) return t;
            return defaultValue;
        }
    }
}
