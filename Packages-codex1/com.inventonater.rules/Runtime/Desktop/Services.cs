using System.Collections.Generic;
using UnityEngine;
using Inventonater.Rules.Engine;

namespace Inventonater.Rules.Desktop
{
    public class Services : MonoBehaviour, IServiceExecutor
    {
        public EntityStore Store;
        public void Execute(string service, Dictionary<string, object> data, EntityStore store)
        {
            switch (service)
            {
                case "audio.play":
                    var clipName = data != null && data.TryGetValue("clip", out var c) ? c.ToString() : "beep";
                    var vol = 1f;
                    if (data != null && data.TryGetValue("volume_0_to_1", out var v))
                        float.TryParse(v.ToString(), out vol);
                    var clip = Resources.Load<AudioClip>(clipName);
                    if (clip)
                        AudioSource.PlayClipAtPoint(clip, Vector3.zero, vol);
                    break;
                case "debug.log":
                    var msg = data != null && data.TryGetValue("message", out var m) ? m.ToString() : "";
                    Debug.Log(msg);
                    break;
                case "ui.toast":
                    var toast = data != null && data.TryGetValue("message", out var t) ? t.ToString() : "";
                    Debug.Log("TOAST: " + toast);
                    break;
                case "state.set":
                    if (data != null && data.TryGetValue("entity", out var e) && data.TryGetValue("value", out var val))
                    {
                        var entity = e.ToString();
                        if (double.TryParse(val.ToString(), out var num))
                            store.Set(entity, num);
                        else
                            store.Set(entity, val.ToString());
                    }
                    break;
            }
        }
    }
}
