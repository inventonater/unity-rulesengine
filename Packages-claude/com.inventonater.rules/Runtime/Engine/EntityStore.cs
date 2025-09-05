using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    public class EntityStore : MonoBehaviour
    {
        private readonly Dictionary<string, double> _numericValues = new Dictionary<string, double>();
        private readonly Dictionary<string, string> _stringValues = new Dictionary<string, string>();

        private void Start()
        {
            // Initialize some default values
            SetState("ui.mode", "normal");
        }

        public double GetNumeric(string path)
        {
            if (_numericValues.TryGetValue(path, out var value))
            {
                return value;
            }
            return 0.0;
        }

        public void SetNumeric(string path, double value)
        {
            var oldValue = GetNumeric(path);
            _numericValues[path] = value;
            
            if (oldValue != value)
            {
                Debug.Log($"[EntityStore] Numeric '{path}' changed: {oldValue:F2} → {value:F2}");
                
                // Check for threshold crossings
                CheckThresholdTriggers(path, oldValue, value);
            }
        }

        public string GetState(string path)
        {
            if (_stringValues.TryGetValue(path, out var value))
            {
                return value;
            }
            return "";
        }

        public void SetState(string path, string value)
        {
            var oldValue = GetState(path);
            _stringValues[path] = value ?? "";
            
            if (oldValue != value)
            {
                Debug.Log($"[EntityStore] State '{path}' changed: '{oldValue}' → '{value}'");
            }
        }

        private void CheckThresholdTriggers(string path, double oldValue, double newValue)
        {
            var repo = FindObjectOfType<RuleRepository>();
            if (repo == null) return;
            
            // Check for above threshold crossing
            foreach (var rule in repo.GetCandidatesFor($"num:above:{path}"))
            {
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "numeric_threshold" && 
                        trigger.entity?.Count > 0 && 
                        trigger.entity[0] == path &&
                        trigger.above != 0)
                    {
                        if (oldValue <= trigger.above && newValue > trigger.above)
                        {
                            Debug.Log($"[EntityStore] Threshold crossed above {trigger.above} for '{path}'");
                            
                            if (trigger.for_ms_0_to_60000 > 0)
                            {
                                // Schedule a delayed check
                                StartCoroutine(DelayedThresholdCheck(rule, trigger, path, newValue));
                            }
                            else
                            {
                                // Immediate trigger
                                EventBus.Publish($"threshold:{path}:above:{trigger.above}");
                            }
                        }
                    }
                }
            }
            
            // Check for below threshold crossing
            foreach (var rule in repo.GetCandidatesFor($"num:below:{path}"))
            {
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "numeric_threshold" && 
                        trigger.entity?.Count > 0 && 
                        trigger.entity[0] == path &&
                        trigger.below != 0)
                    {
                        if (oldValue >= trigger.below && newValue < trigger.below)
                        {
                            Debug.Log($"[EntityStore] Threshold crossed below {trigger.below} for '{path}'");
                            
                            if (trigger.for_ms_0_to_60000 > 0)
                            {
                                // Schedule a delayed check
                                StartCoroutine(DelayedThresholdCheck(rule, trigger, path, newValue));
                            }
                            else
                            {
                                // Immediate trigger
                                EventBus.Publish($"threshold:{path}:below:{trigger.below}");
                            }
                        }
                    }
                }
            }
        }

        private System.Collections.IEnumerator DelayedThresholdCheck(RuleDto rule, TriggerDto trigger, string path, double value)
        {
            float delay = trigger.for_ms_0_to_60000 / 1000f;
            yield return new WaitForSeconds(delay);
            
            // Check if still beyond threshold
            double currentValue = GetNumeric(path);
            bool stillTriggered = false;
            
            if (trigger.above != 0 && currentValue > trigger.above)
            {
                stillTriggered = true;
                EventBus.Publish($"threshold:{path}:above:{trigger.above}:sustained");
            }
            else if (trigger.below != 0 && currentValue < trigger.below)
            {
                stillTriggered = true;
                EventBus.Publish($"threshold:{path}:below:{trigger.below}:sustained");
            }
            
            if (stillTriggered)
            {
                Debug.Log($"[EntityStore] Sustained threshold for '{path}' after {delay}s");
            }
        }
    }
}