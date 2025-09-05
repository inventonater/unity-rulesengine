using System;
using System.Collections.Generic;
using System.Linq;

namespace Inventonater.Rules
{
    public class ConditionEvaluator : IConditionEvaluator
    {
        private readonly IEntityStore _store;

        public ConditionEvaluator(IEntityStore store)
        {
            _store = store;
        }

        public bool Evaluate(List<ConditionDto> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return true;
            
            return conditions.All(EvaluateSingle);
        }

        private bool EvaluateSingle(ConditionDto condition)
        {
            if (condition == null) return true;
            
            switch (condition.type)
            {
                case "comparison":
                    return EvaluateComparison(condition);
                
                case "existence":
                    return EvaluateExistence(condition);
                
                default:
                    return true;
            }
        }

        private bool EvaluateComparison(ConditionDto condition)
        {
            if (string.IsNullOrEmpty(condition.entity)) return false;
            
            var storedValue = _store.Get<object>(condition.entity);
            if (storedValue == null) return false;
            
            var op = condition.@operator ?? "equals";
            var targetValue = condition.value;
            
            if (TryCompareNumeric(storedValue, targetValue, op, out bool numericResult))
            {
                return numericResult;
            }
            
            return CompareStrings(storedValue.ToString(), targetValue, op);
        }

        private bool TryCompareNumeric(object stored, string target, string op, out bool result)
        {
            result = false;
            
            if (!double.TryParse(stored.ToString(), out double storedNum))
                return false;
            
            if (!double.TryParse(target, out double targetNum))
                return false;
            
            result = op switch
            {
                "equals" => Math.Abs(storedNum - targetNum) < 0.0001,
                "not_equals" => Math.Abs(storedNum - targetNum) >= 0.0001,
                "greater_than" => storedNum > targetNum,
                "less_than" => storedNum < targetNum,
                "greater_or_equal" => storedNum >= targetNum,
                "less_or_equal" => storedNum <= targetNum,
                _ => storedNum == targetNum
            };
            
            return true;
        }

        private bool CompareStrings(string stored, string target, string op)
        {
            return op switch
            {
                "equals" => stored == target,
                "not_equals" => stored != target,
                "contains" => stored.Contains(target),
                "starts_with" => stored.StartsWith(target),
                "ends_with" => stored.EndsWith(target),
                _ => stored == target
            };
        }

        private bool EvaluateExistence(ConditionDto condition)
        {
            if (string.IsNullOrEmpty(condition.entity)) return false;
            
            return _store.TryGet<object>(condition.entity, out var value) && value != null;
        }
    }
}