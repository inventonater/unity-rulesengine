using System;
using System.Collections.Generic;
using Inventonater.RulesEngine.Engine;

namespace Inventonater.RulesEngine.Desktop
{
    public class Services
    {
        private readonly EntityStore _store;

        public Services(EntityStore store)
        {
            _store = store;
        }

        public void Call(string service, Dictionary<string, object> data)
        {
            switch (service)
            {
                case "debug.log":
                    if (data != null && data.TryGetValue("message", out var msg))
                        Console.WriteLine(msg);
                    break;
                case "audio.play":
                    Console.WriteLine("[audio] play {0}", data != null && data.TryGetValue("clip", out var clip) ? clip : "beep");
                    break;
                case "ui.toast":
                    Console.WriteLine("[toast] {0}", data != null && data.TryGetValue("text", out var text) ? text : string.Empty);
                    break;
                case "state.set":
                    if (data != null && data.TryGetValue("entity", out var ent) && data.TryGetValue("value", out var val))
                        _store.Set(ent.ToString()!, val);
                    break;
            }
        }
    }
}
