Below is a concrete, **Unity‑ready implementation plan** for a fast, flexible **ECA (Event‑Condition‑Action)** package in C#. It avoids `MonoBehaviour` and `ScriptableObject`, uses **Newtonsoft.Json** for the artifact, and **UniTask** for any async work (e.g., policy loading, shadow‑runs). I include architecture, data shapes, a tiny compiler to deterministic runtime tables + HFSM, CEP (short temporal windows), hot‑swap, PlayerLoop integration **without** MonoBehaviours, and example code.

---

## 0) Goals & constraints

* **L0/L1 hot path**: deterministic, constant or tightly bounded time; **no allocations per tick**.
* **Artifact**: human‑readable JSON (via **Newtonsoft.Json**).
* **Agents**: propose policy updates offline; engine compiles/validates and **atomically swaps** runtime data.
* **Unity**: no MonoBehaviours or Scriptables; integrate via **PlayerLoop** and/or **UniTask**.
* **Explainability**: return a small “why” record for logs/AAC.

---

## 1) Package layout (single assembly or asmdef group)

```
EcaCore/                   (pure .NET Standard 2.1 or .NET 6 where possible)
  EcaModel.cs              // JSON-facing DTOs (Policy/Rule/Event/Condition/Action)
  EcaCompiler.cs           // Policy → CompiledRuntime (tables + HFSM + CEP)
  EcaRuntime.cs            // Evaluate(), state machines, decision tables
  EcaCep.cs                // ring buffers, dwell/hysteresis
  EcaPolicyManager.cs      // load/verify/shadow/commit/rollback (UniTask)
  EcaExplain.cs            // small explain records
  EcaContext.cs            // IContextProvider, symbol table, value store
  EcaUtils.cs              // pooling, time, math helpers

EcaUnityIntegration/       (tiny; optional)
  EcaPlayerLoop.cs         // injects a Tick without MonoBehaviour
  EcaUnityTime.cs          // time provider mapping to Unity's PlayerLoop (no MB)
```

> Keep `EcaCore` free of `UnityEngine` references. Use `System.Numerics` where needed.

---

## 2) JSON artifact (authoring model) — minimal & type‑safe

### 2.1 DTOs (Newtonsoft.Json)

```csharp
using Newtonsoft.Json;

public sealed class EcaPolicy
{
    [JsonProperty("policy_id")] public string PolicyId { get; set; } = "";
    [JsonProperty("version")] public int Version { get; set; }
    [JsonProperty("issuer")] public string Issuer { get; set; } = "";
    [JsonProperty("created_at_utc")] public string CreatedAtUtc { get; set; } = "";
    [JsonProperty("ttl_seconds")] public int? TtlSeconds { get; set; }
    [JsonProperty("scope")] public EcaScope Scope { get; set; } = new();
    [JsonProperty("rules")] public List<EcaRule> Rules { get; set; } = new();
    [JsonProperty("notes")] public string? Notes { get; set; }
}

public sealed class EcaScope
{
    [JsonProperty("autonomy_level")] public string AutonomyLevel { get; set; } = "Manual"; // Manual/Assisted/CoPilot/Autonomous
    [JsonProperty("place_type")] public string? PlaceType { get; set; } // e.g., "museum"
    [JsonProperty("mode")] public string? Mode { get; set; } // e.g., "camera"
}

public sealed class EcaRule
{
    [JsonProperty("rule_id")] public string RuleId { get; set; } = "";
    [JsonProperty("priority")] public int Priority { get; set; } = 0; // higher wins after specificity
    [JsonProperty("tier_hint")] public string TierHint { get; set; } = "L1"; // L0/L1/L2
    [JsonProperty("event")] public string Event { get; set; } = ""; // e.g., "gesture.pinch"
    [JsonProperty("conditions")] public List<EcaCondition> Conditions { get; set; } = new();
    [JsonProperty("roi")] public EcaRoi? Roi { get; set; }
    [JsonProperty("dwell_ms")] public int? DwellMs { get; set; }
    [JsonProperty("action")] public EcaAction Action { get; set; } = new();
    [JsonProperty("explain")] public string? Explain { get; set; }
}

public sealed class EcaCondition
{
    [JsonProperty("key")] public string Key { get; set; } = "";      // e.g., "ambient.light.lux"
    [JsonProperty("op")]  public string Op { get; set; } = "gte";    // eq, ne, lt, lte, gt, gte, in
    [JsonProperty("value")] public object? Value { get; set; }       // number/bool/string/array
}

public sealed class EcaRoi
{
    [JsonProperty("type")] public string Type { get; set; } = "cone"; // rect/ellipse/cone
    [JsonProperty("params")] public Dictionary<string, float> Params { get; set; } = new(); // e.g., { "min_cm":15, "max_cm":35, "half_angle_deg":15 }
    [JsonProperty("dynamic_anchor")] public string? DynamicAnchor { get; set; } // e.g., "hand.right"
}

public sealed class EcaAction
{
    [JsonProperty("route_tier")] public string RouteTier { get; set; } = "L1"; // L0/L1
    [JsonProperty("name")] public string Name { get; set; } = ""; // e.g., "capture.suggest" or "volume.step"
    [JsonProperty("params")] public Dictionary<string, object>? Params { get; set; }
    [JsonProperty("outputs")] public List<string>? Outputs { get; set; } // e.g., ["haptic.micro","visual.badge"]
}
```

### 2.2 Example JSON (museum assisted capture)

```json
{
  "policy_id": "museum_cam_v3",
  "version": 3,
  "issuer": "agent.photography",
  "created_at_utc": "2025-09-04T18:00:00Z",
  "scope": { "autonomy_level": "Assisted", "place_type": "museum", "mode": "camera" },
  "rules": [
    {
      "rule_id": "pinch_confirm",
      "priority": 100,
      "tier_hint": "L1",
      "event": "gesture.pinch",
      "conditions": [
        { "key": "pinch.confidence", "op": "gte", "value": 0.85 },
        { "key": "pose.stable_ms", "op": "gte", "value": 80 }
      ],
      "roi": { "type": "cone", "params": { "min_cm": 15, "max_cm": 35, "half_angle_deg": 15 } },
      "dwell_ms": 120,
      "action": { "route_tier": "L1", "name": "capture.confirm", "outputs": ["haptic.micro","visual.tick"] },
      "explain": "Confirm capture when pinch is confident and steady within near-hand cone."
    }
  ]
}
```

> JSON is the authoring format. **Do not evaluate it directly at runtime**. Compile it to a deterministic structure (below).

---

## 3) Compiler → deterministic runtime

**Inputs:** `EcaPolicy` (JSON) + a **SymbolTable** for fast key lookups.
**Outputs:** `CompiledPolicy` with:

* **Decision tables** per event (O(1) indexing, priority/specificity pre‑sorted).
* **HFSM** per domain (stateful flows like “gesture → confirmed → cooldown”).
* **CEP** descriptors (dwell windows, debounce, minimal ring buffers).
* **Immutable guardrails check** (L0/L1 latency budgets, no cloud dependencies).

### 3.1 Compiled runtime types

```csharp
// Small ids avoid string lookups in hot path.
public readonly struct SymId { public readonly int Value; public SymId(int v) => Value = v; public static implicit operator int(SymId s) => s.Value; }

public sealed class CompiledPolicy
{
    public readonly int Version;
    public readonly string PolicyId;
    public readonly DecisionTable[] Tables;         // indexed by EventId
    public readonly HfsmDomain[] Domains;           // optional per feature area
    public readonly CepDescriptor Cep;              // dwell/debounce configs
    public readonly SymbolTable Symbols;
    public CompiledPolicy(int version, string id, DecisionTable[] tables, HfsmDomain[] domains, CepDescriptor cep, SymbolTable symbols)
    { Version = version; PolicyId = id; Tables = tables; Domains = domains; Cep = cep; Symbols = symbols; }
}

public sealed class DecisionTable
{
    // Rows are pre-sorted by (specificity desc, priority desc, stable tie-break)
    public readonly RuleRow[] Rows;
    public DecisionTable(RuleRow[] rows) { Rows = rows; }
}

public readonly struct RuleRow
{
    public readonly int RuleIndex;
    public readonly short Priority;
    public readonly short Specificity; // number of conditions + ROI + dwell
    public readonly byte Tier;         // 0=L0,1=L1,2=L2 but we only execute L0/L1 here
    public readonly ActionDescriptor Action;
    public readonly ConditionBlock Conds; // compiled, no alloc
    public RuleRow(int idx, short prio, short spec, byte tier, in ActionDescriptor act, in ConditionBlock conds)
    { RuleIndex = idx; Priority = prio; Specificity = spec; Tier = tier; Action = act; Conds = conds; }
}

public readonly struct ActionDescriptor
{
    public readonly SymId Name;              // e.g., "capture.confirm"
    public readonly SymId[] Outputs;         // e.g., ["haptic.micro"]
    public readonly short ParamOffset;       // index into a packed param array
    public readonly short ParamCount;
    public ActionDescriptor(SymId name, SymId[] outputs, short off, short count)
    { Name = name; Outputs = outputs; ParamOffset = off; ParamCount = count; }
}

// Compact, non-allocating condition block
public readonly struct ConditionBlock
{
    public readonly CondOp[] Ops;            // array of ops (==, >=, in, etc.)
    public readonly CondRef[] Refs;          // symbol references
    public readonly CondValue[] Values;      // typed literals
    public ConditionBlock(CondOp[] ops, CondRef[] refs, CondValue[] vals) { Ops = ops; Refs = refs; Values = vals; }
}

public enum CondOp : byte { Eq, Ne, Lt, Lte, Gt, Gte, InSet }
public readonly struct CondRef { public readonly SymId Key; public readonly byte TypeTag; /* 0=float,1=int,2=bool,3=string,4=set */ public CondRef(SymId k, byte t){ Key=k; TypeTag=t; } }
public readonly struct CondValue { public readonly float F; public readonly int I; public readonly bool B; public readonly int SetIndex; public CondValue(float f,int i,bool b,int setIdx){F=f;I=i;B=b;SetIndex=setIdx;} }

// CEP / dwell
public sealed class CepDescriptor
{
    public readonly int DefaultDwellMs; public readonly int GraceGapMs;
    public CepDescriptor(int defDwellMs=100, int graceGapMs=16) { DefaultDwellMs = defDwellMs; GraceGapMs = graceGapMs; }
}

// Minimal HFSM (optional per domain)
public sealed class HfsmDomain
{
    public readonly SymId DomainId;
    public readonly HfsmState[] States;
    public readonly short InitialStateIndex;
    public HfsmDomain(SymId id, HfsmState[] states, short initial) { DomainId = id; States = states; InitialStateIndex = initial; }
}
public readonly struct HfsmState { public readonly SymId Name; public readonly short[] Transitions; /* indexes into DecisionTable, or inline */ /* ... */ public HfsmState(SymId name, short[] transitions){Name=name;Transitions=transitions;} }
```

### 3.2 Compiler sketch

* Build a **SymbolTable** for all strings → int ids.
* For each rule: compute **specificity** (count conditions + ROI + dwell).
* Lower **ROI** into simple math checks (rect/ellipse/cone parameters).
* Lower **dwell** to CEP flags (window length).
* Normalize **priority** and stable tiebreak (e.g., hash of rule\_id).
* **Sort** rows by `(specificity desc, priority desc, tieBreak asc)` per event.
* **Validate** immutables: L0 rules must have no external refs; L1 no cloud I/O; both must fit budget by static estimate (condition count, ROI check, no dynamic alloc).

---

## 4) Runtime: constant‑time evaluation

### 4.1 Core evaluator

```csharp
public readonly struct EcaEvent
{
    public readonly SymId EventId;
    public readonly long TimestampTicks; // monotonic
    public readonly PayloadView Payload; // struct view into preallocated storage
    public EcaEvent(SymId id, long t, in PayloadView p){ EventId=id; TimestampTicks=t; Payload=p; }
}

public interface IContextProvider
{
    // Returns a read-only view of current context values (floats/ints/bools) by SymId
    bool TryGetFloat(SymId key, out float value);
    bool TryGetInt(SymId key, out int value);
    bool TryGetBool(SymId key, out bool value);
    bool IsInRoi(in EcaRoiRuntime roi, in PayloadView payload, IContextProvider ctx);
}

public sealed class EcaEngine
{
    private volatile CompiledPolicy _current; // atomic swap for hot updates
    private readonly DwellTracker _dwell;
    private readonly IEcaClock _clock;

    public EcaEngine(CompiledPolicy compiled, IEcaClock clock)
    { _current = compiled; _clock = clock; _dwell = new DwellTracker(compiled.Cep); }

    // Main entry: constant-time scan over pre-sorted rows for this event
    public bool Evaluate(in EcaEvent ev, IContextProvider ctx, out EcaDecision decision)
    {
        var table = _current.Tables[ev.EventId];
        ref readonly var rows = ref table.Rows;
        var now = _clock.NowTicks;

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (!row.Conds.Pass(ctx)) continue;
            if (!RoiPass(row, ev, ctx)) continue;
            if (!_dwell.Pass(ev, row)) continue;

            // Route only L0/L1 here. L2 is handled by agentic loop elsewhere.
            if (row.Tier <= 1)
            {
                decision = EcaDecision.FromRow(row, ev.EventId, now);
                return true;
            }
        }
        decision = default;
        return false;
    }

    private static bool RoiPass(in RuleRow row, in EcaEvent ev, IContextProvider ctx)
        => RoiCache.TryGet(row.RuleIndex, out var roi) ? ctx.IsInRoi(roi, ev.Payload, ctx) : true;

    public void HotSwap(CompiledPolicy next) => System.Threading.Volatile.Write(ref _current, next);
}
```

**Notes**

* `ConditionBlock.Pass(ctx)` is a tight loop over primitive comparisons; no LINQ; no boxing.
* ROI lookups are pre‑lowered to `EcaRoiRuntime` structs; **no heap alloc** per tick.
* Dwell uses a per‑rule state ring buffer keyed by `(eventId, entityId?)`.

### 4.2 Conditions (tight loop)

```csharp
public static class ConditionBlockExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Pass(this in ConditionBlock block, IContextProvider ctx)
    {
        var ops = block.Ops; var refs = block.Refs; var vals = block.Values;
        for (int i = 0; i < ops.Length; i++)
        {
            var r = refs[i]; var op = ops[i]; var v = vals[i];
            switch (r.TypeTag)
            {
                case 0: if (!ctx.TryGetFloat(r.Key, out var f)) return false; if(!CmpFloat(op,f,v.F)) return false; break;
                case 1: if (!ctx.TryGetInt(r.Key, out var n)) return false; if(!CmpInt(op,n,v.I)) return false; break;
                case 2: if (!ctx.TryGetBool(r.Key, out var b)) return false; if(!CmpBool(op,b,v.B)) return false; break;
                default: return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CmpFloat(CondOp op, float a, float b) => op switch
    { CondOp.Eq => a==b, CondOp.Ne => a!=b, CondOp.Lt => a<b, CondOp.Lte => a<=b, CondOp.Gt => a>b, CondOp.Gte => a>=b, _ => false };
    private static bool CmpInt(CondOp op, int a, int b) => op switch
    { CondOp.Eq => a==b, CondOp.Ne => a!=b, CondOp.Lt => a<b, CondOp.Lte => a<=b, CondOp.Gt => a>b, CondOp.Gte => a>=b, _ => false };
    private static bool CmpBool(CondOp op, bool a, bool b) => op switch
    { CondOp.Eq => a==b, CondOp.Ne => a!=b, _ => false };
}
```

### 4.3 CEP (dwell/debounce) — ring buffer

```csharp
public sealed class DwellTracker
{
    private readonly CepDescriptor _cfg;
    // For simplicity: per RuleIndex last-onset timestamp + stable flag
    private readonly long[] _lastOnsetTicks;
    private readonly byte[] _state; // 0=idle,1=holding

    public DwellTracker(CepDescriptor cfg, int maxRules = 4096)
    { _cfg = cfg; _lastOnsetTicks = new long[maxRules]; _state = new byte[maxRules]; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Pass(in EcaEvent ev, in RuleRow row)
    {
        var dwellMs = EcaCompiler.GetDwellMs(row); // compile-time constant per row
        if (dwellMs <= 0) return true;

        var idx = row.RuleIndex & ( _lastOnsetTicks.Length - 1 ); // simple mask if pow2 sized
        var now = ev.TimestampTicks;
        var onset = _lastOnsetTicks[idx];
        var st = _state[idx];

        if (st == 0) { _lastOnsetTicks[idx] = now; _state[idx] = 1; return false; }
        var elapsedMs = (now - onset) / 10_000; // ticks → ms
        if (elapsedMs + _cfg.GraceGapMs >= dwellMs) { _state[idx] = 0; return true; }
        return false;
    }
}
```

---

## 5) Policy management & hot‑swap (UniTask)

* Load JSON with **Newtonsoft.Json**.
* Verify schema, invariants (immutables for L0/L1).
* **Shadow‑run**: feed recorded events to both current and candidate compiled policies; compare decisions.
* **Atomic swap** on success; keep last‑good for rollback.
* **All long operations use UniTask**, never block the main thread.

```csharp
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

public sealed class EcaPolicyManager
{
    private readonly EcaEngine _engine;
    private readonly JsonSerializerSettings _json = new JsonSerializerSettings
    { TypeNameHandling = TypeNameHandling.None, FloatParseHandling = FloatParseHandling.Double };

    public EcaPolicyManager(EcaEngine engine) { _engine = engine; }

    public async UniTask<bool> LoadAndApplyAsync(string jsonText, IRecordedTrace? trace = null)
    {
        var policy = JsonConvert.DeserializeObject<EcaPolicy>(jsonText, _json);
        if (policy == null) return false;

        var compiled = await UniTask.Run(() => EcaCompiler.Compile(policy));
        if (!Validate(compiled)) return false;

        if (trace != null && !await ShadowRun(trace, compiled)) return false;

        _engine.HotSwap(compiled);
        return true;
    }

    private static bool Validate(CompiledPolicy p) => /* check invariants */ true;

    private async UniTask<bool> ShadowRun(IRecordedTrace trace, CompiledPolicy candidate)
    {
        // Replay events quickly off-thread; ensure same or better outcomes under guardrails
        return await UniTask.Run(() => TraceComparer.Compare(trace, candidate));
    }
}
```

---

## 6) Unity integration **without** MonoBehaviour

Use **PlayerLoop** injection or **UniTask** streams.

```csharp
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Cysharp.Threading.Tasks;

public static class EcaPlayerLoop
{
    [UnityEngine.RuntimeInitializeOnLoadMethod]
    private static void Install()
    {
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopSystem ecaSystem = new PlayerLoopSystem
        {
            type = typeof(EcaPlayerLoop),
            updateDelegate = Tick
        };
        // Insert before Update or in a specific phase
        InsertBefore(ref loop, typeof(Update), ecaSystem);
        PlayerLoop.SetPlayerLoop(loop);

        // Or, alternatively, run an async UniTask loop:
        RunAsyncLoop().Forget();
    }

    private static void Tick()
    {
        // Acquire input events from your pipeline (not shown)
        // var ev = EventSource.TryRead(out var e) ? e : default;
        // EcaEngineInstance.Evaluate(e, ContextProvider, out var decision);
        // Apply outputs per decision...
    }

    private static async UniTaskVoid RunAsyncLoop()
    {
        while (true)
        {
            // Do periodic tasks (policy refresh, shadow runs) without blocking main loop
            await UniTask.Delay(250);
        }
    }

    private static void InsertBefore(ref PlayerLoopSystem loop, System.Type target, PlayerLoopSystem system)
    {
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            ref var sub = ref loop.subSystemList[i];
            if (sub.type == target)
            {
                var list = new List<PlayerLoopSystem>(sub.subSystemList) { system };
                sub.subSystemList = list.ToArray();
                return;
            }
        }
    }
}
```

> This pattern keeps the engine pure and allows **external code** to feed events/call `Evaluate()` each tick.

---

## 7) Context & events: no string lookups at runtime

* Build a **SymbolTable** at compile time; map `"ambient.light.lux"` → `SymId(42)`.
* `IContextProvider` supplies values by `SymId`.
* Normalize all incoming events to `EcaEvent` with `SymId EventId` and a **struct** `PayloadView`.

```csharp
public sealed class SymbolTable
{
    private readonly Dictionary<string,int> _toId = new(1024);
    private readonly List<string> _toStr = new(1024);
    public SymId Intern(string s)
    {
        if (_toId.TryGetValue(s, out var id)) return new SymId(id);
        id = _toStr.Count; _toId[s] = id; _toStr.Add(s); return new SymId(id);
    }
    public string this[SymId id] => _toStr[id.Value];
}
```

---

## 8) ROI checks (fast math)

Lower each ROI to a compact runtime struct:

```csharp
public readonly struct EcaRoiRuntime
{
    public readonly byte Type; // 0=rect,1=ellipse,2=cone
    public readonly float A, B, C; // min/max cm, half-angle, etc.
    public readonly SymId Anchor; // optional dynamic anchor
    public EcaRoiRuntime(byte type, float a, float b, float c, SymId anchor) { Type=type; A=a; B=b; C=c; Anchor=anchor; }
}

public static class RoiEval
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRoi(this IContextProvider ctx, in EcaRoiRuntime roi, in PayloadView payload)
    {
        switch (roi.Type)
        {
            case 2: // cone
                // Example: use hand distance + angle from camera axis
                ctx.TryGetFloat(new SymId(/*hand.distance_cm*/ 101), out var d);
                ctx.TryGetFloat(new SymId(/*hand.angle_deg*/ 102), out var ang);
                return d >= roi.A && d <= roi.B && Math.Abs(ang) <= roi.C;
            // ...rect/ellipse variants...
            default: return true;
        }
    }
}
```

---

## 9) Outputs & application

**Decision → Outputs**: return a small `EcaDecision` that your app applies (not shown here). Keep it data‑only; the app executes platform‑specific effects (haptics/audio/visual), preserving separation of concerns.

```csharp
public readonly struct EcaDecision
{
    public readonly SymId EventId;
    public readonly SymId ActionName;
    public readonly SymId[] Outputs;
    public readonly long DecidedAtTicks;
    public static EcaDecision FromRow(in RuleRow row, SymId evId, long now)
        => new EcaDecision(evId, row.Action.Name, row.Action.Outputs, now);

    private EcaDecision(SymId evId, SymId act, SymId[] outputs, long t)
    { EventId = evId; ActionName = act; Outputs = outputs; DecidedAtTicks = t; }
}
```

---

## 10) Testing & performance checklist

* **Unit tests** for: condition evaluation, ROI checks, dwell timing, priority/specificity ordering, interrupt precedence.
* **Golden trace** tests: replay captured events, ensure stable decisions before/after policy changes.
* **Micro‑benchmarks**: p95 time per `Evaluate()` under worst‑case ruleset for each event type.
* **Allocations**: verify **0 GC** in hot path (Profiler + `GC.GetAllocatedBytesForCurrentThread()` deltas).
* **Concurrency**: hot‑swap (`Volatile.Write`) while ticks read `_current` (no locks in hot path).

---

## 11) Where to use UniTask

* **Load/verify/compile** policy artifacts.
* **Shadow‑run** comparisons and simulations.
* **Periodic fetch** of agent proposals from your agent bus (if applicable).
* **Never** for L0/L1 decision path.

---

## 12) Applying to the museum continuum (end‑to‑end)

* **Manual (L0)**: a hardware shutter event maps to one `RuleRow` → immediate capture & haptic.
* **Assisted (L1)**: CV “salient\_scene\_detected” + ROI + **dwell 200 ms** → `capture.suggest` with micro‑badge; “pinch” (L1) confirms.
* **Co‑pilot (L1/L2)**: the **agent** proposes a new policy that enables candidate capture; **compiler** validates; runtime remains L1 deterministic for confirm/undo gestures.
* **Autonomous (L2 assist, L1 execute)**: agent schedules ECA rules for the next 30 minutes; hot‑swap brings them local; execution stays L1 for fast confirm/steer.

---

## 13) Practical guidance & pitfalls

* Prefer **JSON** over YAML for artifact (predictable parser; fewer surprises).
* Keep **conditions primitive** (no lambdas/expressions at runtime).
* **No reflection** or dynamic dispatch in hot path; avoid LINQ; avoid boxing.
* Preallocate arrays; use `ArrayPool<T>` if you must.
* Time via a **monotonic** clock (don’t use `DateTime.Now`); expose `IEcaClock` for tests.
* If you later need more speed, you can code‑gen the compiled tables to C# partial classes at **Editor time**; but the above structure is usually enough.

---

### TL;DR

* Author policies as **ECA JSON** (Newtonsoft).
* **Compile** to **HFSM + decision tables + CEP** with a **SymbolTable** (no string lookups).
* **Evaluate** in constant time; **0 alloc**; **no MonoBehaviours**.
* Use **UniTask** for policy loading/shadow‑runs; integrate via **PlayerLoop**.
* Agents **propose**; the engine **verifies & hot‑swaps**; L0/L1 stay deterministic.

If you want, I can adapt these snippets into a ready‑to‑drop **Unity asmdef** with a tiny demo (pinch‑to‑confirm and volume step), including NUnit tests and a sample policy file.
