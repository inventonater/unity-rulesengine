using System.Collections.Generic;

namespace Inventonater.Rules
{
    [System.Serializable]
    public class RuleDto
    {
        public string id;
        public string mode; // single, restart, queued, parallel
        public List<TriggerDto> triggers = new List<TriggerDto>();
        public List<ConditionDto> conditions = new List<ConditionDto>();
        public List<ActionDto> actions = new List<ActionDto>();
    }

    [System.Serializable]
    public class TriggerDto
    {
        public string type;
        public string name; // for event trigger
        
        // for numeric_threshold
        public List<string> entity;
        public double above;
        public double below;
        public int for_ms_0_to_60000;
        
        // for time_schedule
        public int every_ms_10_to_600000;
        
        // for pattern_sequence
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
        
        // for state_equals
        public List<string> entity;
        public List<string> equals;
        
        // for numeric_compare
        public string comparison; // "==", "!=", "<", ">", "<=", ">="
        public double value;
    }

    [System.Serializable]
    public class ActionDto
    {
        public string type;
        
        // for service_call
        public string service;
        public Dictionary<string, object> data;
        
        // for wait_duration
        public int duration_ms_0_to_60000;
        
        // for repeat_count
        public int count_1_to_20;
        public List<ActionDto> actions; // nested actions for repeat
    }
}
