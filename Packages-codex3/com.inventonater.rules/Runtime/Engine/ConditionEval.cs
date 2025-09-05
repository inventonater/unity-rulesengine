using System;
using System.Collections.Generic;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public class ConditionEval
    {
        private readonly EntityStore _store;

        public ConditionEval(EntityStore store) => _store = store;

        public bool Evaluate(List<ConditionDto> conditions)
        {
            if (conditions == null || conditions.Count == 0) return true;
            foreach (var c in conditions)
            {
                if (!Evaluate(c)) return false;
            }
            return true;
        }

        private bool Evaluate(ConditionDto c)
        {
            switch (c.type)
            {
                case "state_equals":
                    var val = _store.Get<object>(c.entity);
                    return val != null && val.ToString() == c.equals;
                case "numeric_compare":
                    var num = _store.Get<double>(c.entity);
                    return c.compare switch
                    {
                        "gt" => num > c.value,
                        "lt" => num < c.value,
                        "gte" => num >= c.value,
                        "lte" => num <= c.value,
                        _ => false
                    };
                default:
                    return false;
            }
        }
    }
}
