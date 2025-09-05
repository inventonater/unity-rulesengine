using System.Collections.Generic;

namespace Inventonater.Rules
{
    [System.Serializable]
    public class RuleDto
    {
        public string id;
        public string mode;
        public List<TriggerDto> triggers = new List<TriggerDto>();
        public List<ConditionDto> conditions = new List<ConditionDto>();
        public List<ActionDto> actions = new List<ActionDto>();
    }

    [System.Serializable]
    public class TriggerDto
    {
        public string type;
        public string name;
        public List<string> entity;
        public double above;
        public double below;
        public int for_ms_0_to_60000;
        public int every_ms_10_to_600000;
        
        // pattern_sequence
        public int within_ms_10_to_5000;
        public List<PatternStep> sequence;
        
        // aliases (loader only)
        public int every_ms;
        public int within_ms;
        public string path;
    }

    [System.Serializable]
    public class PatternStep
    {
        public string name;
        public string @event; // "@event" alias accepted
    }

    [System.Serializable]
    public class ConditionDto
    {
        public string type;
        public List<string> entity;
        public List<string> equals;
        public string @operator;
        public double value;
    }

    [System.Serializable]
    public class ActionDto
    {
        public string type;
        public string service;
        public Dictionary<string, object> data;
        public int duration_ms_0_to_60000;
        public int count_1_to_20;
        public List<ActionDto> actions;
    }
}