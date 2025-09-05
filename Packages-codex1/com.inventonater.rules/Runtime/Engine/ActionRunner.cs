using System.Collections.Generic;
using System.Threading.Tasks;
using Inventonater.Rules.Authoring;

namespace Inventonater.Rules.Engine
{
    public interface IServiceExecutor
    {
        void Execute(string service, Dictionary<string, object> data, EntityStore store);
    }

    public class ActionRunner
    {
        private readonly IServiceExecutor _services;
        private bool _stopped;

        public ActionRunner(IServiceExecutor services)
        {
            _services = services;
        }

        public async Task RunAsync(IEnumerable<ActionDto> actions, EntityStore store)
        {
            _stopped = false;
            await RunList(actions, store);
        }

        private async Task RunList(IEnumerable<ActionDto> list, EntityStore store)
        {
            foreach (var a in list)
            {
                if (_stopped) break;
                switch (a.Type)
                {
                    case "service_call":
                        _services.Execute(a.Service, a.Data, store);
                        break;
                    case "wait_duration":
                        if (a.DurationMs.HasValue)
                            await Task.Delay(a.DurationMs.Value);
                        break;
                    case "repeat_count":
                        if (a.Count.HasValue && a.Actions != null)
                        {
                            for (int i = 0; i < a.Count.Value; i++)
                                await RunList(a.Actions, store);
                        }
                        break;
                    case "stop":
                        _stopped = true;
                        break;
                }
            }
        }
    }
}
