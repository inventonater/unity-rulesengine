using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inventonater.Rules.Authoring
{
    public static class RuleRepository
    {
        public static List<RuleDto> LoadFromJson(string json)
        {
            var root = JToken.Parse(json);
            var list = new List<RuleDto>();
            if (root.Type == JTokenType.Array)
            {
                foreach (var item in root)
                {
                    var rule = item.ToObject<RuleDto>();
                    RuleCoercion.Normalize(rule);
                    list.Add(rule);
                }
            }
            else
            {
                var rule = root.ToObject<RuleDto>();
                RuleCoercion.Normalize(rule);
                list.Add(rule);
            }
            return list;
        }

        public static List<RuleDto> LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return LoadFromJson(json);
        }
    }
}
