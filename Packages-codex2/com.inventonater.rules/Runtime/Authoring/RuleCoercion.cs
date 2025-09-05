using Newtonsoft.Json.Linq;

namespace Inventonater.RulesEngine.Authoring
{
    /// <summary>
    /// Normalizes legacy or short-form trigger names to canonical ones.
    /// </summary>
    public static class RuleCoercion
    {
        public static void NormalizeTrigger(JObject trigger)
        {
            var type = (string?)trigger["type"];
            if (type == null) return;
            switch (type)
            {
                case "timer":
                    trigger["type"] = "time_schedule";
                    if (trigger["every_ms"] != null)
                    {
                        trigger["every_ms_10_to_600000"] = trigger["every_ms"];
                        trigger.Remove("every_ms");
                    }
                    break;
                case "value":
                    trigger["type"] = "numeric_threshold";
                    break;
                case "pattern":
                    trigger["type"] = "pattern_sequence";
                    if (trigger["within_ms"] != null)
                    {
                        trigger["within_ms_10_to_5000"] = trigger["within_ms"];
                        trigger.Remove("within_ms");
                    }
                    break;
            }
        }
    }
}
