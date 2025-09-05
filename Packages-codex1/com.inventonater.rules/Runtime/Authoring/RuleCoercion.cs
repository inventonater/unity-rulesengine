using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Inventonater.Rules.Authoring
{
    public static class RuleCoercion
    {
        // Normalize aliases and simple coercions
        public static void Normalize(RuleDto rule)
        {
            foreach (var t in rule.Triggers)
            {
                switch (t.Type)
                {
                    case "timer":
                        t.Type = "time_schedule";
                        break;
                    case "value":
                        t.Type = "numeric_threshold";
                        break;
                    case "pattern":
                        t.Type = "pattern_sequence";
                        break;
                }
            }
        }

        // Coerce generic object to dictionary for action data
        public static Dictionary<string, object> CoerceData(JObject obj)
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in obj.Properties())
            {
                dict[prop.Name] = prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Integer
                    ? (double)prop.Value
                    : (object)prop.Value.ToString();
            }
            return dict;
        }
    }
}
