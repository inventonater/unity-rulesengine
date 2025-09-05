using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inventonater.RulesEngine.Authoring
{
    public static class RuleRepository
    {
        public static List<RuleDto> LoadFromFile(string path)
        {
            var text = File.ReadAllText(path);
            return LoadFromText(text);
        }

        public static List<RuleDto> LoadFromText(string json)
        {
            var obj = JObject.Parse(json);
            var triggers = obj["triggers"] as JArray;
            if (triggers != null)
            {
                foreach (JObject trig in triggers)
                {
                    RuleCoercion.NormalizeTrigger(trig);
                }
            }
            var rule = obj.ToObject<RuleDto>();
            return rule != null ? new List<RuleDto> { rule } : new List<RuleDto>();
        }
    }
}
