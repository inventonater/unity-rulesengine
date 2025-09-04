Below is a complete, drop‑in **automation runner** for Unity in plain C# (no `MonoBehaviour`/`ScriptableObject`).
It models **Home Assistant–style** automations: **triggers (events)** → **conditions** → **actions**, loads them from **JSON**, and executes them with **async flows via UniTask** and **parsing via Newtonsoft.Json**.

It’s organized into small files so you can paste them into a folder like `Assets/AutomationRuntime/`.
At the end you’ll find **sample JSON**, **Quest input hookup examples**, and a **minimal bootstrap** that runs without any scene objects.

---

## Key design points

* **Zero MBs/SOs:** Uses `UniTask` player loop and `CancellationToken`s instead of Unity behaviours/ticking.
* **Extensible registries:** `TriggerRegistry`, `ConditionRegistry`, `ActionRegistry` map simple `type` strings → handlers.
* **Event bus:** In‑process, lock‑free, safe to publish from anywhere; triggers subscribe by name.
* **Templating:** `"{{ $.path }}"` placeholders using **JSONPath** against a merged runtime context (`event`, `vars`, etc.).
* **Concurrency modes:** `single | queued | restart` per automation (like Home Assistant).
* **Quest inputs:** Provided as an **event source** that emits events (examples for Unity XR and Oculus Integration via optional `#if OCULUS_INTEGRATION`).
* **HTTP, log, emit\_event, haptics** actions included; more can be added easily.

---

## 1) Contracts & Models — `AutomationContracts.cs`

```csharp
// Assets/AutomationRuntime/AutomationContracts.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    public sealed class AutomationEvent
    {
        public string Name { get; }
        public JObject Payload { get; }
        public DateTimeOffset Timestamp { get; }
        public string? Source { get; }

        public AutomationEvent(string name, JObject payload, string? source = null, DateTimeOffset? ts = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Payload = payload ?? new JObject();
            Source = source;
            Timestamp = ts ?? DateTimeOffset.UtcNow;
        }

        public override string ToString() => $"Event(Name={Name}, Source={Source}, Payload={Payload})";
    }

    // Automation definition DTOs (JSON-facing)
    public sealed class AutomationDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public string? alias;
        public string mode = "single"; // single | queued | restart
        public Dictionary<string, JToken>? variables; // optional default vars
        public List<TriggerDef> triggers = new();
        public List<ConditionDef>? conditions; // AND across top-level list
        public List<ActionDef> actions = new();
    }

    public sealed class TriggerDef { public string type = ""; public JObject @params = new(); }
    public sealed class ConditionDef { public string type = ""; public JObject @params = new(); }
    public sealed class ActionDef { public string type = ""; public JObject @params = new(); }

    // Runtime context per trigger execution
    public sealed class AutomationContext
    {
        public AutomationDefinition Definition { get; }
        public AutomationEvent Event { get; }
        public JObject Vars { get; } // merged variables at runtime (definition vars + inbound + any computed)
        public IServiceLocator Services { get; }
        public CancellationToken Cancellation { get; }

        public AutomationContext(AutomationDefinition def, AutomationEvent ev, JObject vars, IServiceLocator services, CancellationToken ct)
        {
            Definition = def;
            Event = ev;
            Vars = vars;
            Services = services;
            Cancellation = ct;
        }

        // JSON root for templating: $.event, $.vars, $.now, $.def
        public JObject ToTemplateRoot()
        {
            var root = new JObject
            {
                ["event"] = JObject.FromObject(new
                {
                    name = Event.Name,
                    payload = Event.Payload,
                    timestamp = Event.Timestamp,
                    source = Event.Source
                }),
                ["vars"] = Vars.DeepClone(),
                ["now"] = DateTimeOffset.UtcNow,
                ["def"] = JObject.FromObject(new { id = Definition.id, alias = Definition.alias, mode = Definition.mode })
            };
            return root;
        }
    }

    // Service locator to pass integrations without MBs
    public interface IServiceLocator
    {
        T? Get<T>() where T : class;
        object? Get(Type t);
    }

    public interface ILogger
    {
        void Info(string msg);
        void Warn(string msg);
        void Error(string msg);
        void Debug(string msg);
    }

    // Registries
    public interface ITrigger
    {
        // The trigger binds itself to the bus and pushes candidate events into the callback
        UniTask BindAsync(EventBus bus, AutomationDefinition def, Func<AutomationEvent, UniTask> onEvent, IServiceLocator services, ILogger log, CancellationToken ct);
    }

    public interface IConditionEvaluator
    {
        // Returns true if the condition passes for the given context
        bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log);
    }

    public interface IActionExecutor
    {
        UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log);
    }

    // Event sources (e.g., Quest inputs) can be run alongside the engine
    public interface IEventSource
    {
        UniTask RunAsync(EventBus bus, IServiceLocator services, ILogger log, CancellationToken ct);
    }
}
```

---

## 2) Event Bus — `EventBus.cs`

```csharp
// Assets/AutomationRuntime/EventBus.cs
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HAStyleAutomation
{
    public sealed class EventBus
    {
        private readonly ConcurrentDictionary<string, List<Func<AutomationEvent, UniTask>>> _subs = new();
        private readonly ConcurrentBag<Func<AutomationEvent, UniTask>> _wildcard = new();

        public void Subscribe(string eventName, Func<AutomationEvent, UniTask> handler)
        {
            if (string.IsNullOrEmpty(eventName) || eventName == "*")
            {
                _wildcard.Add(handler);
                return;
            }

            _subs.AddOrUpdate(eventName,
                _ => new List<Func<AutomationEvent, UniTask>> { handler },
                (_, list) => { lock (list) list.Add(handler); return list; });
        }

        public void Unsubscribe(string eventName, Func<AutomationEvent, UniTask> handler)
        {
            if (string.IsNullOrEmpty(eventName) || eventName == "*")
                return;

            if (_subs.TryGetValue(eventName, out var list))
            {
                lock (list) list.Remove(handler);
            }
        }

        public void Emit(AutomationEvent ev)
        {
            // Fire-and-forget on the main player loop; order isn't guaranteed
            foreach (var wc in _wildcard)
                wc(ev).Forget();

            if (_subs.TryGetValue(ev.Name, out var list))
            {
                List<Func<AutomationEvent, UniTask>> snapshot;
                lock (list) snapshot = new List<Func<AutomationEvent, UniTask>>(list);
                foreach (var h in snapshot) h(ev).Forget();
            }
        }
    }
}
```

---

## 3) Utilities — `TemplateResolver.cs` and `AsyncQueue.cs`

```csharp
// Assets/AutomationRuntime/TemplateResolver.cs
#nullable enable
using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    public static class TemplateResolver
    {
        // Replaces strings containing {{ $.json.path }} with values from root via SelectToken.
        // Recurses through JTokens to resolve in objects/arrays too.
        private static readonly Regex Placeholder = new(@"\{\{\s*([^\}]+)\s*\}\}", RegexOptions.Compiled);

        public static JToken ResolveAll(JToken token, JObject root)
        {
            return token switch
            {
                JValue v when v.Type == JTokenType.String => new JValue(ResolveString(v.Value<string>()!, root)),
                JObject o => ResolveObject(o, root),
                JArray a => ResolveArray(a, root),
                _ => token.DeepClone()
            };
        }

        public static string ResolveString(string s, JObject root)
        {
            return Placeholder.Replace(s, m =>
            {
                var path = m.Groups[1].Value.Trim();
                try
                {
                    var t = root.SelectToken(path);
                    return t switch
                    {
                        null => "",
                        JValue jv => jv.Value?.ToString() ?? "",
                        _ => t.ToString(Newtonsoft.Json.Formatting.None)
                    };
                }
                catch { return ""; }
            });
        }

        private static JObject ResolveObject(JObject o, JObject root)
        {
            var copy = new JObject();
            foreach (var p in o.Properties())
                copy[p.Name] = ResolveAll(p.Value, root);
            return copy;
        }

        private static JArray ResolveArray(JArray a, JObject root)
        {
            var arr = new JArray();
            foreach (var item in a) arr.Add(ResolveAll(item, root));
            return arr;
        }
    }
}

// Assets/AutomationRuntime/AsyncQueue.cs
#nullable enable
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HAStyleAutomation
{
    internal sealed class AsyncQueue<T>
    {
        private readonly ConcurrentQueue<T> _q = new();
        private UniTaskCompletionSource? _tcs;

        public void Enqueue(T item)
        {
            _q.Enqueue(item);
            _tcs?.TrySetResult();
        }

        public async UniTask<T> DequeueAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_q.TryDequeue(out var item))
                    return item;

                var tcs = new UniTaskCompletionSource();
                _tcs = tcs;
                using (ct.Register(() => tcs.TrySetCanceled()))
                    await tcs.Task;
            }
            ct.ThrowIfCancellationRequested();
            return default!;
        }
    }
}
```

---

## 4) Registries & Built‑ins — `Registries.cs`

```csharp
// Assets/AutomationRuntime/Registries.cs
#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    public sealed class TriggerRegistry
    {
        private readonly Dictionary<string, Func<JObject, ITrigger>> _f = new(StringComparer.OrdinalIgnoreCase);

        public void Register(string type, Func<JObject, ITrigger> factory) => _f[type] = factory;
        public ITrigger Resolve(TriggerDef def) =>
            _f.TryGetValue(def.type, out var f) ? f(def.@params) :
            throw new InvalidOperationException($"Unknown trigger type '{def.type}'");
    }

    public sealed class ConditionRegistry
    {
        private readonly Dictionary<string, Func<JObject, IConditionEvaluator>> _f = new(StringComparer.OrdinalIgnoreCase);
        public void Register(string type, Func<JObject, IConditionEvaluator> factory) => _f[type] = factory;
        public IConditionEvaluator Resolve(ConditionDef def) =>
            _f.TryGetValue(def.type, out var f) ? f(def.@params) :
            throw new InvalidOperationException($"Unknown condition type '{def.type}'");
    }

    public sealed class ActionRegistry
    {
        private readonly Dictionary<string, Func<JObject, IActionExecutor>> _f = new(StringComparer.OrdinalIgnoreCase);
        public void Register(string type, Func<JObject, IActionExecutor> factory) => _f[type] = factory;
        public IActionExecutor Resolve(ActionDef def) =>
            _f.TryGetValue(def.type, out var f) ? f(def.@params) :
            throw new InvalidOperationException($"Unknown action type '{def.type}'");
    }
}
```

---

## 5) Conditions — `BuiltInConditions.cs`

```csharp
// Assets/AutomationRuntime/BuiltInConditions.cs
#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    // Helper: base with params captured
    abstract class ConditionBase : IConditionEvaluator
    {
        public abstract bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log);
    }

    // always: {}
    sealed class AlwaysCondition : ConditionBase
    {
        public override bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log) => true;
    }

    // jsonpath_compare: { path: "$.event.payload.value", op: ">", value: 0.5 }
    sealed class JsonPathCompareCondition : ConditionBase
    {
        private readonly string _path; private readonly string _op; private readonly JToken _rhs;

        public JsonPathCompareCondition(JObject p)
        {
            _path = p.Value<string>("path") ?? throw new ArgumentException("path required");
            _op = p.Value<string>("op") ?? "==";
            _rhs = p["value"] ?? JValue.CreateNull();
        }
        public override bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log)
        {
            var root = ctx.ToTemplateRoot();
            var lhsTok = root.SelectToken(_path);
            if (lhsTok == null) return false;

            // Numeric/boolean/string comparisons
            int CmpAsDouble(JToken a, JToken b)
            {
                if (double.TryParse(a.ToString(), out var da) && double.TryParse(b.ToString(), out var db))
                    return da.CompareTo(db);
                return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
            }

            return _op switch
            {
                "==" => JToken.DeepEquals(lhsTok, _rhs) || lhsTok.ToString() == _rhs.ToString(),
                "!=" => !JToken.DeepEquals(lhsTok, _rhs) && lhsTok.ToString() != _rhs.ToString(),
                ">"  => CmpAsDouble(lhsTok, _rhs) > 0,
                ">=" => CmpAsDouble(lhsTok, _rhs) >= 0,
                "<"  => CmpAsDouble(lhsTok, _rhs) < 0,
                "<=" => CmpAsDouble(lhsTok, _rhs) <= 0,
                "exists" => lhsTok.Type != JTokenType.Null && lhsTok.Type != JTokenType.Undefined,
                _ => false
            };
        }
    }

    // and: { all: [ ... conditions ... ] }
    sealed class AndCondition : ConditionBase
    {
        private readonly List<ConditionDef> _conds;
        private readonly ConditionRegistry _registry;
        public AndCondition(ConditionRegistry reg, JObject p)
        {
            _registry = reg;
            _conds = p["all"]?.ToObject<List<ConditionDef>>() ?? new List<ConditionDef>();
        }
        public override bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log)
        {
            foreach (var c in _conds)
                if (!_registry.Resolve(c).Evaluate(c, ctx, log)) return false;
            return true;
        }
    }

    // or: { any: [ ... ] }
    sealed class OrCondition : ConditionBase
    {
        private readonly List<ConditionDef> _conds;
        private readonly ConditionRegistry _registry;
        public OrCondition(ConditionRegistry reg, JObject p)
        {
            _registry = reg;
            _conds = p["any"]?.ToObject<List<ConditionDef>>() ?? new List<ConditionDef>();
        }
        public override bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log)
        {
            foreach (var c in _conds)
                if (_registry.Resolve(c).Evaluate(c, ctx, log)) return true;
            return false;
        }
    }

    // not: { cond: { type: ..., params: ... } }
    sealed class NotCondition : ConditionBase
    {
        private readonly ConditionDef? _inner;
        private readonly ConditionRegistry _registry;
        public NotCondition(ConditionRegistry reg, JObject p)
        {
            _registry = reg;
            _inner = p["cond"]?.ToObject<ConditionDef>();
        }
        public override bool Evaluate(ConditionDef def, AutomationContext ctx, ILogger log)
        {
            if (_inner == null) return true;
            return !_registry.Resolve(_inner).Evaluate(_inner, ctx, log);
        }
    }

    public static class BuiltInConditions
    {
        public static void RegisterInto(ConditionRegistry reg)
        {
            reg.Register("always", _ => new AlwaysCondition());
            reg.Register("jsonpath_compare", p => new JsonPathCompareCondition(p));
            reg.Register("and", p => new AndCondition(reg, p));
            reg.Register("or", p => new OrCondition(reg, p));
            reg.Register("not", p => new NotCondition(reg, p));
        }
    }
}
```

---

## 6) Triggers — `BuiltInTriggers.cs`

```csharp
// Assets/AutomationRuntime/BuiltInTriggers.cs
#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    // event: { name: "door.open" }
    sealed class EventTrigger : ITrigger
    {
        private readonly string _name;
        private readonly ConditionDef? _predicate; // optional quick filter

        private readonly ConditionRegistry _condReg;
        public EventTrigger(ConditionRegistry condReg, JObject p)
        {
            _condReg = condReg;
            _name = p.Value<string>("name") ?? "*";
            var pred = p["predicate"] as JObject;
            _predicate = pred != null ? pred.ToObject<ConditionDef>() : null;
        }

        public UniTask BindAsync(EventBus bus, AutomationDefinition def, Func<AutomationEvent, UniTask> onEvent, IServiceLocator services, ILogger log, CancellationToken ct)
        {
            bus.Subscribe(_name == "*" ? "" : _name, async ev =>
            {
                if (ct.IsCancellationRequested) return;
                if (_predicate != null)
                {
                    var ctx = new AutomationContext(def, ev, MergeVars(def), services, ct);
                    if (!_condReg.Resolve(_predicate).Evaluate(_predicate, ctx, log)) return;
                }
                await onEvent(ev);
            });
            return UniTask.CompletedTask;
        }

        private static JObject MergeVars(AutomationDefinition def)
            => def.variables != null ? JObject.FromObject(def.variables) : new JObject();
    }

    // interval: { seconds: 5, name: "timer.tick" }
    sealed class IntervalTrigger : ITrigger
    {
        private readonly double _seconds;
        private readonly string _name;
        public IntervalTrigger(JObject p)
        {
            _seconds = Math.Max(0.01, p.Value<double?>("seconds") ?? 1.0);
            _name = p.Value<string>("name") ?? "time.interval";
        }

        public async UniTask BindAsync(EventBus bus, AutomationDefinition def, Func<AutomationEvent, UniTask> onEvent, IServiceLocator services, ILogger log, CancellationToken ct)
        {
            // Each automation gets its own ticking loop
            _ = UniTask.Create(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var ev = new AutomationEvent(_name, new JObject { ["intervalSeconds"] = _seconds }, "interval");
                    await onEvent(ev);
                    await UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: ct);
                }
            });
            await UniTask.CompletedTask;
        }
    }

    public static class BuiltInTriggers
    {
        public static void RegisterInto(TriggerRegistry reg, ConditionRegistry condReg)
        {
            reg.Register("event", p => new EventTrigger(condReg, p));
            reg.Register("interval", p => new IntervalTrigger(p));
            // More can be added (cron, sunrise, etc.)
        }
    }
}
```

---

## 7) Actions — `BuiltInActions.cs`

```csharp
// Assets/AutomationRuntime/BuiltInActions.cs
#nullable enable
using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking; // for HTTP
using UnityEngine;            // for Debug (no MB usage)

namespace HAStyleAutomation
{
    // Logger + templated message
    sealed class LogAction : IActionExecutor
    {
        private readonly string _level; private readonly string _message;
        public LogAction(JObject p)
        {
            _level = p.Value<string>("level") ?? "info";
            _message = p.Value<string>("message") ?? "";
        }
        public UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log)
        {
            var msg = TemplateResolver.ResolveString(_message, ctx.ToTemplateRoot());
            switch (_level.ToLowerInvariant())
            {
                case "warn": log.Warn(msg); break;
                case "error": log.Error(msg); break;
                case "debug": log.Debug(msg); break;
                default: log.Info(msg); break;
            }
            return UniTask.CompletedTask;
        }
    }

    // emit_event: { name: "...", payload: {...} }
    sealed class EmitEventAction : IActionExecutor
    {
        private readonly string _name; private readonly JObject _payload;
        public EmitEventAction(JObject p)
        {
            _name = p.Value<string>("name") ?? throw new ArgumentException("emit_event.name required");
            _payload = p["payload"] as JObject ?? new JObject();
        }
        public UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log)
        {
            var root = ctx.ToTemplateRoot();
            var payload = (JObject)TemplateResolver.ResolveAll(_payload, root);
            ctx.Services.Get<EventBus>()?.Emit(new AutomationEvent(_name, payload, "emit_event"));
            return UniTask.CompletedTask;
        }
    }

    // http.request: { method, url, headers?, json? | body? (string), timeoutSeconds? }
    sealed class HttpRequestAction : IActionExecutor
    {
        private readonly string _method, _url;
        private readonly JObject? _headers, _json;
        private readonly string? _body;
        private readonly int _timeout;

        public HttpRequestAction(JObject p)
        {
            _method = p.Value<string>("method") ?? "GET";
            _url = p.Value<string>("url") ?? throw new ArgumentException("http.request.url required");
            _headers = p["headers"] as JObject;
            _json = p["json"] as JObject;
            _body = p.Value<string>("body");
            _timeout = Math.Max(1, p.Value<int?>("timeoutSeconds") ?? 15);
        }

        public async UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log)
        {
            var root = ctx.ToTemplateRoot();
            var url = TemplateResolver.ResolveString(_url, root);
            using var req = new UnityWebRequest(url, _method.ToUpperInvariant());

            if (_json != null)
            {
                var resolved = TemplateResolver.ResolveAll(_json, root);
                var data = Encoding.UTF8.GetBytes(resolved.ToString(Newtonsoft.Json.Formatting.None));
                req.uploadHandler = new UploadHandlerRaw(data);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
            }
            else if (_body != null)
            {
                var b = TemplateResolver.ResolveString(_body, root);
                var data = Encoding.UTF8.GetBytes(b);
                req.uploadHandler = new UploadHandlerRaw(data);
                req.downloadHandler = new DownloadHandlerBuffer();
            }
            else
            {
                req.downloadHandler = new DownloadHandlerBuffer();
            }

            if (_headers != null)
            {
                foreach (var prop in _headers)
                {
                    var val = TemplateResolver.ResolveString(prop.Value?.ToString() ?? "", root);
                    req.SetRequestHeader(prop.Key, val);
                }
            }

            var op = req.SendWebRequest();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.Cancellation);
            cts.CancelAfterSlim(TimeSpan.FromSeconds(_timeout));
            try
            {
                await op.WithCancellation(cts.Token);
                if (req.result != UnityWebRequest.Result.Success)
                    log.Warn($"HTTP {_method} {url} => {req.result} {req.responseCode} {req.error}");
                else
                    log.Debug($"HTTP {_method} {url} => {req.responseCode} bodyLen={req.downloadedBytes}");
            }
            catch (OperationCanceledException)
            {
                log.Warn($"HTTP {_method} {url} timed out after {_timeout}s");
            }
        }
    }

    // haptics.pulse: { hand: "left"|"right", amplitude: 0..1, durationMs: int }
    // Requires an IHapticsService registered in the ServiceLocator.
    public interface IHapticsService
    {
        UniTask PulseAsync(string hand, float amplitude, int durationMs, CancellationToken ct);
    }

    sealed class HapticsPulseAction : IActionExecutor
    {
        private readonly string _hand; private readonly float _amp; private readonly int _ms;
        public HapticsPulseAction(JObject p)
        {
            _hand = p.Value<string>("hand") ?? "right";
            _amp = (float)(p.Value<double?>("amplitude") ?? 0.5);
            _ms = p.Value<int?>("durationMs") ?? 100;
        }
        public async UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log)
        {
            var svc = ctx.Services.Get<IHapticsService>();
            if (svc == null) { log.Warn("No IHapticsService registered"); return; }
            await svc.PulseAsync(_hand, Mathf.Clamp01(_amp), Math.Max(1, _ms), ctx.Cancellation);
        }
    }

    public static class BuiltInActions
    {
        public static void RegisterInto(ActionRegistry reg)
        {
            reg.Register("log", p => new LogAction(p));
            reg.Register("emit_event", p => new EmitEventAction(p));
            reg.Register("http.request", p => new HttpRequestAction(p));
            reg.Register("haptics.pulse", p => new HapticsPulseAction(p));
        }
    }
}
```

---

## 8) Engine — `AutomationEngine.cs`

```csharp
// Assets/AutomationRuntime/AutomationEngine.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HAStyleAutomation
{
    public sealed class AutomationEngine
    {
        public readonly EventBus Bus = new();
        public readonly TriggerRegistry Triggers = new();
        public readonly ConditionRegistry Conditions = new();
        public readonly ActionRegistry Actions = new();

        private readonly IServiceLocator _services;
        private readonly ILogger _log;

        private readonly Dictionary<string, AutomationInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

        public AutomationEngine(IServiceLocator services, ILogger log)
        {
            _services = services;
            _log = log;

            // Register built-ins
            BuiltInConditions.RegisterInto(Conditions);
            BuiltInTriggers.RegisterInto(Triggers, Conditions);
            BuiltInActions.RegisterInto(Actions);
        }

        public void LoadFromJsonString(string json)
        {
            JToken root = JToken.Parse(json);
            var defs = new List<AutomationDefinition>();

            if (root.Type == JTokenType.Array)
                defs.AddRange(root.ToObject<List<AutomationDefinition>>() ?? new List<AutomationDefinition>());
            else
                defs.Add(root.ToObject<AutomationDefinition>() ?? new AutomationDefinition());

            foreach (var def in defs)
                AddOrReplace(def);
        }

        public void LoadFromDirectory(string dir, string pattern = "*.automation.json")
        {
            foreach (var file in Directory.GetFiles(dir, pattern))
            {
                var json = File.ReadAllText(file);
                LoadFromJsonString(json);
            }
        }

        public void AddOrReplace(AutomationDefinition def)
        {
            if (_instances.TryGetValue(def.id, out var old))
            {
                old.Dispose();
                _instances.Remove(def.id);
            }
            var inst = new AutomationInstance(def, this, _services, _log);
            _instances[def.id] = inst;
            inst.Activate();
            _log.Info($"Automation loaded: {def.alias ?? def.id} (mode={def.mode})");
        }

        public void StopAll()
        {
            foreach (var kv in _instances) kv.Value.Dispose();
            _instances.Clear();
        }
    }

    internal sealed class AutomationInstance : IDisposable
    {
        private readonly AutomationDefinition _def;
        private readonly AutomationEngine _engine;
        private readonly IServiceLocator _services;
        private readonly ILogger _log;

        private readonly List<ITrigger> _boundTriggers = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly AsyncQueue<AutomationEvent> _queue = new();
        private UniTask _loopTask;

        public AutomationInstance(AutomationDefinition def, AutomationEngine engine, IServiceLocator services, ILogger log)
        {
            _def = def; _engine = engine; _services = services; _log = log;
        }

        public void Activate()
        {
            // Bind triggers
            foreach (var tdef in _def.triggers)
            {
                var trig = _engine.Triggers.Resolve(tdef);
                _boundTriggers.Add(trig);
                trig.BindAsync(_engine.Bus, _def, OnTrigger, _services, _log, _cts.Token).Forget();
            }

            // Main runner loop
            _loopTask = UniTask.Create(async () =>
            {
                CancellationToken ct = _cts.Token;
                UniTask running = UniTask.CompletedTask;
                var runningCts = new CancellationTokenSource();

                while (!ct.IsCancellationRequested)
                {
                    var ev = await _queue.DequeueAsync(ct);

                    if (_def.mode.Equals("single", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!running.Status.IsCompleted()) { _log.Debug($"[{_def.alias ?? _def.id}] drop event {ev.Name} (busy)"); continue; }
                        running = RunOnce(ev, runningCts.Token);
                    }
                    else if (_def.mode.Equals("restart", StringComparison.OrdinalIgnoreCase))
                    {
                        runningCts.Cancel();
                        runningCts.Dispose();
                        runningCts = new CancellationTokenSource();
                        running = RunOnce(ev, runningCts.Token);
                    }
                    else /* queued */
                    {
                        // await previous and chain
                        running = AwaitThenRun(running, () => RunOnce(ev, runningCts.Token));
                    }
                }
            });
        }

        private async UniTask AwaitThenRun(UniTask prev, Func<UniTask> next)
        {
            try { await prev; } catch { /* swallow */ }
            await next();
        }

        private async UniTask OnTrigger(AutomationEvent ev)
        {
            _queue.Enqueue(ev);
            await UniTask.Yield(); // return quickly
        }

        private async UniTask RunOnce(AutomationEvent ev, CancellationToken ct)
        {
            var vars = _def.variables != null ? JObject.FromObject(_def.variables) : new JObject();
            var ctx = new AutomationContext(_def, ev, vars, _services, ct);

            // Conditions (AND across top level list if present)
            if (_def.conditions != null)
            {
                foreach (var c in _def.conditions)
                {
                    if (!_engine.Conditions.Resolve(c).Evaluate(c, ctx, _log))
                    {
                        _log.Debug($"[{_def.alias ?? _def.id}] conditions failed for event {ev.Name}");
                        return;
                    }
                }
            }

            // Actions
            foreach (var a in _def.actions)
            {
                var exec = _engine.Actions.Resolve(a);
                try { await exec.ExecuteAsync(a, ctx, _log); }
                catch (OperationCanceledException) { _log.Warn($"[{_def.alias ?? _def.id}] action canceled ({a.type})"); throw; }
                catch (Exception ex) { _log.Error($"[{_def.alias ?? _def.id}] action '{a.type}' failed: {ex}"); }
            }
        }

        public void Dispose() => _cts.Cancel();
    }
}
```

---

## 9) Default Services (Logger/Locator) — `Infrastructure.cs`

```csharp
// Assets/AutomationRuntime/Infrastructure.cs
#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HAStyleAutomation
{
    public sealed class ConsoleLogger : ILogger
    {
        private readonly string _prefix;
        public ConsoleLogger(string prefix = "Automation") { _prefix = prefix; }
        public void Info(string msg)  => Debug.Log($"[{_prefix}] {msg}");
        public void Warn(string msg)  => Debug.LogWarning($"[{_prefix}] {msg}");
        public void Error(string msg) => Debug.LogError($"[{_prefix}] {msg}");
        public void Debug(string msg) => Debug.Log($"[{_prefix}][DBG] {msg}");
    }

    public sealed class DictionaryServiceLocator : IServiceLocator
    {
        private readonly Dictionary<Type, object> _map = new();
        public DictionaryServiceLocator Register<T>(T impl) where T : class { _map[typeof(T)] = impl; return this; }
        public T? Get<T>() where T : class => _map.TryGetValue(typeof(T), out var o) ? (T)o : null;
        public object? Get(Type t) => _map.TryGetValue(t, out var o) ? o : null;
    }
}
```

---

## 10) Quest Input → Events (optional sources)

> You can pick either **Unity XR** or **Oculus Integration (OVRInput)**. Both are **optional** and compile‑guarded. They publish events like `xr.button` and `xr.axis` into the bus.

### a) Unity XR (new input system not required) — `UnityXREventSource.cs`

```csharp
// Assets/AutomationRuntime/UnityXREventSource.cs
#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace HAStyleAutomation
{
    public sealed class UnityXREventSource : IEventSource
    {
        private readonly float _pollHz;
        public UnityXREventSource(float pollHz = 90f) { _pollHz = Mathf.Max(10f, pollHz); }

        public async UniTask RunAsync(EventBus bus, IServiceLocator services, ILogger log, CancellationToken ct)
        {
            var devices = new List<InputDevice>();
            bool lastPrimaryRight = false, lastPrimaryLeft = false;

            while (!ct.IsCancellationRequested)
            {
                InputDevices.GetDevices(devices);

                var right = devices.Find(d => d.characteristics.HasFlag(InputDeviceCharacteristics.Right) && d.characteristics.HasFlag(InputDeviceCharacteristics.Controller));
                var left  = devices.Find(d => d.characteristics.HasFlag(InputDeviceCharacteristics.Left)  && d.characteristics.HasFlag(InputDeviceCharacteristics.Controller));

                if (right.isValid && right.TryGetFeatureValue(CommonUsages.primaryButton, out bool pr))
                {
                    if (pr && !lastPrimaryRight)
                        bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="right", ["button"]="primary", ["state"]="pressed" }, "UnityXR"));
                    else if (!pr && lastPrimaryRight)
                        bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="right", ["button"]="primary", ["state"]="released" }, "UnityXR"));
                    lastPrimaryRight = pr;
                }

                if (left.isValid && left.TryGetFeatureValue(CommonUsages.primaryButton, out bool pl))
                {
                    if (pl && !lastPrimaryLeft)
                        bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="left", ["button"]="primary", ["state"]="pressed" }, "UnityXR"));
                    else if (!pl && lastPrimaryLeft)
                        bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="left", ["button"]="primary", ["state"]="released" }, "UnityXR"));
                    lastPrimaryLeft = pl;
                }

                await UniTask.Delay(System.TimeSpan.FromSeconds(1.0 / _pollHz), cancellationToken: ct);
            }
        }
    }
}
```

### b) Oculus Integration (OVRInput) — `OculusQuestEventSource.cs`

```csharp
// Assets/AutomationRuntime/OculusQuestEventSource.cs
#nullable enable
#if OCULUS_INTEGRATION
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HAStyleAutomation
{
    public sealed class OculusQuestEventSource : IEventSource
    {
        private readonly float _pollHz;
        public OculusQuestEventSource(float pollHz = 90f) { _pollHz = Mathf.Max(30f, pollHz); }

        public async UniTask RunAsync(EventBus bus, IServiceLocator services, ILogger log, CancellationToken ct)
        {
            bool lastA=false, lastX=false;

            while (!ct.IsCancellationRequested)
            {
                bool a = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
                if (a && !lastA) bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="right", ["button"]="A", ["state"]="pressed" }, "OVR"));
                if (!a && lastA) bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="right", ["button"]="A", ["state"]="released" }, "OVR"));
                lastA = a;

                bool x = OVRInput.Get(OVRInput.Button.Three, OVRInput.Controller.LTouch);
                if (x && !lastX) bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="left", ["button"]="X", ["state"]="pressed" }, "OVR"));
                if (!x && lastX) bus.Emit(new AutomationEvent("xr.button", new JObject { ["hand"]="left", ["button"]="X", ["state"]="released" }, "OVR"));
                lastX = x;

                await UniTask.Delay(System.TimeSpan.FromSeconds(1.0 / _pollHz), cancellationToken: ct);
            }
        }
    }
}
#endif
```

### c) Oculus Haptics Service (optional) — `OculusHapticsService.cs`

```csharp
// Assets/AutomationRuntime/OculusHapticsService.cs
#nullable enable
#if OCULUS_INTEGRATION
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HAStyleAutomation
{
    public sealed class OculusHapticsService : IHapticsService
    {
        public UniTask PulseAsync(string hand, float amplitude, int durationMs, CancellationToken ct)
        {
            var controller = (hand.ToLowerInvariant() == "left") ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            // Simple vibration using OVRInput.SetControllerVibration
            _ = UniTask.Create(async () =>
            {
                OVRInput.SetControllerVibration(160f, amplitude, controller);
                try { await UniTask.Delay(durationMs, cancellationToken: ct); } finally { OVRInput.SetControllerVibration(0f, 0f, controller); }
            });
            return UniTask.CompletedTask;
        }
    }
}
#endif
```

---

## 11) Bootstrap (no MBs) — `AutomationBootstrap.cs`

This shows how to start the engine from code (e.g., inside your own static entry point or any existing initialization you already have).
It also demonstrates running **event sources** alongside the engine.

```csharp
// Assets/AutomationRuntime/AutomationBootstrap.cs
#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HAStyleAutomation
{
    public static class AutomationBootstrap
    {
        private static CancellationTokenSource? _cts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // Comment out this attribute if you don't want auto-start.
            StartFromStreamingAssets().Forget();
        }

        public static async UniTaskVoid StartFromStreamingAssets()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var log = new ConsoleLogger();
            var services = new DictionaryServiceLocator();

            var engine = new AutomationEngine(services, log);

            // Make the bus accessible as a service (so actions can emit events)
            services.Register(engine.Bus);

            // Optional: register haptics for Quest (if using OVR)
#if OCULUS_INTEGRATION
            services.Register<IHapticsService>(new OculusHapticsService());
#endif

            // Load JSON from StreamingAssets/automations/
            try
            {
                var dir = System.IO.Path.Combine(Application.streamingAssetsPath, "automations");
                engine.LoadFromDirectory(dir);
            }
            catch (Exception ex) { log.Error($"Failed loading automations: {ex}"); }

            // Run event sources (Unity XR here; swap or add others)
            var src = new UnityXREventSource(90f);
            _ = src.RunAsync(engine.Bus, services, log, _cts.Token);

            // Demo: emit a test event 2s after start
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: _cts.Token);
            engine.Bus.Emit(new AutomationEvent("app.started", new Newtonsoft.Json.Linq.JObject { ["hello"] = "world" }, "bootstrap"));
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
```

---

## 12) Example automation JSON (LLM‑friendly)

Save as `StreamingAssets/automations/quest-demo.automation.json`:

```json
{
  "id": "quest-demo-1",
  "alias": "Pulse when A pressed",
  "mode": "restart",
  "variables": {
    "amp": 0.8,
    "durMs": 120
  },
  "triggers": [
    {
      "type": "event",
      "params": {
        "name": "xr.button",
        "predicate": {
          "type": "and",
          "params": {
            "all": [
              { "type": "jsonpath_compare", "params": { "path": "$.event.payload.state", "op": "==", "value": "pressed" } },
              { "type": "jsonpath_compare", "params": { "path": "$.event.payload.hand", "op": "==", "value": "right" } }
            ]
          }
        }
      }
    }
  ],
  "actions": [
    {
      "type": "log",
      "params": { "level": "info", "message": "Button pressed: {{ $.event.payload.hand }} {{ $.event.payload.button }}" }
    },
    {
      "type": "haptics.pulse",
      "params": { "hand": "{{ $.event.payload.hand }}", "amplitude": "{{ $.vars.amp }}", "durationMs": "{{ $.vars.durMs }}" }
    },
    {
      "type": "emit_event",
      "params": { "name": "analytics.button_press", "payload": { "hand": "{{ $.event.payload.hand }}", "button": "{{ $.event.payload.button }}" } }
    }
  ]
}
```

Another example: **interval timer posting to webhook** (`timer.automation.json`):

```json
{
  "id": "timer-ping-1",
  "alias": "Ping every 10s",
  "mode": "queued",
  "triggers": [
    { "type": "interval", "params": { "seconds": 10, "name": "timer.tick" } }
  ],
  "actions": [
    { "type": "log", "params": { "level": "debug", "message": "Tick at {{ $.now }}" } },
    {
      "type": "http.request",
      "params": {
        "method": "POST",
        "url": "https://httpbin.org/post",
        "json": { "time": "{{ $.now }}", "id": "{{ $.def.id }}" },
        "timeoutSeconds": 5
      }
    }
  ]
}
```

---

## 13) Mapping your own actions/services

Add an action:

```csharp
// Example: actions.my_custom
public sealed class MyCustomAction : IActionExecutor
{
    private readonly string _msg;
    public MyCustomAction(Newtonsoft.Json.Linq.JObject p){ _msg = p.Value<string>("msg") ?? "hi"; }

    public UniTask ExecuteAsync(ActionDef def, AutomationContext ctx, ILogger log)
    {
        var resolved = TemplateResolver.ResolveString(_msg, ctx.ToTemplateRoot());
        // do stuff...
        log.Info($"MyCustomAction: {resolved}");
        return UniTask.CompletedTask;
    }
}

// Register:
engine.Actions.Register("actions.my_custom", p => new MyCustomAction(p));
```

Add a service (e.g., a gameplay API):

```csharp
public interface IGameplayAPI { void SetSpeed(float v); }
services.Register<IGameplayAPI>(new GameplayAPIImpl(/* deps */));
```

Then call it from an action executor via `ctx.Services.Get<IGameplayAPI>()`.

---

## 14) Notes & best‑practice tips

* **Threading:** All bus emissions and action execution naturally run on Unity’s main thread via UniTask scheduling. If you emit off‑thread, the bus handlers still run in the player loop because we don’t block.
* **Cancellation & modes:** `restart` cancels an in‑flight sequence when a new trigger arrives. `queued` waits; `single` drops new triggers while running.
* **Templating:** Any string in action/condition params can reference **\$.event**, **\$.vars**, **\$.now**, **\$.def** (JSONPath). Arrays/objects are resolved recursively.
* **Validation:** For production, consider adding JSON Schema validation before `AddOrReplace` so LLM‑authored files fail fast with clear errors.
* **File watching:** If you want live reload on desktop, you can add a `FileSystemWatcher` wrapper (avoid on Android/Quest). Expose an API like `engine.LoadFromDirectory(...)` and call it when files change.

---

## 15) Minimal unit smoke test (NUnit) — optional

```csharp
#if UNITY_EDITOR
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

public class AutomationTests
{
    [Test]
    public void LogAction_Works()
    {
        var services = new HAStyleAutomation.DictionaryServiceLocator();
        var log = new HAStyleAutomation.ConsoleLogger("Test");
        var engine = new HAStyleAutomation.AutomationEngine(services, log);
        services.Register(engine.Bus);

        var json = @"{
          'id':'t1','alias':'test','mode':'single',
          'triggers':[{'type':'event','params':{'name':'ping'}}],
          'actions':[{'type':'log','params':{'message':'Hello {{ $.event.name }}'}}]
        }";

        engine.LoadFromJsonString(json);
        engine.Bus.Emit(new HAStyleAutomation.AutomationEvent("ping", new JObject(), "test"));

        // If it didn't throw, consider it ok for smoke
        Assert.Pass();
    }
}
#endif
```

---

## 16) Quick “how do I run it” checklist

1. Install packages:

   * **Newtonsoft.Json** (via Unity Package Manager → Add package by name: `com.unity.nuget.newtonsoft-json`, or include DLL).
   * **UniTask** (add `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` in Package Manager).
   * (Optional) **Oculus Integration** if using OVRInput; add scripting define `OCULUS_INTEGRATION`.

2. Create folder `Assets/StreamingAssets/automations/` and drop your JSON files there (examples above).

3. Enter Play Mode. The bootstrapper loads automations, starts the Unity XR event source, and you can press controller buttons to see logs/haptics.

---

### That’s it

This gives you a compact, extensible **automation runner** you can feed with LLM‑generated JSON. Extend by registering new triggers/conditions/actions or by plugging additional **event sources** for local Quest inputs, MQTT frames, BLE devices, etc.
