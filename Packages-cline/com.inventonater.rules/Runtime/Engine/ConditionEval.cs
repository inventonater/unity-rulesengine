using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Evaluates rule conditions against the current entity state
    /// </summary>
    public class ConditionEval
    {
        private readonly EntityStore _store;
        
        public ConditionEval(EntityStore store)
        {
            _store = store;
        }
        
        public bool EvaluateAll(List<ConditionDto> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return true; // No conditions means always true
            
            return conditions.All(c => Evaluate(c));
        }
        
        private bool Evaluate(ConditionDto condition)
        {
            if (condition == null) return true;
            
            switch (condition.type)
            {
                case "state_equals":
                    return EvaluateStateEquals(condition);
                    
                case "numeric_compare":
                    return EvaluateNumericCompare(condition);
                    
                default:
                    Debug.LogWarning($"Unknown condition type: {condition.type}");
                    return false;
            }
        }
        
        private bool EvaluateStateEquals(ConditionDto condition)
        {
            if (condition.entity == null || condition.entity.Count == 0)
                return false;
            
            var entityKey = condition.entity[0];
            var currentValue = _store.GetState(entityKey);
            
            if (condition.equals == null || condition.equals.Count == 0)
                return false;
            
            // Check if current value matches any of the expected values
            foreach (var expected in condition.equals)
            {
                if (currentValue == expected)
                    return true;
            }
            
            return false;
        }
        
        private bool EvaluateNumericCompare(ConditionDto condition)
        {
            if (condition.entity == null || condition.entity.Count == 0)
                return false;
            
            var entityKey = condition.entity[0];
            var currentValue = _store.GetNumeric(entityKey);
            
            switch (condition.comparison)
            {
                case "==":
                    return System.Math.Abs(currentValue - condition.value) < 0.001;
                case "!=":
                    return System.Math.Abs(currentValue - condition.value) >= 0.001;
                case "<":
                    return currentValue < condition.value;
                case ">":
                    return currentValue > condition.value;
                case "<=":
                    return currentValue <= condition.value;
                case ">=":
                    return currentValue >= condition.value;
                default:
                    Debug.LogWarning($"Unknown comparison operator: {condition.comparison}");
                    return false;
            }
        }
    }
}
