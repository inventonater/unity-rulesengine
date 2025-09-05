using System;
using System.Collections.Generic;
using Inventonater.Rules.Engine;

namespace Inventonater.Rules.Desktop
{
    public class Services
    {
        private readonly EntityStore _store;
        public Services(EntityStore store) => _store = store;

        public void Call(string service, Dictionary<string, object> data)
        {
            switch (service)
            {
                case "debug.log":
                    if (data != null && data.TryGetValue("message", out var msg))
                        Console.WriteLine(msg);
                    break;
                case "audio.play":
                    // In lieu of Unity audio, just log the clip name
                    if (data != null && data.TryGetValue("clip", out var clip))
                        Console.WriteLine($"Play clip: {clip}");
                    break;
                case "ui.toast":
                    if (data != null && data.TryGetValue("message", out var toast))
                        Console.WriteLine($"Toast: {toast}");
                    break;
                case "state.set":
                    if (data != null && data.TryGetValue("entity", out var entity) && data.TryGetValue("value", out var value))
                        _store.Set(entity.ToString(), value);
                    break;
            }
        }
    }
}
