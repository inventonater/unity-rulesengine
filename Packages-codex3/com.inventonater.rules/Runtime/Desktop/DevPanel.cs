using Inventonater.Rules.Authoring;
using Inventonater.Rules.Engine;

namespace Inventonater.Rules.Desktop
{
    /// <summary>Minimal developer panel that loads rules from JSON.</summary>
    public class DevPanel
    {
        private readonly RuleEngine _engine;
        public DevPanel(RuleEngine engine) => _engine = engine;
        public void Load(string json)
        {
            var rules = RuleRepository.FromJson(json);
            _engine.Load(rules);
        }
    }
}
