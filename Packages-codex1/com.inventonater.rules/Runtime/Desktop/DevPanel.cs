using UnityEngine;
using Inventonater.Rules.Authoring;
using Inventonater.Rules.Engine;
using System.Collections.Generic;

namespace Inventonater.Rules.Desktop
{
    public class DevPanel : MonoBehaviour
    {
        public TextAsset[] RuleFiles;
        private RuleEngine _engine;

        void Start()
        {
            var bus = new EventBus();
            var timer = new TimerService();
            var store = new EntityStore();
            var services = GetComponent<Services>();
            services.Store = store;
            var input = GetComponent<DesktopInput>();
            input.Bus = bus;
            input.Store = store;

            _engine = new RuleEngine(bus, timer, store, services);
            var rules = new List<RuleDto>();
            foreach (var ta in RuleFiles)
                rules.AddRange(RuleRepository.LoadFromJson(ta.text));
            _engine.Load(rules);
        }
    }
}
