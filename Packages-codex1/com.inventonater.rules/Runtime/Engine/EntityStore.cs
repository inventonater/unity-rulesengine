using System;
using System.Collections.Generic;

namespace Inventonater.Rules.Engine
{
    public class EntityStore
    {
        private readonly Dictionary<string, double> _numbers = new();
        private readonly Dictionary<string, string> _strings = new();

        public event Action<string, double> NumberChanged;
        public event Action<string, string> StringChanged;

        public void Set(string key, double value)
        {
            _numbers[key] = value;
            NumberChanged?.Invoke(key, value);
        }

        public void Set(string key, string value)
        {
            _strings[key] = value;
            StringChanged?.Invoke(key, value);
        }

        public bool TryGetNumber(string key, out double value) => _numbers.TryGetValue(key, out value);
        public bool TryGetString(string key, out string value) => _strings.TryGetValue(key, out value);
    }
}
