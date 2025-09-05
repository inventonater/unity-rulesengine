Below is a **fully updated, comprehensive implementation plan** (reflecting the simplifications and guardrails we discussed), followed by a **curated set of examples** that exercise the key patterns. It’s written to be drop‑in actionable for Phase 1–4 execution and to keep you well within Quest 72–120 Hz constraints.

---

# Unity Rules Engine (Quest VR) — Implementation Plan

## 0) Executive summary

* **Pattern:** Event‑Condition‑Action (ECA) compiled into a **tiny, explicit per‑rule FSM** for determinism and profiling clarity.
* **Shape:** Standalone **.NET Standard 2.1 core** + **Unity adapter**. The core is Unity‑free and IL2CPP‑safe; the adapter maps Unity/Meta XR to canonical events/services.
* **Schema:** Lean authoring surface (triggers: `event|state|zone|time|pattern`; conditions: `state|numeric|time|expr`; actions: `call|wait|choose|parallel|stop` + optional `set_vars`). Expressions use a **tiny, typed DSL** (no Jinja).
* **Perf:** Zero‑alloc hot paths, **timer wheel** for timeouts, integer IDs everywhere, value‑type events, bounded concurrency.
* **DX:** Strict validation + linter, compiler to a compact **.uar** (Unity Automation Rules) binary, simulator, and “why didn’t this fire?” traces.

---

## 1) Scope & non‑goals

**In scope**

* Reactive automation across input, spatial, UI, VFX/SFX, gameplay glue.
* Complex temporal patterns and simple quest step logic.
* Deterministic execution with explicit concurrency semantics.

**Out of scope (v1)**

* Full behavior trees / GOAP.
* Arbitrary code execution or general scripting beyond the tiny DSL.
* Networked determinism (v1 handles local client only).

---

## 2) Requirements & success criteria

* **Latency:** ≤ **0.5 ms** per rule evaluation worst‑case on Quest hardware for the hot path.
* **Reliability:** zero crashes in ≥ 1 hour continuous operation.
* **Scale:** ≥ 100 active rules without frame degradation.
* **Compat:** IL2CPP/AOT compliance.
* **DX:** fast load, exhaustive error reporting, hot reload in dev.

---

## 3) Architecture overview

### 3.1 Layers & modules

**Core Engine (C#, .NET Standard 2.1)**

* `RuleLoader` — parse JSON, validate, and build authoring AST.
* `RuleCompiler` — bind names to IDs, precompile expressions, generate **RulePlan** FSM.
* `EventBus` — lock‑free ring buffer, index maps for event routing.
* `ConditionEval` — tiny expression interpreter (no reflection).
* `ActionExec` — deterministic dispatcher with cancellation and coalescing.
* `Scheduler` & `IClock` — per‑frame tick integration; **timer wheel** for `for/timeout`.
* `EntityRegistry` — entity/attribute store, SoA layout with int handles.
* `ServiceRegistry` — mapping of `domain.service` → numeric ID + adapter call.
* `Diagnostics` — profiling hooks, structured logs, per‑rule circular trace.

**Unity Adapter (C# in Unity)**

* PlayerLoop integration (`Update`/`FixedUpdate`), single‑threaded main‑thread bridge.
* Input bridge (OVR / Meta XR) → canonical `xr.*` events.
* Spatial queries (zones, proximity) using non‑alloc physics APIs.
* Services: haptics, audio, UI, scene, camera, HTTP (UnityWebRequest), etc.
* UniTask boundary adapters (if used), but **core** remains `Task`/`ValueTask`‑free.

### 3.2 Threading model

* Core engine is **single‑threaded** from the game thread’s perspective; no `ThreadPool` in hot paths.
* Background IO (e.g., HTTP) terminates on the adapter side and marshals back to main in a controlled point.
* Unity calls only occur via the adapter on the main thread.

### 3.3 Rule lifecycle (high‑level flow)

1. **Load** authoring JSON → **validate** (strict schema + lints).
2. **Compile** to `RulePlan`:

   * Resolve strings → **int IDs** (entities, zones, services).
   * Precompile expressions to AST nodes with typed eval delegates.
   * Build FSM states (`Idle`, `Waiting`, `Running`, `Cancelled`, `Done`).
   * Prepare indexed **subscriptions** (event name → subscriber list).
3. **Activate** plan in `RuleEngine`. Subscribe triggers, prime timers.
4. **Tick** per PlayerLoop phase:

   * Drain `EventBus` (struct events), route by index, evaluate conditions, schedule actions.
   * Service timers via **timer wheel**.
   * Execute actions deterministically under the rule’s **mode**.
5. **Hot reload (dev):** swap plan atomically; keep previous for in‑flight completions or cancel according to policy.

---

## 4) Authoring model (v1)

### 4.1 Top level

```json
{
  "id": "string",
  "alias": "string?",
  "description": "string?",
  "mode": "single|restart|queue|parallel",
  "max": 10,
  "vars": { "any": "json" },
  "on": [ /* triggers */ ],
  "if": [ /* optional conditions */ ],
  "do": [ /* actions */ ],
  "schemaVersion": 1
}
```

**Mode semantics**

* `single`: ignore new triggers while running.
* `restart`: cancel running and start new instance.
* `queue`: enqueue up to `max`; FIFO, back‑pressure when full.
* `parallel`: allow up to `max` concurrent instances.

### 4.2 Triggers

```json
{ "type": "event", "name": "app.started", "data?": { "k": "v" }, "id?": "t1" }
{ "type": "state", "entity": "player.health", "to?": ["low"], "from?": ["ok"], "for?": 500, "attr?": "value", "id?": "t2" }
{ "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "enter|leave", "id?": "t3" }
{ "type": "time", "at?": "HH:MM:SS", "every?": { "hours?": "/1", "minutes?": "/5", "seconds?": "/1" }, "id?": "t4" }
{
  "type": "pattern", "within": 300,
  "steps": [
    { "event": { "name": "xr.button", "data": { "hand": "right", "btn": "primary", "state": "pressed" } } },
    { "event": { "name": "xr.button", "data": { "hand": "right", "btn": "primary", "state": "pressed" } } }
  ],
  "id": "double_tap"
}
```

### 4.3 Conditions

```json
{ "type": "state",   "entity": "game.mode", "is": ["playing","paused"] }
{ "type": "numeric", "entity": "sensor.current_fps", "below?": 45, "above?": 15 }
{ "type": "time",    "after?": "08:00:00", "before?": "20:00:00", "weekday?": ["mon","tue"] }
{ "type": "expr",    "expr": "state('sensor.fps') < 45 && vars.autoPerf == true" }
```

### 4.4 Actions

```json
{ "type": "call", "service": "haptics.pulse", "target?": { "entity": "hand.right" },
  "data?": { "amplitude": "${clamp(state('player.health')/100, 0.2, 1.0)}", "duration_ms": 50 } }

{ "type": "wait", "for": 1000 }
{ "type": "wait", "until": { "type": "expr", "expr": "state('player.health') >= 30" }, "timeout?": 10000, "on_timeout?": "continue|stop" }
{ "type": "wait", "trigger": { "type": "zone", "entity": "player.avatar", "zone": "temple_entrance", "event": "enter" }, "timeout?": 600000 }

{
  "type": "choose",
  "when": [
    { "if": [ { "type":"expr", "expr":"trigger.id == 'press'" } ],
      "do": [ { "type":"call", "service":"haptics.pulse", "data": { "hand":"right","amplitude":0.5,"duration_ms":50 } } ] }
  ],
  "else": [ { "type":"call", "service":"log.info", "data": { "message": "fallback" } } ]
}

{ "type": "parallel",
  "do": [
    { "type":"call", "service":"light.turn_on", "target":{"entity":"light.player_torch"}, "data":{"brightness":255,"transition":2} },
    { "type":"call", "service":"audio.play", "data":{"sound":"torch_ignite","volume":0.7} }
  ]
}

{ "type": "set_vars", "vars": { "phase": "combat" } }   // optional (explicit mutation)
{ "type": "stop", "reason": "No active players", "error?": false }
```

### 4.5 Expression DSL (fast & sandboxed)

* **Selectors:** `state("entity.id")`, `attr("entity.id","attr")`, `vars.key`, `trigger.id`, `now()` (monotonic ms).
* **Ops:** arithmetic, comparisons, `&& || !`, ternary `cond ? a : b`.
* **Funcs:** `clamp(x,a,b)`, `abs(x)`, `min`, `max`, `distance(a,b)` (adapter provided).
* **Strings:** only literal + interpolation via `${expr}` inside action data.
* **No** loops, dynamic member access, or reflection.

---

## 5) Core engine design details

### 5.1 Key C# interfaces (abbrev.)

```csharp
public interface IRuleEngine
{
    void Load(IEnumerable<RuleDocument> docs);     // parse+validate+compile
    void Activate();                                // subscribe all triggers
    void Deactivate();                              // unsubscribe
    void Tick(TimeSpan delta);                      // called from PlayerLoop
    EngineStats GetStats();
}

public interface IEntityRegistry
{
    int ResolveEntityId(string name);               // compile-time
    ReadOnlySpan<float> GetSoA(int entityId);       // numeric attrs fast path
    string GetStateString(int entityId);            // cold path
    void SetState(int entityId, in EntityState state);
}

public interface IServiceRegistry
{
    int ResolveServiceId(string dotName);           // compile-time
    void Invoke(int serviceId, in ServiceCall call); // main thread
}

public interface IClock { long NowMs { get; } }     // monotonic

public interface ITimerWheel
{
    TimerHandle Schedule(long dueMs, Action cb);
    void Cancel(TimerHandle handle);
    void Advance(long nowMs);                       // called from Tick
}
```

### 5.2 Event pipeline

* `struct EngineEvent { int key; int a; int b; long t; }` (keyed by name/entity).
* **Index maps**: `Dictionary<int, SubscriberList>` built at compile time; no string hashing at runtime.
* **Ring buffer** with fixed capacity (configurable), drop policy configurable (warn vs. back‑pressure).

### 5.3 Action execution model

* **Deterministic action queue** per rule instance.
* **Coalescing** policy for frequent actions (e.g., multiple haptics in same frame → merged).
* **Error policy:** action failure → mark rule instance “failed” and either `stop` or continue based on `onError` (config default: stop & log).

### 5.4 Timers

* **Timer wheel** or calendar queue; O(1) amortized insert/advance.
* All time fields are **ms (int)** after compilation.

### 5.5 Serialization

* Authoring JSON (human‑friendly) compiled to **`.uar`** (binary):

  * Header (magic, schemaVersion), tables (entities/services/zones), rule plans, expression bytecode/AST.
  * Optional compression.
* Optionally support **System.Text.Json source‑gen** for the authoring phase (dev) and **MessagePack** for runtime caches.

---

## 6) Unity adapter design

* **PlayerLoop hook:** one engine `Tick` in `Update`. If you rely on physics zones, optionally sample in `FixedUpdate` and enqueue canonical events.
* **Input bridge:** normalize button/axis/gesture into `xr.button`, `xr.axis`, `xr.gesture` events with sample time.
* **Zones:** central registry (AABB/sphere/mesh ref); non‑alloc overlap queries; cache colliders; per‑zone membership tracking to emit `enter/leave`.
* **Services mapping:**

  * `haptics.*` → Meta XR APIs
  * `audio.play`, `vfx.*`, `ui.*`, `scene.*`, `camera.*`, `graphics.*`
  * `http.*` → UnityWebRequest (allowlist + timeout + max bytes)
* **Threading:** All service invocations on main thread; HTTP completion marshaled back safely.

---

## 7) Determinism & performance

* Value‑type events, **no LINQ**, no per‑frame allocations.
* Expression eval in **100–300 ns** on device (target); cache entity lookups.
* Pre‑allocate subscriber lists; avoid `List<T>.Add` in hot path by sizing at compile.
* `IClock` is the **single source of time**. No `DateTime.UtcNow` scattered around.
* Explicit **mode** semantics tested via harness (see §9).

---

## 8) Tooling & developer experience

* **JSON Schema + Linter:** exhaustive errors with locations and suggestions; emits warnings for anti‑patterns (e.g., unbounded `parallel`).
* **Compiler CLI** (`uarc`): `validate`, `compile`, `disasm` (dump plans/ASTs), `stats`.
* **Simulator CLI** (`uarsim`): feed event traces, print decision logs and action plan.
* **Unity Editor window:** visualize active rules, instances, timers, last N decisions.
* **“Why didn’t this fire?”** buffer: per rule instance circular trace of triggers, condition outcomes, and reasons for back‑off.

---

## 9) Testing strategy

**Core**

* Unit tests for parser, validator, expression evaluator, timer wheel, FSM transitions.
* Property‑based tests: numeric thresholds, time windows, sequence patterns.
* Fuzz tests on authoring JSON (malformed, adversarial values).

**Integration**

* Adapter integration tests with mocked input streams and zones.
* Determinism tests for `mode` under bursty triggers.

**Device**

* On‑device perf harness with synthetic workloads: N rules × M events/sec; capture CPU/GC.
* IL2CPP builds with stripping; verify no reflection‑driven failures.

**CI**

* Headless `uarc validate/compile` on PRs; per‑PR perf baseline comparison.

---

## 10) Security & safety

* **Expression DSL** only; no codegen; whitelisted functions.
* **HTTP allowlist** + per‑request timeout + size limits.
* **Quota protections:** max `parallel` instances; global action budget per frame.
* **Namespace hygiene:** entities/services resolved only from registries; unknown names are hard errors at load.

---

## 11) Versioning & migration

* `schemaVersion` incremented on breaking changes; compiler can up‑convert minor variants.
* `.uar` includes target engine version; engine rejects incompatible packages with clear guidance.

---

## 12) Risk register (targeted mitigations)

* **Rx allocations:** keep R3 optional; prefer custom value‑type operators for hot streams.
* **IL2CPP edge cases:** no `dynamic`, no `Expression.Compile`; integration tests per feature.
* **Zone perf:** many zones require a simple spatial index (grid hashing) to limit pair checks.

---

## 13) Deliverables

* **Core package** (`RulesEngine.Core.dll`) + tests
* **Unity adapter** (`RulesEngine.Unity.dll`) + example scenes
* **Tools:** `uarc`, `uarsim`, JSON Schema, linter rules
* **Docs:** Architecture, Authoring Guide, Perf Guide, Troubleshooting

---

# Curated examples (v1 schema)

All examples use the simplified v1 schema and tiny DSL. Times are in **milliseconds** unless a wall‑clock string is indicated.

---

### 1) App start → log (minimal)

```json
{
  "id": "app_start_log",
  "alias": "Log on startup",
  "mode": "single",
  "on": [ { "type": "event", "name": "app.started" } ],
  "do": [
    { "type": "call", "service": "log.info", "data": { "message": "Started at ${now()}" } }
  ]
}
```

---

### 2) XR button → haptics; long release → pattern (two triggers, one rule)

```json
{
  "id": "xr_button_handler",
  "alias": "Right primary button",
  "mode": "single",
  "on": [
    { "type": "state", "entity": "xr.controller.right.button_primary", "to": ["pressed"], "id": "press" },
    { "type": "state", "entity": "xr.controller.right.button_primary", "to": ["released"], "for": 2000, "id": "long_release" }
  ],
  "if": [
    { "type": "state", "entity": "game.mode", "is": ["playing","paused"] }
  ],
  "do": [
    {
      "type": "choose",
      "when": [
        {
          "if": [ { "type":"expr", "expr":"trigger.id == 'press'" } ],
          "do": [ { "type":"call", "service":"haptics.pulse", "data": { "hand":"right","amplitude":0.5,"duration_ms":50 } } ]
        },
        {
          "if": [ { "type":"expr", "expr":"trigger.id == 'long_release'" } ],
          "do": [ { "type":"call", "service":"haptics.pattern", "data": { "hand":"right","pattern":"double_click" } } ]
        }
      ]
    }
  ]
}
```

---

### 3) Zone entry/exit → torch + exposure (parallel)

```json
{
  "id": "zone_lighting",
  "alias": "Adaptive cave lighting",
  "mode": "restart",
  "on": [
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "enter", "id": "enter" },
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "leave", "id": "leave" }
  ],
  "do": [
    {
      "type": "choose",
      "when": [
        {
          "if": [ { "type":"expr", "expr":"trigger.id == 'enter'" } ],
          "do": [
            { "type":"parallel", "do": [
              { "type":"call", "service":"light.turn_on", "target": { "entity": "light.player_torch" }, "data": { "brightness": 255, "transition": 2000 } },
              { "type":"call", "service":"audio.play", "data": { "sound": "torch_ignite", "volume": 0.7 } },
              { "type":"call", "service":"camera.set_exposure", "data": { "exposure": 1.5, "adaptation_ms": 3000 } }
            ] }
          ]
        },
        {
          "if": [ { "type":"expr", "expr":"trigger.id == 'leave'" } ],
          "do": [
            { "type":"call", "service":"light.turn_off", "target": { "entity": "light.player_torch" }, "data": { "transition": 1000 } },
            { "type":"call", "service":"camera.set_exposure", "data": { "exposure": 1.0, "adaptation_ms": 2000 } }
          ]
        }
      ]
    }
  ]
}
```

---

### 4) Temperature threshold → HTTP + UI, daytime only

```json
{
  "id": "temp_alert",
  "alias": "Temperature alerting",
  "mode": "queue",
  "max": 3,
  "vars": { "endpoint": "https://api.example.com/alerts", "threshold": 28.5 },
  "on": [
    { "type": "numeric", "entity": "sensor.room_temp_c", "above": 28.5, "for": 30000 }
  ],
  "if": [
    { "type": "time", "after": "08:00:00", "before": "20:00:00" }
  ],
  "do": [
    { "type": "call", "service": "http.post",
      "data": {
        "url": "${vars.endpoint}",
        "headers": { "Content-Type": "application/json", "X-Source": "unity-automation" },
        "json": {
          "sensor": "sensor.room_temp_c",
          "temperature": "${state('sensor.room_temp_c')}",
          "threshold": "${vars.threshold}",
          "ts": "${now()}"
        },
        "timeout": 5000
      }
    },
    { "type": "call", "service": "ui.show_notification",
      "data": { "title": "Temperature Alert", "message": "Room: ${state('sensor.room_temp_c')}°C", "type": "warning", "duration": 5000 }
    }
  ]
}
```

---

### 5) Quest branch: wait for zone then load scene (choose + wait.trigger)

```json
{
  "id": "quest_progress",
  "alias": "Ancient artifact quest",
  "mode": "parallel",
  "max": 5,
  "on": [
    { "type": "event", "name": "quest.objective_complete", "data": { "quest_id": "ancient_artifact" } }
  ],
  "do": [
    {
      "type": "choose",
      "when": [
        {
          "if": [ { "type":"expr", "expr":"trigger.event.data.objective_id == 'collect_fragments'" } ],
          "do": [
            { "type":"call", "service":"ui.show_notification", "data": { "title":"Fragments Collected", "message":"You have all three fragments!", "duration": 5000 } },
            { "type":"call", "service":"object.spawn", "data": { "prefab": "ancient_portal", "position": "${state('marker.temple_entrance')}", "activate_after_ms": 2000 } },
            { "type":"wait", "trigger": { "type":"zone", "entity":"player.avatar", "zone":"temple_entrance", "event":"enter" }, "timeout": 600000, "on_timeout": "stop" },
            { "type":"call", "service":"scene.load", "data": { "scene":"ancient_temple_interior", "transition":"fade_to_black" } }
          ]
        },
        {
          "if": [ { "type":"expr", "expr":"trigger.event.data.objective_id == 'defeat_guardian'" } ],
          "do": [
            { "type":"parallel", "do": [
              { "type":"call", "service":"quest.complete", "data": { "quest_id":"ancient_artifact", "rewards": { "xp": 5000, "items": ["legendary_sword"] } } },
              { "type":"call", "service":"achievement.unlock", "data": { "achievement_id":"guardian_slayer" } },
              { "type":"call", "service":"cutscene.play", "data": { "cutscene_id":"artifact_revealed" } }
            ] }
          ]
        }
      ]
    }
  ]
}
```

---

### 6) Time.every telemetry, skip when no players (stop)

```json
{
  "id": "telemetry",
  "alias": "Periodic telemetry",
  "mode": "single",
  "on": [ { "type": "time", "every": { "minutes": "/5" } } ],
  "do": [
    {
      "type": "choose",
      "when": [
        {
          "if": [ { "type":"expr", "expr":"state('sensor.active_players') == 0" } ],
          "do": [
            { "type":"call", "service":"log.debug", "data": { "message": "No active players, skipping" } },
            { "type":"stop", "reason": "No active players" }
          ]
        }
      ],
      "else": [
        { "type":"call", "service":"http.post", "data": {
          "url":"https://telemetry.example.com/ingest",
          "json": {
            "ts": "${now()}",
            "players": "${state('sensor.active_players')}",
            "fps": "${state('sensor.average_fps')}",
            "mem": "${state('sensor.memory_mb')}",
            "scene": "${state('game.current_scene')}"
          },
          "timeout": 10000
        } }
      ]
    }
  ]
}
```

---

### 7) Performance optimizer (responds to sustained FPS drop)

```json
{
  "id": "perf_optimizer",
  "alias": "Auto performance tuning",
  "mode": "single",
  "vars": { "target_fps": 60 },
  "on": [ { "type": "numeric", "entity": "sensor.current_fps", "below": 30, "for": 5000 } ],
  "if": [ { "type": "state", "entity": "settings.auto_performance", "is": ["enabled"] } ],
  "do": [
    { "type":"call", "service":"log.warning", "data": { "message":"FPS < 30 for 5s, optimizing..." } },
    { "type":"call", "service":"graphics.reduce_quality", "data": { "step": 1 } },
    { "type":"wait", "for": 2000 },
    { "type":"choose",
      "when": [
        { "if": [ { "type":"expr", "expr":"state('sensor.current_fps') < 45 && state('graphics.quality') != 'very_low'" } ],
          "do": [ { "type":"call", "service":"scene.schedule_recheck", "data": { "rule_id": "perf_optimizer", "delay_ms": 2000 } } ] }
      ],
      "else": [
        { "type":"call", "service":"log.info", "data": { "message":"Quality=${state('graphics.quality')} FPS=${state('sensor.current_fps')}" } }
      ]
    }
  ]
}
```

> The `scene.schedule_recheck` service is an example “self‑trigger” facility in the adapter that re‑emits the original trigger after a delay, letting you iterate without a `repeat` primitive.

---

### 8) Gesture pattern: right primary double‑tap within 300 ms

```json
{
  "id": "double_tap_action",
  "alias": "Right double tap action",
  "mode": "single",
  "on": [
    {
      "type": "pattern",
      "within": 300,
      "steps": [
        { "event": { "name": "xr.button", "data": { "hand":"right", "btn":"primary", "state":"pressed" } } },
        { "event": { "name": "xr.button", "data": { "hand":"right", "btn":"primary", "state":"pressed" } } }
      ],
      "id": "double_tap"
    }
  ],
  "do": [
    { "type":"call", "service":"ui.show_notification", "data": { "message":"Double tap!", "duration": 1500 } },
    { "type":"call", "service":"haptics.pattern", "data": { "hand":"right", "pattern":"double_click" } }
  ]
}
```

---

### 9) Wait until a condition becomes true (gate progression)

```json
{
  "id": "gate_progress",
  "alias": "Wait for key",
  "mode": "single",
  "on": [ { "type": "event", "name": "door.interact" } ],
  "do": [
    { "type":"wait", "until": { "type":"expr", "expr":"state('player.inventory.has_key') == true" }, "timeout": 600000, "on_timeout": "stop" },
    { "type":"call", "service":"door.open", "target": { "entity": "door.ancient_gate" } }
  ]
}
```

---

### 10) Using vars + interpolation to parameterize a family of rules

```json
{
  "id": "param_haptics",
  "alias": "Parametric haptics",
  "mode": "single",
  "vars": { "hand": "left", "amp": 0.6, "dur": 40 },
  "on": [ { "type":"event", "name":"ability.activated", "data": { "name":"dash" } } ],
  "do": [
    { "type":"call", "service":"haptics.pulse", "data": { "hand":"${vars.hand}", "amplitude":"${vars.amp}", "duration_ms":"${vars.dur}" } }
  ]
}
```

---

# Implementation notes that make the examples real

1. **Entity registry:** Define strong naming (`xr.controller.right.button_primary`) and expose a JSON manifest or code‑gen file so LLMs and humans autocomplete valid names.
2. **Zone registry:** Provide static definitions (sphere/AABB/mesh ref) in a single source of truth; the adapter attaches colliders and performs non‑alloc checks.
3. **Service registry:** Keep the `domain.service` strings for authoring. At compile, assign **numeric IDs** and dispatch through a vtable‑like array to eliminate string lookups.
4. **Time parsing:** Human‑friendly `HH:MM:SS` is accepted only at authoring; compiler converts to ms. Runtime never parses strings.
5. **Error messages:** Always include the rule `id`, action index, and a short cause (“target entity not found”, “timeout”, “http 403”).

---

# What you can ship first (pragmatic v1)

* Keep **triggers** (`event|state|zone|time`), **conditions** (`state|numeric|expr|time`), **actions** (`call|wait|choose|parallel|stop`), and **optional** `pattern` + `set_vars`.
* Implement **FSM compilation**, **timer wheel**, **expression DSL**, **entity/service registries**, **adapter services** for haptics/audio/ui/scene/http.
* Ship with **the 10 examples** above and an **Editor window** that shows active rules, recent triggers, and action traces.

If you want, I can turn this into a small starter repo layout (projects, folders, initial interfaces, and a stubbed compiler) so you can start wiring Unity immediately.

Source: https://chatgpt.com/c/68ba34ca-2b0c-8326-affe-2c923ae67d6a