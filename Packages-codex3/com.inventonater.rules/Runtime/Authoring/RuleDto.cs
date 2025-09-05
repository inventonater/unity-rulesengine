using System.Collections.Generic;

namespace Inventonater.Rules.Authoring
{
    // DTOs that mirror the JSON schema used by the rules engine.
    public class RuleDto
    {
        public string id;
        public string mode; // optional: "single" or "restart"
        public List<TriggerDto> triggers;
        public List<ConditionDto> conditions;
        public List<ActionDto> actions;
    }

    public class TriggerDto
    {
        public string type; // event | numeric_threshold | time_schedule | pattern_sequence
        public string name; // for event triggers
        public string entity; // for numeric_threshold
        public string above;
        public string below;
        public int? for_ms;
        public int? every_ms_10_to_600000; // time_schedule
        public int? within_ms_10_to_5000; // pattern_sequence
        public List<PatternStepDto> sequence;
    }

    public class PatternStepDto
    {
        public string name;
    }

    public class ConditionDto
    {
        public string type; // state_equals | numeric_compare
        public string entity;
        public string equals;
        public string compare; // gt | lt | gte | lte
        public double? value;
    }

    public class ActionDto
    {
        public string type; // service_call | wait_duration | repeat_count | stop
        public string service; // for service_call
        public Dictionary<string, object> data; // service payload
        public int? duration_ms_0_to_60000; // wait_duration
        public int? count_1_to_20; // repeat_count
        public List<ActionDto> actions; // nested actions for repeat_count
    }
}
