using System.Collections.Generic;
using Inventonater.RulesEngine.Authoring;
using Inventonater.RulesEngine.Engine;

namespace Inventonater.RulesEngine.Desktop
{
    /// <summary>
    /// Minimal development helper to load rules from JSON strings.
    /// </summary>
    public class DevPanel
    {
        private readonly RuleEngine _engine;

        public DevPanel(RuleEngine engine)
        {
            _engine = engine;
        }

        public void LoadJson(string json)
        {
            var rules = RuleRepository.LoadFromText(json);
            foreach (var r in rules)
            {
                _engine.AddRule(r);
            }
        }
    }
}
