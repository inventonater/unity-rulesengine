Below is a complete, Unity‑ready plan to implement a **Home‑Assistant‑style Automations engine**—**Trigger → Conditions → Actions**—for **Quest (OpenXR) VR input** and app‑specific logic. It leans on **R3** (modern Rx for Unity), **UniTask** (async on PlayerLoop), **VContainer** (DI, pure C# entry points), **MessagePipe** (fast, DI‑first event bus), **Unity Input System** (OpenXR controllers), **Cronos** (cron/interval timers), **Newtonsoft.Json** (JSON), **JsonSchema.Net** (validate LLM‑generated JSON), and **ZLogger** (high‑performance logs). None of your core logic needs `MonoBehaviour` or `ScriptableObject`. ([GitHub][1], [Medium][2], [Cysharp][3], [VContainer][4], [Unity Documentation][5], [json-everything][6])

---

## 0) TL;DR architecture

* **Core:**
  `EventBus` (MessagePipe) + `AutomationEngine` (R3 + UniTask) + `StateStore` + `ServiceRegistry` (your action endpoints).
* **Adapters:**
  `InputSystemAdapter` (OpenXR → events), `TimerScheduler` (Cronos/interval), optional `Scene/Physics` adapters if needed.
* **Config:**
  Automations defined in JSON (validated with a schema), hot‑reloaded at runtime.
* **Bootstrapping:**
  Pure C# entry points via **VContainer**, scheduled on its own player‑loop hooks—no MonoBehaviours in domain logic. ([VContainer][4])

---

## 1) Dependencies (Unity‑proven & IL2CPP‑friendly)

| Purpose              | Package                                                 | Why this one                                                                                                                |
| -------------------- | ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Reactive pipelines   | **R3** (`com.cysharp.r3`)                               | Modern, Unity‑optimized successor to UniRx; frame/time providers; resilient error model. ([GitHub][1], [Medium][2])         |
| Async on PlayerLoop  | **UniTask**                                             | Allocation‑free async/await; Delay/Yield on PlayerLoop; no threads required. ([GitHub][7])                                  |
| Dependency injection | **VContainer**                                          | Fast DI; supports **pure C# entry points** (no MonoBehaviour) and integrates with R3/UniTask/MessagePipe. ([VContainer][4]) |
| Event bus            | **MessagePipe**                                         | High‑performance in‑memory pub/sub with DI; perfect “system events” backbone. ([GitHub][8])                                 |
| Input                | **Unity Input System** + **OpenXR**                     | Action‑based, evented input; OpenXR layouts for Quest controllers. ([Unity Documentation][5])                               |
| Timers/Cron          | **Cronos**                                              | Well‑maintained cron library with timezone/DST handling. ([GitHub][9])                                                      |
| JSON parsing         | **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) | Mature, fast, officially packaged for Unity; IL2CPP‑friendly builds available. ([Unity Documentation][10], [npm][11])       |
| JSON validation      | **JsonSchema.Net**                                      | Implements JSON Schema; validate LLM‑generated configs at load. ([json-everything][6], [JSON Schema][12])                   |
| Logging              | **ZLogger**                                             | Zero‑alloc logging, structured logs; Unity‑supported. ([GitHub][13])                                                        |

> **AOT/IL2CPP note:** avoid engines that use `System.Reflection.Emit` / `dynamic` at runtime; IL2CPP can’t JIT. Stick to the above stack and static, typed predicates in conditions. ([Unity Documentation][14], [Unity Discussions][15])

---

## 2) Data model (JSON)

Automations mirror HA’s shape: `triggers[]  → conditions[] → actions[]`. Example:

```json
{
  "id": "double-tap-shield",
  "alias": "Double-tap A to toggle shield",
  "run_mode": "queued",
  "max_concurrency": 1,
  "triggers": [
    {
      "type": "input_action",
      "action": "xr/a_button",
      "phase": "performed",
      "interactions": "multiTap(tapCount=2, tapTime=0.3)",
      "hand": "right"
    }
  ],
  "conditions": [
    { "type": "bool_state", "entity": "player.armed", "is": true },
    { "type": "cooldown", "key": "shield-toggle", "seconds": 2 }
  ],
  "actions": [
    { "type": "call_service", "service": "combat.toggle_shield" },
    { "type": "call_service", "service": "haptics.pulse", "data": { "hand": "right", "amplitude": 0.7, "duration_ms": 80 } }
  ]
}
```

Provide a **JSON Schema** and validate any LLM‑generated JSON before applying changes. (Use `JsonSchema.Net`.) ([json-everything][6])

---

## 3) Subsystems & responsibilities

### 3.1 EventBus (MessagePipe)

* Typed channels: `InputActionEvent`, `StateChangedEvent<T>`, `TimerEvent`, `CustomEvent`.
* Simple DI‑first pub/sub for systems to remain decoupled. ([GitHub][8])

### 3.2 InputSystemAdapter (OpenXR + Input System)

* Build \*\*`InputAction`\*\*s in code from a small mapping (left/right controller, button/axis), add interactions like `tap`, `multiTap`, `hold`. Subscribe to **`started` / `performed` / `canceled`** and publish `InputActionEvent` to the bus. ([Unity Documentation][5])
* Unity ships OpenXR controller layouts you bind to with the Input System (e.g., `<XRController>{RightHand}/primaryButton`). ([Unity Documentation][16])

### 3.3 TimerScheduler

* Interval triggers and Cron expressions (Cronos). Publish `TimerEvent` per automation “id/time”. DST safe. ([GitHub][9])

### 3.4 StateStore

* Minimal working memory: key/value typed store (`bool`, `number`, `string`, `Vector3`, etc.) + change notifications (`StateChangedEvent<T>`).

### 3.5 ServiceRegistry

* Maps action names to **use‑case code** (e.g., `haptics.pulse`, `combat.toggle_shield`, `ui.toast`). Pure C# services; adapters (haptics, VFX, SFX) live here.

### 3.6 AutomationEngine

* For each automation, composes **R3** streams from triggers, evaluates **Conditions**, and executes **Actions** under a **run mode** (`single`, `restart`, `queued`, `parallel`) using **UniTask** for async steps/delays. ([GitHub][1])

### 3.7 ConfigLoader

* Downloads or reads JSON, validates with JSON Schema, diffs against active automations, applies hot reload.

### 3.8 Bootstrapping (no MonoBehaviour)

* Use **VContainer** to create a **pure C# entry point** (`IStartable` or `IAsyncStartable`), register all subsystems, and start them at app boot via `RuntimeInitializeOnLoadMethod` or VContainer root scope. ([VContainer][4], [Unity Documentation][17])

---

## 4) Unity‑side integration without MonoBehaviours

* **Entry point:** `RuntimeInitializeOnLoadMethod` sets up VContainer RootLifetimeScope programmatically (or one small bootstrap component if you prefer), then registers **pure C#** “presenters/controllers” as entry points. ([Unity Documentation][17], [VContainer][4])
* **PlayerLoop:** If you need a custom tick (rare here), insert one safely via **PlayerLoop customization**; UniTask also exposes PlayerLoop helpers. ([Unity Documentation][18], [Cysharp][3])

---

## 5) Key types (sketch)

> Namespaces trimmed for brevity. These compile in standard C# (Unity 2022+). All domain logic is MonoBehaviour‑free; only the Input adapter touches UnityEngine.

### 5.1 Events

```csharp
public interface IAppEvent { DateTime UtcTime { get; } }

public sealed class InputActionEvent : IAppEvent {
    public DateTime UtcTime { get; init; }
    public string ActionId { get; init; }        // e.g., "xr/a_button"
    public string Phase { get; init; }           // started|performed|canceled
    public string Hand { get; init; }            // left|right|none
    public string ControlPath { get; init; }     // "<XRController>{RightHand}/primaryButton"
    public object? Value { get; init; }          // float/Vector2/bool etc.
}

public sealed class TimerEvent : IAppEvent {
    public DateTime UtcTime { get; init; }
    public string TimerId { get; init; }         // "every_1s" or "cron:*/5 * * * *"
}

public sealed class StateChangedEvent<T> : IAppEvent {
    public DateTime UtcTime { get; init; }
    public string Entity { get; init; }          // e.g., "player.armed"
    public T? Old { get; init; }
    public T? New { get; init; }
}
```

### 5.2 EventBus (MessagePipe)

```csharp
public interface IEventBus {
    void Publish<T>(T message) where T : IAppEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : IAppEvent;
}

public sealed class MessagePipeEventBus : IEventBus {
    readonly IServiceProvider _provider;
    public MessagePipeEventBus(IServiceProvider provider) => _provider = provider;
    public void Publish<T>(T message) where T : IAppEvent =>
        _provider.GetRequiredService<IPublisher<T>>().Publish(message);
    public IDisposable Subscribe<T>(Action<T> handler) where T : IAppEvent =>
        _provider.GetRequiredService<ISubscriber<T>>().Subscribe(handler);
}
```

> MessagePipe gives you high‑performance typed pub/sub and nice VContainer integration. ([GitHub][8], [VContainer][19])

### 5.3 Automation model (strictly typed)

```csharp
public enum RunMode { Single, Restart, Queued, Parallel }

public sealed class AutomationConfig {
    public string Id { get; init; }
    public string Alias { get; init; }
    public RunMode Run_Mode { get; init; } = RunMode.Queued;
    public int Max_Concurrency { get; init; } = 1;
    public List<TriggerConfig> Triggers { get; init; }
    public List<ConditionConfig> Conditions { get; init; } = new();
    public List<ActionConfig> Actions { get; init; }
}

public abstract record TriggerConfig(string Type);
public abstract record ConditionConfig(string Type);
public abstract record ActionConfig(string Type);

// Triggers
public sealed record InputActionTriggerConfig(
    string Action, string Phase = "performed", string? Interactions = null, string? Hand = null
) : TriggerConfig("input_action");

public sealed record IntervalTriggerConfig(int Ms) : TriggerConfig("interval");
public sealed record CronTriggerConfig(string Expression, string? Timezone = null) : TriggerConfig("cron");

// Conditions
public sealed record BoolStateConditionConfig(string Entity, bool Is)
    : ConditionConfig("bool_state");
public sealed record CooldownConditionConfig(string Key, double Seconds)
    : ConditionConfig("cooldown");
public sealed record AndConditionConfig(List<ConditionConfig> All)
    : ConditionConfig("and");
public sealed record OrConditionConfig(List<ConditionConfig> Any)
    : ConditionConfig("or");

// Actions
public sealed record CallServiceActionConfig(string Service, object? Data = null)
    : ActionConfig("call_service");
public sealed record DelayActionConfig(int Ms)
    : ActionConfig("delay");
public sealed record SequenceActionConfig(List<ActionConfig> Steps)
    : ActionConfig("sequence");
public sealed record ChooseActionConfig(List<(ConditionConfig When, List<ActionConfig> Do)> Branches, List<ActionConfig>? Default = null)
    : ActionConfig("choose");
```

### 5.4 Automation runtime (R3 + UniTask)

```csharp
public sealed class Automation {
    readonly AutomationConfig _cfg;
    readonly IEventBus _bus;
    readonly IConditionEvaluator _cond;
    readonly IActionExecutor _actions;
    readonly CompositeDisposable _subscriptions = new();
    int _running;
    readonly Queue<Func<CancellationToken, UniTask>> _queue = new();

    public Automation(AutomationConfig cfg, IEventBus bus, IConditionEvaluator cond, IActionExecutor actions) {
        _cfg = cfg; _bus = bus; _cond = cond; _actions = actions;
    }

    public void Start() {
        foreach (var t in _cfg.Triggers) {
            var o = BuildTrigger(t); // returns Observable<Unit>
            _subscriptions.Add(o.SubscribeAwait(async (_, ct) => await HandleFire(ct)));
        }
    }
    public void Stop() => _subscriptions.Dispose();

    Observable<Unit> BuildTrigger(TriggerConfig t) => t switch {
        InputActionTriggerConfig it => ObserveInput(it),
        IntervalTriggerConfig iv   => Observable.Interval(TimeSpan.FromMilliseconds(iv.Ms)).Select(_ => Unit.Default),
        CronTriggerConfig cron     => CronStream(cron),
        _ => throw new NotSupportedException(t.Type)
    };

    Observable<Unit> ObserveInput(InputActionTriggerConfig t) {
        // Adapt bus -> observable: we bridge MessagePipe subscription to an R3 stream
        return Observable.Create<Unit>(observer => {
            return _bus.Subscribe<InputActionEvent>(e => {
                if (!string.Equals(e.ActionId, t.Action, StringComparison.OrdinalIgnoreCase)) return;
                if (!string.Equals(e.Phase, t.Phase, StringComparison.OrdinalIgnoreCase)) return;
                if (t.Hand != null && !string.Equals(e.Hand, t.Hand, StringComparison.OrdinalIgnoreCase)) return;
                observer.OnNext(Unit.Default);
            });
        });
    }

    Observable<Unit> CronStream(CronTriggerConfig cfg) {
        var cron = Cronos.CronExpression.Parse(cfg.Expression);
        var tz = cfg.Timezone != null ? TimeZoneInfo.FindSystemTimeZoneById(cfg.Timezone) : TimeZoneInfo.Utc;
        return Observable.Create<Unit>(observer => {
            var disposed = false;
            async UniTask Loop() {
                while (!disposed) {
                    var now = DateTimeOffset.UtcNow;
                    var next = cron.GetNextOccurrence(now, tz);
                    if (next == null) break;
                    var delay = next.Value - now;
                    await UniTask.Delay(delay, DelayType.DeltaTime);
                    observer.OnNext(Unit.Default);
                }
            }
            var task = Loop();
            return Disposable.Create(() => { disposed = true; });
        });
    }

    async UniTask HandleFire(CancellationToken ct) {
        // Run-mode behavior
        switch (_cfg.Run_Mode) {
            case RunMode.Single:
                if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
                try { if (await _cond.EvaluateAll(_cfg.Conditions, ct)) await _actions.Run(_cfg.Actions, ct); }
                finally { Interlocked.Exchange(ref _running, 0); }
                break;
            case RunMode.Restart:
                Interlocked.Exchange(ref _running, 1);
                if (await _cond.EvaluateAll(_cfg.Conditions, ct)) await _actions.Run(_cfg.Actions, ct);
                Interlocked.Exchange(ref _running, 0);
                break;
            case RunMode.Queued:
                _queue.Enqueue(async c => { if (await _cond.EvaluateAll(_cfg.Conditions, c)) await _actions.Run(_cfg.Actions, c); });
                if (Interlocked.CompareExchange(ref _running, 1, 0) == 0) {
                    try {
                        while (_queue.TryDequeue(out var job)) await job(ct);
                    } finally { Interlocked.Exchange(ref _running, 0); }
                }
                break;
            case RunMode.Parallel:
                if (await _cond.EvaluateAll(_cfg.Conditions, ct)) _ = _actions.Run(_cfg.Actions, ct);
                break;
        }
    }
}
```

### 5.5 Conditions & Actions (examples)

```csharp
public interface IConditionEvaluator {
    UniTask<bool> EvaluateAll(IReadOnlyList<ConditionConfig> conditions, CancellationToken ct);
}
public sealed class ConditionEvaluator : IConditionEvaluator {
    readonly IStateStore _state;
    readonly ICooldownStore _cooldowns;
    public ConditionEvaluator(IStateStore state, ICooldownStore cooldowns) { _state = state; _cooldowns = cooldowns; }

    public async UniTask<bool> EvaluateAll(IReadOnlyList<ConditionConfig> conditions, CancellationToken ct) {
        foreach (var c in conditions) if (!await Eval(c, ct)) return false;
        return true;
    }

    async UniTask<bool> Eval(ConditionConfig c, CancellationToken ct) => c switch {
        BoolStateConditionConfig b => _state.Get<bool>(b.Entity) == b.Is,
        CooldownConditionConfig cd => _cooldowns.TryConsume(cd.Key, TimeSpan.FromSeconds(cd.Seconds)),
        AndConditionConfig and     => await All(and.All, ct),
        OrConditionConfig or       => await Any(or.Any, ct),
        _ => throw new NotSupportedException(c.Type)
    };

    UniTask<bool> All(IEnumerable<ConditionConfig> cs, CancellationToken ct)
        => cs.Select(c => Eval(c, ct)).WhenAll().ContinueWith(results => results.All(x => x));
    UniTask<bool> Any(IEnumerable<ConditionConfig> cs, CancellationToken ct)
        => cs.Select(c => Eval(c, ct)).WhenAll().ContinueWith(results => results.Any(x => x));
}

public interface IActionExecutor { UniTask Run(IReadOnlyList<ActionConfig> steps, CancellationToken ct); }

public sealed class ActionExecutor : IActionExecutor {
    readonly IServiceRegistry _services;
    public ActionExecutor(IServiceRegistry services) { _services = services; }

    public async UniTask Run(IReadOnlyList<ActionConfig> steps, CancellationToken ct) {
        foreach (var s in steps) {
            switch (s) {
                case CallServiceActionConfig call: await _services.Invoke(call.Service, call.Data, ct); break;
                case DelayActionConfig d: await UniTask.Delay(d.Ms, cancellationToken: ct); break;
                case SequenceActionConfig seq: await Run(seq.Steps, ct); break;
                case ChooseActionConfig choose:
                    foreach (var (when, doSteps) in choose.Branches)
                        if (await new ConditionEvaluator(_services.State, _services.Cooldowns).EvaluateAll(new[] { when }, ct)) { await Run(doSteps, ct); goto Next; }
                    if (choose.Default is { } def) await Run(def, ct);
                    Next: break;
                default: throw new NotSupportedException(s.Type);
            }
        }
    }
}
```

---

## 6) Input (Quest/OpenXR) without MonoBehaviours

> The Input System provides **`InputAction`** with `started/performed/canceled` phases; you can build actions and bindings **in code** and subscribe to callbacks. Use **OpenXR** controller layouts. ([Unity Documentation][5])

```csharp
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public sealed class InputSystemAdapter {
    readonly IEventBus _bus;
    readonly Dictionary<string, InputAction> _actions = new();

    public InputSystemAdapter(IEventBus bus) => _bus = bus;

    public void RegisterButton(string id, string controlPath, string? interactions = null) {
        var action = new InputAction(name: id, type: InputActionType.Button);
        var b = action.AddBinding(controlPath);
        if (!string.IsNullOrEmpty(interactions)) b.WithInteractions(interactions); // e.g., "multiTap(tapCount=2,tapTime=0.3)"
        action.started += ctx => Publish(id, "started", ctx);
        action.performed += ctx => Publish(id, "performed", ctx);
        action.canceled += ctx => Publish(id, "canceled", ctx);
        action.Enable();
        _actions[id] = action;
    }

    void Publish(string id, string phase, InputAction.CallbackContext ctx) {
        _bus.Publish(new InputActionEvent {
            UtcTime = DateTime.UtcNow,
            ActionId = id,
            Phase = phase,
            ControlPath = ctx.control?.path ?? "",
            Hand = ctx.control?.path.Contains("{RightHand}") == true ? "right"
                   : ctx.control?.path.Contains("{LeftHand}") == true ? "left" : "none",
            Value = TryReadValue(ctx)
        });
    }

    static object? TryReadValue(InputAction.CallbackContext ctx) {
        // Minimal, extend as needed
        try { return ctx.ReadValue<float>(); } catch { }
        try { return ctx.ReadValue<Vector2>(); } catch { }
        return null;
    }
}
```

**Example bindings (Quest / OpenXR):**

```csharp
// Right controller A button double-tap
adapter.RegisterButton("xr/a_button", "<XRController>{RightHand}/primaryButton", "multiTap(tapCount=2,tapTime=0.3)");
// Left trigger press
adapter.RegisterButton("xr/left_trigger", "<XRController>{LeftHand}/trigger");
```

> OpenXR provides controller layouts usable with Input System; interactions like `tap`/`multiTap` come built‑in. ([Unity Documentation][16])

---

## 7) Bootstrapping (VContainer)

```csharp
using VContainer;
using VContainer.Unity;
using MessagePipe;
using Cysharp.Threading.Tasks;

public sealed class RootInstaller : IStartable, IAsyncStartable, IDisposable {
    readonly IContainer _container;
    readonly AutomationManager _manager;
    public RootInstaller(IContainer container, AutomationManager manager) {
        _container = container; _manager = manager;
    }
    public void Start() { /* optional sync startup */ }
    public async UniTask StartAsync(CancellationToken _) => await _manager.LoadAndStartAsync();
    public void Dispose() => _manager.Dispose();
}

public static class Bootstrap {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() {
        var builder = new ContainerBuilder();

        // MessagePipe setup
        var options = builder.RegisterMessagePipe();
        builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

        // Core singletons
        builder.Register<IEventBus, MessagePipeEventBus>(Lifetime.Singleton);
        builder.Register<IStateStore, InMemoryStateStore>(Lifetime.Singleton);
        builder.Register<ICooldownStore, InMemoryCooldownStore>(Lifetime.Singleton);
        builder.Register<IActionExecutor, ActionExecutor>(Lifetime.Singleton);
        builder.Register<IConditionEvaluator, ConditionEvaluator>(Lifetime.Singleton);
        builder.Register<ConfigLoader>(Lifetime.Singleton);
        builder.Register<InputSystemAdapter>(Lifetime.Singleton);
        builder.Register<TimerScheduler>(Lifetime.Singleton);
        builder.Register<ServiceRegistry>(Lifetime.Singleton);
        builder.Register<AutomationManager>(Lifetime.Singleton);

        // Entry point (pure C#)
        builder.RegisterEntryPoint<RootInstaller>();

        builder.Build(); // VContainer schedules entry points on its own PlayerLoop system
    }
}
```

> VContainer supports **plain C# entry points** and integrates with UniTask & MessagePipe, letting you keep gameplay logic out of MonoBehaviours. ([VContainer][4])

---

## 8) Config loading & validation (LLM‑friendly)

* **Schema‑first:** define a JSON Schema that encodes allowed trigger/condition/action types, enums (phases/run\_mode), and required fields. Validate with **JsonSchema.Net** before deserialization and log structured errors (**ZLogger**). ([json-everything][6], [GitHub][13])
* **Deserialization:** **Newtonsoft.Json** (Unity package) is standard and IL2CPP‑ready. ([Unity Documentation][10])
* **Hot reload:** watch a file path (PC) or poll a URL (Quest) with `UnityWebRequest` via UniTask; diff by `id` to add/remove/update automations.

---

## 9) Example end‑to‑end scenario

**Goal:** Double‑tap A (right hand) to toggle a shield, only if `player.armed == true`, with a 2s cooldown, and haptics confirmation.

1. **Bindings:**

```csharp
adapter.RegisterButton("xr/a_button", "<XRController>{RightHand}/primaryButton", "multiTap(tapCount=2,tapTime=0.3)");
```

2. **Automation JSON:** (same as in §2)

3. **Services:**

```csharp
public sealed class ServiceRegistry : IServiceRegistry {
    readonly IStateStore State;
    readonly IHaptics Haptics;
    readonly ICombat Combat;
    public ServiceRegistry(IStateStore s, IHaptics h, ICombat c) { State = s; Haptics = h; Combat = c; }
    public Task Invoke(string service, object? data, CancellationToken ct) => service switch {
        "combat.toggle_shield" => Combat.ToggleShieldAsync(ct),
        "haptics.pulse"        => Haptics.Pulse(((dynamic)data).hand, (float)((dynamic)data).amplitude, (int)((dynamic)data).duration_ms, ct),
        _ => throw new InvalidOperationException(service)
    };
}
```

4. **Run‑time:** `AutomationEngine` composes an observable from `InputActionEvent` → evaluates conditions (`BoolState`, `Cooldown`) → executes `CallServiceAction` steps with UniTask.

---

## 10) Why these choices

* **R3** is the modern Rx tuned for Unity; it’s the intended migration path from UniRx and avoids Rx.NET pitfalls (error tear‑down, scheduler overhead). ([GitHub][1], [Medium][2])
* **UniTask** gives you allocation‑free async on PlayerLoop—cleaner and faster than coroutines. ([GitHub][7])
* **VContainer** lets you run everything from **pure C#** entry points; MonoBehaviours become thin views/adapters only when needed.
* **MessagePipe** is a fast, type‑safe bus integrated with DI; ideal for your event backbone.
* **Input System + OpenXR** is the Unity‑native path for Quest controllers; actions expose `started/performed/canceled` to drive triggers.
* **Cronos** and **JsonSchema.Net** solve timers and LLM safety guardrails respectively.
* **Newtonsoft.Json** is ubiquitous and available as an official Unity package (or IL2CPP‑ready forks).

> **AOT constraints:** avoid dynamic code‑gen/`dynamic`/`Reflection.Emit` due to IL2CPP limits; use typed predicates and services instead.

---

## 11) Implementation checklist

1. **Packages:** add R3, UniTask, VContainer, MessagePipe, JsonSchema.Net, ZLogger; enable Input System + OpenXR plugin.
2. **Core projects (asmdefs):**

   * `Automation.Core` (no UnityEngine reference)
   * `Automation.Unity` (adapters for Input/Haptics/etc.)
3. **Event Bus & DI:** wire MessagePipe & VContainer; expose `IEventBus`.
4. **Adapters:** map OpenXR controls to action IDs in code; publish `InputActionEvent`.
5. **State & Services:** implement `IStateStore`, `ICooldownStore`, `ServiceRegistry` with your domain services.
6. **Automation runtime:** build triggers → conditions → actions; implement run modes.
7. **Config:** define JSON Schema; validate; deserialize; hot‑reload.
8. **Boot:** VContainer root + `RegisterEntryPoint` for `AutomationManager`.
9. **Diagnostics:** add ZLogger; optionally expose a “Trace” panel listing last N rule firings.

---

## 12) Optional enhancements

* **Per‑action timeouts, retries, and cancellation** via UniTask tokens.
* **Rate‑limit/debounce operators** on trigger streams (R3 `Debounce`, `ThrottleLast`, etc.) to tame noisy inputs.
* **Human‑readable cron** descriptions (cron‑descriptor).
* **LLM prompting:** ship your JSON Schema and a few examples; validate before applying.

---

### Quick references

* **R3 (Unity‑ready Rx, successor to UniRx)** — repo & overview.
* **UniTask** — PlayerLoop‑based async; replaces coroutines.
* **VContainer** — DI with pure C# entry points; UniTask/MessagePipe integrations.
* **MessagePipe** — high‑performance pub/sub for .NET & Unity.
* **Input System actions & phases** — `started/performed/canceled`.
* **OpenXR controller layouts** — bind Input System actions to Quest controllers.
* **Cronos** — cron expressions with timezone support.
* **Newtonsoft.Json (Unity package)** — JSON parsing.
* **JsonSchema.Net** — validating automation JSON.
* **IL2CPP/AOT restrictions** — avoid `dynamic`/Emit.

---

If you want, I can tailor the **JSON Schema** and the **InputAction mappings** to your exact Quest interactions (buttons, grips, axes/thresholds, multitap/hold) and generate seed automations for your current game scene.

[1]: https://github.com/Cysharp/R3 "GitHub - Cysharp/R3: The new future of dotnet/reactive and UniRx."
[2]: https://neuecc.medium.com/r3-a-new-modern-reimplementation-of-reactive-extensions-for-c-cf29abcc5826?utm_source=chatgpt.com "R3 — A New Modern Reimplementation of Reactive ..."
[3]: https://cysharp.github.io/UniTask/api/Cysharp.Threading.Tasks.PlayerLoopHelper.html?utm_source=chatgpt.com "PlayerLoopHelper Class | UniTask"
[4]: https://vcontainer.hadashikick.jp/ "About | VContainer"
[5]: https://docs.unity3d.com/Packages/com.unity.inputsystem%401.0/manual/Actions.html?utm_source=chatgpt.com "Actions | Input System | 1.0.2"
[6]: https://docs.json-everything.net/schema/basics/?utm_source=chatgpt.com "JsonSchema.Net Basics - json-everything"
[7]: https://github.com/Cysharp/UniTask?utm_source=chatgpt.com "Cysharp/UniTask: Provides an efficient allocation ..."
[8]: https://github.com/Cysharp/MessagePipe?utm_source=chatgpt.com "Cysharp/MessagePipe: High performance in-memory ..."
[9]: https://github.com/HangfireIO/Cronos?utm_source=chatgpt.com "HangfireIO/Cronos"
[10]: https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json%403.0/?utm_source=chatgpt.com "Newtonsoft Json Unity Package"
[11]: https://www.npmjs.com/package/jillejr.newtonsoft.json-for-unity?utm_source=chatgpt.com "jillejr.newtonsoft.json-for-unity"
[12]: https://json-schema.org/docs?utm_source=chatgpt.com "Docs"
[13]: https://github.com/Cysharp/ZLogger?utm_source=chatgpt.com "Cysharp/ZLogger"
[14]: https://docs.unity3d.com/6000.2/Documentation/Manual/scripting-restrictions.html?utm_source=chatgpt.com "Manual: Scripting restrictions"
[15]: https://discussions.unity.com/t/reflection-emit-not-supported-because-this-lines-of-code/676079?utm_source=chatgpt.com "Reflection.Emit not supported, because this lines of code?"
[16]: https://docs.unity3d.com/Packages/com.unity.xr.openxr%400.1/manual/input.html?utm_source=chatgpt.com "Input in Unity OpenXR | OpenXR Plugin | 0.1.2-preview.2"
[17]: https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html?utm_source=chatgpt.com "Scripting API: RuntimeInitializeOnLoadMethodAttribute"
[18]: https://docs.unity3d.com/6000.1/Documentation/Manual/player-loop-customizing.html?utm_source=chatgpt.com "Customizing the Player loop"
[19]: https://vcontainer.hadashikick.jp/integrations/messagepipe?utm_source=chatgpt.com "VContainer + MessagePipe"
