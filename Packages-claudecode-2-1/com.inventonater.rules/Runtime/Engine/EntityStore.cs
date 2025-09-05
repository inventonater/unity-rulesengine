using System;
using System.Collections.Generic;

namespace Inventonater.Rules
{
    public class EntityStore : IEntityStore
    {
        private readonly Dictionary<string, object> _data = new();
        public event Action<string, object> OnChanged;

        public T Get<T>(string key)
        {
            if (_data.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        public void Set(string key, object value)
        {
            var oldValue = _data.ContainsKey(key) ? _data[key] : null;
            _data[key] = value;
            
            if (!Equals(oldValue, value))
            {
                OnChanged?.Invoke(key, value);
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var objValue))
            {
                if (objValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                
                try
                {
                    value = (T)Convert.ChangeType(objValue, typeof(T));
                    return true;
                }
                catch
                {
                    value = default(T);
                    return false;
                }
            }
            
            value = default(T);
            return false;
        }

        public void Clear()
        {
            _data.Clear();
        }
    }
}