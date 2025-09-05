using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    public static class RuleCoercion
    {
        public static void CoerceRule(RuleDto rule)
        {
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
            // Handle timer -> time_schedule alias
            if (t.type == "timer")
            {
                t.type = "time_schedule";
                if (t.every_ms_10_to_600000 == 0 && t.every_ms > 0)
                {
                    t.every_ms_10_to_600000 = Mathf.Clamp(t.every_ms, 10, 600000);
                }
            }
            
            // Handle pattern -> pattern_sequence alias
            if (t.type == "pattern")
            {
                t.type = "pattern_sequence";
                if (t.within_ms_10_to_5000 == 0 && t.within_ms > 0)
                {
                    t.within_ms_10_to_5000 = Mathf.Clamp(t.within_ms, 10, 5000);
                }
                
                // Convert event field to name field in sequence steps
                if (t.sequence != null)
                {
                    foreach (var s in t.sequence)
                    {
                        if (string.IsNullOrEmpty(s.name) && !string.IsNullOrEmpty(s.@event))
                        {
                            s.name = s.@event;
                        }
                    }
                }
            }
            
            // Handle value -> numeric_threshold alias
            if (t.type == "value" && !string.IsNullOrEmpty(t.path))
            {
                t.type = "numeric_threshold";
                if (t.entity == null || t.entity.Count == 0)
                {
                    t.entity = new List<string> { t.path };
                }
            }
            
            // Ensure entity is always a list
            if (t.type == "numeric_threshold" && t.entity == null)
            {
                t.entity = new List<string>();
            }
            
            // Clamp numeric values
            if (t.for_ms_0_to_60000 > 0)
            {
                t.for_ms_0_to_60000 = Mathf.Clamp(t.for_ms_0_to_60000, 0, 60000);
            }
            
            if (t.every_ms_10_to_600000 > 0)
            {
                t.every_ms_10_to_600000 = Mathf.Clamp(t.every_ms_10_to_600000, 10, 600000);
            }
            
            if (t.within_ms_10_to_5000 > 0)
            {
                t.within_ms_10_to_5000 = Mathf.Clamp(t.within_ms_10_to_5000, 10, 5000);
            }
        }

        public static void CoerceCondition(ConditionDto c)
        {
            // Ensure entity and equals are always lists
            if (c.entity == null)
            {
                c.entity = new List<string>();
            }
            
            if (c.type == "state_equals" && c.equals == null)
            {
                c.equals = new List<string>();
            }
        }

        public static void CoerceAction(ActionDto a)
        {
            // Clamp duration values
            if (a.duration_ms_0_to_60000 > 0)
            {
                a.duration_ms_0_to_60000 = Mathf.Clamp(a.duration_ms_0_to_60000, 0, 60000);
            }
            
            // Clamp count values
            if (a.count_1_to_20 > 0)
            {
                a.count_1_to_20 = Mathf.Clamp(a.count_1_to_20, 1, 20);
            }
            
            // Recursively coerce nested actions
            if (a.actions != null)
            {
                foreach (var nested in a.actions)
                {
                    CoerceAction(nested);
                }
            }
            
            // Ensure data dictionary exists for service_call
            if (a.type == "service_call" && a.data == null)
            {
                a.data = new Dictionary<string, object>();
            }
        }
    }
}