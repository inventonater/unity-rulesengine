using System.Collections.Generic;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public static class ConditionEval
    {
        public static bool Evaluate(IEnumerable<ConditionDto> conditions, EntityStore store)
        {
            foreach (var c in conditions)
            {
                switch (c.Type)
                {
                    case "state_equals":
                        if (store.TryGetString(c.Entity, out var s))
                        {
                            if (s != c.Value) return false;
                        }
                        else if (store.TryGetNumber(c.Entity, out var n))
                        {
                            if (!double.TryParse(c.Value, out var parsed) || n != parsed) return false;
                        }
                        else return false;
                        break;
                    case "numeric_compare":
                        if (!store.TryGetNumber(c.Entity, out var num)) return false;
                        var target = c.Number ?? 0;
                        switch (c.Op)
                        {
                            case ">": if (!(num > target)) return false; break;
                            case ">=": if (!(num >= target)) return false; break;
                            case "<": if (!(num < target)) return false; break;
                            case "<=": if (!(num <= target)) return false; break;
                            case "==": if (!(num == target)) return false; break;
                            default: return false;
                        }
                        break;
                }
            }
            return true;
        }
    }
}
