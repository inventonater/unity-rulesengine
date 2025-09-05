using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Inventonater.Rules
{
    public interface IRuleRepository
    {
        void ReplaceAll(IEnumerable<RuleDto> rules);
        IEnumerable<RuleDto> GetCandidatesFor(string triggerKey);
        IEnumerable<RuleDto> GetPatternRules();
        IEnumerable<RuleDto> GetAllRules();
    }

    public class RuleRepository : MonoBehaviour, IRuleRepository
    {
        private readonly Dictionary<string, List<RuleDto>> _byTrigger = new Dictionary<string, List<RuleDto>>();
        private readonly List<RuleDto> _patternRules = new List<RuleDto>();
        private readonly List<RuleDto> _allRules = new List<RuleDto>();

        public void ReplaceAll(IEnumerable<RuleDto> rules)
        {
            _byTrigger.Clear();
            _patternRules.Clear();
            _allRules.Clear();
            
            foreach (var rule in rules)
            {
                // Apply coercion to normalize aliases
                RuleCoercion.CoerceRule(rule);
                
                _allRules.Add(rule);
                
                if (rule.triggers == null || rule.triggers.Count == 0)
                {
                    Debug.LogWarning($"Rule '{rule.id}' has no triggers");
                    continue;
                }
                
                foreach (var trigger in rule.triggers)
                {
                    if (trigger.type == "pattern_sequence")
                    {
                        _patternRules.Add(rule);
                        continue;
                    }
                    
                    string key = GetTriggerKey(trigger);
                    if (key == null)
                    {
                        Debug.LogWarning($"Unknown trigger type '{trigger.type}' in rule '{rule.id}'");
                        continue;
                    }
                    
                    if (!_byTrigger.TryGetValue(key, out var list))
                    {
                        list = new List<RuleDto>();
                        _byTrigger[key] = list;
                    }
                    list.Add(rule);
                }
            }
            
            Debug.Log($"Repository loaded: {_allRules.Count} rules, {_byTrigger.Count} trigger keys, {_patternRules.Count} pattern rules");
        }

        public IEnumerable<RuleDto> GetCandidatesFor(string triggerKey)
        {
            if (_byTrigger.TryGetValue(triggerKey, out var rules))
            {
                return rules;
            }
            return Enumerable.Empty<RuleDto>();
        }

        public IEnumerable<RuleDto> GetPatternRules()
        {
            return _patternRules;
        }

        public IEnumerable<RuleDto> GetAllRules()
        {
            return _allRules;
        }

        private string GetTriggerKey(TriggerDto trigger)
        {
            return trigger.type switch
            {
                "event" => $"event:{trigger.name}",
                "numeric_threshold" when trigger.above != 0 => $"num:above:{(trigger.entity?.Count > 0 ? trigger.entity[0] : "")}",
                "numeric_threshold" when trigger.below != 0 => $"num:below:{(trigger.entity?.Count > 0 ? trigger.entity[0] : "")}",
                "time_schedule" => $"time:{trigger.every_ms_10_to_600000}",
                _ => null
            };
        }
    }
}