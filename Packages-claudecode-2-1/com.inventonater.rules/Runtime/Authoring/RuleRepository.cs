using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Inventonater.Rules
{
    public class RuleRepository : IRuleRepository
    {
        private readonly Dictionary<string, RuleDto> _rules = new();
        private readonly JsonSerializerSettings _jsonSettings;

        public RuleRepository()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include
            };
        }

        public IEnumerable<RuleDto> GetAllRules()
        {
            return _rules.Values;
        }

        public RuleDto GetRule(string id)
        {
            return _rules.TryGetValue(id, out var rule) ? rule : null;
        }

        public void LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            
            try
            {
                var rules = JsonConvert.DeserializeObject<List<RuleDto>>(json, _jsonSettings);
                if (rules != null)
                {
                    foreach (var rule in rules)
                    {
                        if (!string.IsNullOrEmpty(rule.id))
                        {
                            _rules[rule.id] = CoerceRule(rule);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load rules from JSON: {e}");
            }
        }

        public void LoadFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;
            
            var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    
                    var singleRule = JsonConvert.DeserializeObject<RuleDto>(json, _jsonSettings);
                    if (singleRule != null && !string.IsNullOrEmpty(singleRule.id))
                    {
                        _rules[singleRule.id] = CoerceRule(singleRule);
                        continue;
                    }
                    
                    var multipleRules = JsonConvert.DeserializeObject<List<RuleDto>>(json, _jsonSettings);
                    if (multipleRules != null)
                    {
                        foreach (var rule in multipleRules)
                        {
                            if (!string.IsNullOrEmpty(rule.id))
                            {
                                _rules[rule.id] = CoerceRule(rule);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load rule from {file}: {e}");
                }
            }
        }

        private RuleDto CoerceRule(RuleDto rule)
        {
            rule.mode = rule.mode ?? "single";
            rule.triggers = rule.triggers ?? new List<TriggerDto>();
            rule.conditions = rule.conditions ?? new List<ConditionDto>();
            rule.actions = rule.actions ?? new List<ActionDto>();
            
            foreach (var trigger in rule.triggers)
            {
                if (trigger.sequence != null)
                {
                    trigger.within_ms_10_to_5000 = Mathf.Clamp(trigger.within_ms_10_to_5000, 10, 5000);
                }
                
                if (trigger.every_ms_10_to_600000.HasValue)
                {
                    trigger.every_ms_10_to_600000 = Mathf.Clamp(trigger.every_ms_10_to_600000.Value, 10, 600000);
                }
            }
            
            foreach (var action in rule.actions)
            {
                action.volume_0_to_1 = Mathf.Clamp01(action.volume_0_to_1);
                action.severity = action.severity ?? "info";
            }
            
            return rule;
        }

        public void Clear()
        {
            _rules.Clear();
        }

        public void AddRule(RuleDto rule)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.id))
            {
                _rules[rule.id] = CoerceRule(rule);
            }
        }

        public bool RemoveRule(string id)
        {
            return _rules.Remove(id);
        }
    }
}