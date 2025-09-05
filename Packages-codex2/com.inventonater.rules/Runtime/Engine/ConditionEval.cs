using System;
using System.Collections.Generic;
using Inventonater.RulesEngine.Authoring;

namespace Inventonater.RulesEngine.Engine
{
    public static class ConditionEval
    {
        public static bool Evaluate(IEnumerable<ConditionDto> conditions, EntityStore store)
        {
            foreach (var c in conditions)
            {
                switch (c)
                {
                    case StateEqualsConditionDto eq:
                        var val = store.Get<string>(eq.Entity, string.Empty);
                        if (val != eq.Value) return false;
                        break;
                    case NumericCompareConditionDto num:
                        var n = store.Get<double>(num.Entity, 0);
                        if (num.Above.HasValue && !(n > num.Above.Value)) return false;
                        if (num.Below.HasValue && !(n < num.Below.Value)) return false;
                        break;
                    default:
                        throw new NotSupportedException($"Unknown condition type {c.Type}");
                }
            }
            return true;
        }
    }
}
