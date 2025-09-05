using System.Collections.Generic;
using System.Threading;
using Inventonater.RulesEngine.Authoring;
using Inventonater.RulesEngine.Desktop;

namespace Inventonater.RulesEngine.Engine
{
    public static class ActionRunner
    {
        public static void Run(IEnumerable<ActionDto> actions, Services services, EntityStore store)
        {
            foreach (var a in actions)
            {
                switch (a)
                {
                    case ServiceCallActionDto sc:
                        services.Call(sc.Service, sc.Data);
                        break;
                    case WaitDurationActionDto wait:
                        Thread.Sleep(wait.DurationMs);
                        break;
                    case RepeatCountActionDto rep:
                        for (int i = 0; i < rep.Count; i++)
                        {
                            Run(rep.Actions, services, store);
                        }
                        break;
                    case StopActionDto:
                        return;
                }
            }
        }
    }
}
