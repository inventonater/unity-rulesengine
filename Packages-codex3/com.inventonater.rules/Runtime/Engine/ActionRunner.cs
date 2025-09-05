using System;
using System.Collections.Generic;
using System.Threading;
using Inventonater.Rules.Authoring;
using Inventonater.Rules.Desktop;

namespace Inventonater.Rules.Engine
{
    public class ActionRunner
    {
        private readonly Services _services;

        public ActionRunner(Services services) => _services = services;

        public void Run(List<ActionDto> actions)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                if (RunAction(a)) break;
            }
        }

        private bool RunAction(ActionDto action)
        {
            switch (action.type)
            {
                case "service_call":
                    _services.Call(action.service, action.data);
                    break;
                case "wait_duration":
                    if (action.duration_ms_0_to_60000.HasValue)
                        Thread.Sleep(action.duration_ms_0_to_60000.Value);
                    break;
                case "repeat_count":
                    int count = action.count_1_to_20 ?? 1;
                    for (int i = 0; i < count; i++)
                    {
                        foreach (var child in action.actions)
                        {
                            if (RunAction(child)) return true;
                        }
                    }
                    break;
                case "stop":
                    return true; // halt sequence
            }
            return false;
        }
    }
}
