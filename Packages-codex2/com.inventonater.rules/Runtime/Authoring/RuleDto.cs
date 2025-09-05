using System.Collections.Generic;
using Newtonsoft.Json;

namespace Inventonater.RulesEngine.Authoring
{
    public class RuleDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("mode")] public string Mode { get; set; } = "single";
        [JsonProperty("triggers")] public List<TriggerDto> Triggers { get; set; } = new();
        [JsonProperty("conditions")] public List<ConditionDto> Conditions { get; set; } = new();
        [JsonProperty("actions")] public List<ActionDto> Actions { get; set; } = new();
    }

    public abstract class TriggerDto
    {
        [JsonProperty("type")] public string Type { get; set; }
    }

    public class EventTriggerDto : TriggerDto
    {
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class NumericThresholdTriggerDto : TriggerDto
    {
        [JsonProperty("entity")] public string Entity { get; set; }
        [JsonProperty("above")] public double? Above { get; set; }
        [JsonProperty("below")] public double? Below { get; set; }
        [JsonProperty("for_ms")] public int? ForMs { get; set; }
    }

    public class TimeScheduleTriggerDto : TriggerDto
    {
        [JsonProperty("every_ms_10_to_600000")] public int EveryMs { get; set; }
    }

    public class PatternSequenceTriggerDto : TriggerDto
    {
        [JsonProperty("within_ms_10_to_5000")] public int WithinMs { get; set; }
        [JsonProperty("sequence")] public List<SequenceItemDto> Sequence { get; set; } = new();
    }

    public class SequenceItemDto
    {
        [JsonProperty("name")] public string Name { get; set; }
    }

    public abstract class ConditionDto
    {
        [JsonProperty("type")] public string Type { get; set; }
    }

    public class StateEqualsConditionDto : ConditionDto
    {
        [JsonProperty("entity")] public string Entity { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
    }

    public class NumericCompareConditionDto : ConditionDto
    {
        [JsonProperty("entity")] public string Entity { get; set; }
        [JsonProperty("above")] public double? Above { get; set; }
        [JsonProperty("below")] public double? Below { get; set; }
    }

    public abstract class ActionDto
    {
        [JsonProperty("type")] public string Type { get; set; }
    }

    public class ServiceCallActionDto : ActionDto
    {
        [JsonProperty("service")] public string Service { get; set; }
        [JsonProperty("data")] public Dictionary<string, object> Data { get; set; } = new();
    }

    public class WaitDurationActionDto : ActionDto
    {
        [JsonProperty("duration_ms_0_to_60000")] public int DurationMs { get; set; }
    }

    public class RepeatCountActionDto : ActionDto
    {
        [JsonProperty("count_1_to_20")] public int Count { get; set; }
        [JsonProperty("actions")] public List<ActionDto> Actions { get; set; } = new();
    }

    public class StopActionDto : ActionDto
    {
    }
}
