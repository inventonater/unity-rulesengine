using System.Collections.Generic;
using System.Text.Json;

namespace Inventonater.Rules.Authoring
{
    public static class RuleRepository
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static List<RuleDto> FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<RuleDto>();
            var rules = JsonSerializer.Deserialize<List<RuleDto>>(json, Options) ?? new List<RuleDto>();
            foreach (var rule in rules)
            {
                RuleCoercion.Normalize(rule);
            }
            return rules;
        }
    }
}
