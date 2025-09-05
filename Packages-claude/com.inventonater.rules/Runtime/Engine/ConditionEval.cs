using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    public static class ConditionEval
    {
        public static bool EvaluateAll(IEnumerable<ConditionDto> conditions, EntityStore store)
        {
            if (conditions == null) return true;
            
            foreach (var condition in conditions)
            {
                if (!EvaluateOne(condition, store))
                {
                    return false;
                }
            }
            
            return true;
        }

        private static bool EvaluateOne(ConditionDto condition, EntityStore store)
        {
            if (store == null)
            {
                Debug.LogWarning("EntityStore is null, condition evaluation failed");
                return false;
            }

            switch (condition.type)
            {
                case "state_equals":
                    return EvaluateStateEquals(condition, store);
                    
                case "numeric_compare":
                    return EvaluateNumericCompare(condition, store);
                    
                default:
                    Debug.LogWarning($"Unknown condition type: {condition.type}");
                    return true; // Unknown conditions pass by default
            }
        }

        private static bool EvaluateStateEquals(ConditionDto condition, EntityStore store)
        {
            if (condition.entity == null || condition.entity.Count == 0)
            {
                Debug.LogWarning("state_equals condition missing entity");
                return false;
            }
            
            if (condition.equals == null || condition.equals.Count == 0)
            {
                Debug.LogWarning("state_equals condition missing equals values");
                return false;
            }
            
            string path = condition.entity[0];
            string currentValue = store.GetState(path);
            
            // Check if current value matches any of the expected values
            foreach (var expectedValue in condition.equals)
            {
                if (currentValue == expectedValue)
                {
                    return true;
                }
            }
            
            return false;
        }

        private static bool EvaluateNumericCompare(ConditionDto condition, EntityStore store)
        {
            if (condition.entity == null || condition.entity.Count == 0)
            {
                Debug.LogWarning("numeric_compare condition missing entity");
                return false;
            }
            
            string path = condition.entity[0];
            double currentValue = store.GetNumeric(path);
            double compareValue = condition.value;
            
            string op = condition.@operator ?? "==";
            
            return op switch
            {
                "==" => currentValue == compareValue,
                "!=" => currentValue != compareValue,
                ">" => currentValue > compareValue,
                ">=" => currentValue >= compareValue,
                "<" => currentValue < compareValue,
                "<=" => currentValue <= compareValue,
                _ => {
                    Debug.LogWarning($"Unknown operator: {op}");
                    return false;
                }
            };
        }
    }
}