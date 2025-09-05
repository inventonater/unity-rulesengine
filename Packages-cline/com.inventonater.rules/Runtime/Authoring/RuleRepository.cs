using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

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
            
            if (rules == null) return;
            
            foreach (var r in rules)
            {
                // Apply coercion to normalize aliases
                RuleCoercion.CoerceRule(r);
                
                _allRules.Add(r);
                
                if (r.triggers == null) continue;
                
                foreach (var t in r.triggers)
                {
                    // Pattern rules are tracked separately
                    if (t.type == "pattern_sequence")
                    {
                        _patternRules.Add(r);
                        continue;
                    }
                    
                    var key = GetTriggerKey(t);
                    if (key == null)
                    {
                        Debug.LogWarning($"Unknown trigger type: {t.type} in rule {r.id}");
                        continue;
                    }
                    
                    if (!_byTrigger.TryGetValue(key, out var list))
                    {
                        list = new List<RuleDto>();
                        _byTrigger[key] = list;
                    }
                    list.Add(r);
                }
            }
            
            Debug.Log($"Loaded {_allRules.Count} rules, {_patternRules.Count} with patterns");
        }
        
        public IEnumerable<RuleDto> GetCandidatesFor(string triggerKey)
        {
            if (_byTrigger.TryGetValue(triggerKey, out var list))
            {
                return list;
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
        
        private string GetTriggerKey(TriggerDto t)
        {
            return t.type switch
            {
                "event" => $"event:{t.name}",
                "numeric_threshold" => $"num:{(t.above != 0 ? "above" : "below")}:{t.entity?[0]}",
                "time_schedule" => $"time:{t.every_ms_10_to_600000}",
                _ => null
            };
        }
        
        public void LoadFromJson(string json)
        {
            try
            {
                var rules = JsonConvert.DeserializeObject<List<RuleDto>>(json);
                if (rules != null)
                {
                    ReplaceAll(rules);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse rules JSON: {e.Message}");
            }
        }
        
        public void LoadSingleRuleFromJson(string json)
        {
            try
            {
                var rule = JsonConvert.DeserializeObject<RuleDto>(json);
                if (rule != null)
                {
                    var currentRules = _allRules.ToList();
                    currentRules.Add(rule);
                    ReplaceAll(currentRules);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse rule JSON: {e.Message}");
            }
        }
    }
}
