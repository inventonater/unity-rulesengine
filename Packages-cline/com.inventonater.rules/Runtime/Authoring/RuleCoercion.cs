using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Normalizes aliases to canonical names during parse
    /// </summary>
    public static class RuleCoercion
    {
        public static void CoerceRule(RuleDto rule)
        {
            if (rule == null) return;
            
            if (rule.triggers != null)
            {
                foreach (var trigger in rule.triggers)
                {
                    CoerceTrigger(trigger);
                }
            }
            
            if (rule.conditions != null)
            {
                foreach (var condition in rule.conditions)
                {
                    CoerceCondition(condition);
                }
            }
            
            if (rule.actions != null)
            {
                foreach (var action in rule.actions)
                {
                    CoerceAction(action);
                }
            }
        }
        
        public static void CoerceTrigger(TriggerDto t)
        {
            if (t == null) return;
            
            // Normalize timer -> time_schedule
            if (t.type == "timer")
            {
                t.type = "time_schedule";
                if (t.every_ms_10_to_600000 == 0 && t.every_ms > 0)
                {
                    t.every_ms_10_to_600000 = Mathf.Clamp(t.every_ms, 10, 600000);
                }
            }
            
            // Normalize pattern -> pattern_sequence
            if (t.type == "pattern")
            {
                t.type = "pattern_sequence";
                if (t.within_ms_10_to_5000 == 0 && t.within_ms > 0)
                {
                    t.within_ms_10_to_5000 = Mathf.Clamp(t.within_ms, 10, 5000);
                }
                
                // Normalize event field to name in sequence steps
                if (t.sequence != null)
                {
                    foreach (var step in t.sequence)
                    {
                        if (string.IsNullOrEmpty(step.name) && !string.IsNullOrEmpty(step.@event))
                        {
                            step.name = step.@event;
                        }
                    }
                }
            }
            
            // Normalize value -> numeric_threshold
            if (t.type == "value" && !string.IsNullOrEmpty(t.path))
            {
                t.type = "numeric_threshold";
                if (t.entity == null || t.entity.Count == 0)
                {
                    t.entity = new List<string> { t.path };
                }
            }
            
            // Clamp numeric values
            if (t.type == "time_schedule")
            {
                t.every_ms_10_to_600000 = Mathf.Clamp(t.every_ms_10_to_600000, 10, 600000);
            }
            if (t.type == "pattern_sequence")
            {
                t.within_ms_10_to_5000 = Mathf.Clamp(t.within_ms_10_to_5000, 10, 5000);
            }
            if (t.type == "numeric_threshold")
            {
                t.for_ms_0_to_60000 = Mathf.Clamp(t.for_ms_0_to_60000, 0, 60000);
            }
        }
        
        public static void CoerceCondition(ConditionDto c)
        {
            // Currently no aliases for conditions
            // Future expansion point
        }
        
        public static void CoerceAction(ActionDto a)
        {
            if (a == null) return;
            
            // Clamp duration values
            if (a.type == "wait_duration")
            {
                a.duration_ms_0_to_60000 = Mathf.Clamp(a.duration_ms_0_to_60000, 0, 60000);
            }
            
            if (a.type == "repeat_count")
            {
                a.count_1_to_20 = Mathf.Clamp(a.count_1_to_20, 1, 20);
                
                // Recursively coerce nested actions
                if (a.actions != null)
                {
                    foreach (var nested in a.actions)
                    {
                        CoerceAction(nested);
                    }
                }
            }
        }
    }
}
