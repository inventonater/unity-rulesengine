using System.Collections.Generic;

namespace Inventonater.Rules
{
    public class RuleDto
    {
        public string id { get; set; }
        public string mode { get; set; } = "single";
        public List<TriggerDto> triggers { get; set; } = new();
        public List<ConditionDto> conditions { get; set; } = new();
        public List<ActionDto> actions { get; set; } = new();
    }

    public class TriggerDto
    {
        public string type { get; set; }
        public string name { get; set; }
        public int? every_ms_10_to_600000 { get; set; }
        public string entity { get; set; }
        public string above { get; set; }
        public string below { get; set; }
        public int within_ms_10_to_5000 { get; set; } = 2000;
        public List<SequenceStepDto> sequence { get; set; }
    }

    public class SequenceStepDto
    {
        public string name { get; set; }
    }

    public class ConditionDto
    {
        public string type { get; set; }
        public string entity { get; set; }
        public string @operator { get; set; }
        public string value { get; set; }
    }

    public class ActionDto
    {
        public string type { get; set; }
        public string sound { get; set; }
        public float volume_0_to_1 { get; set; } = 1.0f;
        public string entity { get; set; }
        public string value { get; set; }
        public int delay_ms { get; set; }
        public string message { get; set; }
        public string severity { get; set; } = "info";
    }
}