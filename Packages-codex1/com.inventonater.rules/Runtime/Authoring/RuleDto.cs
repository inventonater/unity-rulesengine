using System.Collections.Generic;
using Newtonsoft.Json;

namespace Inventonater.Rules.Authoring
{
    // Root rule DTO
    public class RuleDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("mode")] public string Mode { get; set; } = "single";
        [JsonProperty("triggers")] public List<TriggerDto> Triggers { get; set; } = new();
        [JsonProperty("conditions")] public List<ConditionDto> Conditions { get; set; } = new();
        [JsonProperty("actions")] public List<ActionDto> Actions { get; set; } = new();
    }

    // Trigger DTOs
    public class TriggerDto
    {
        [JsonProperty("type")] public string Type { get; set; }

        // event
        [JsonProperty("name")] public string Name { get; set; }

        // numeric_threshold
        [JsonProperty("entity")] public string Entity { get; set; }
        [JsonProperty("above")] public double? Above { get; set; }
        [JsonProperty("below")] public double? Below { get; set; }
        [JsonProperty("for_ms")] public int? ForMs { get; set; }

        // time_schedule
        [JsonProperty("every_ms_10_to_600000")] public int? EveryMs { get; set; }

        // pattern_sequence
        [JsonProperty("within_ms_10_to_5000")] public int? WithinMs { get; set; }
        [JsonProperty("sequence")] public List<PatternEventDto> Sequence { get; set; }
    }

    public class PatternEventDto
    {
        [JsonProperty("name")] public string Name { get; set; }
    }

    // Conditions
    public class ConditionDto
    {
        [JsonProperty("type")] public string Type { get; set; }

        // state_equals
        [JsonProperty("entity")] public string Entity { get; set; }
        [JsonProperty("value")] public string Value { get; set; }

        // numeric_compare
        [JsonProperty("op")] public string Op { get; set; }
        [JsonProperty("number")] public double? Number { get; set; }
    }

    // Actions
    public class ActionDto
    {
        [JsonProperty("type")] public string Type { get; set; }

        // service_call
        [JsonProperty("service")] public string Service { get; set; }
        [JsonProperty("data")] public Dictionary<string, object> Data { get; set; }

        // wait_duration
        [JsonProperty("duration_ms_0_to_60000")] public int? DurationMs { get; set; }

        // repeat_count
        [JsonProperty("count_1_to_20")] public int? Count { get; set; }
        [JsonProperty("actions")] public List<ActionDto> Actions { get; set; }
    }
}
