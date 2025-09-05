Below is a **fully updated, comprehensive implementation plan** based on the simplified **v0.1 schema** and your VR constraints, followed by a **curated set of examples** rewritten to the new schema.

---

# Unity Rules Engine — Implementation Plan (v0.1)

## 0) Goal & Success Criteria

**Goal:** A deterministic, zero/low‑allocation Event–Condition–Action (ECA) rules engine for Quest VR (72–120 Hz), powered by LLM‑generated JSON rules, with a standalone C# core and a Unity adapter.

**Success metrics (shipping gate):**

* **Perf:** p95 rule evaluation < **0.5 ms** on-device; < **100 µs**/trigger in hot path after warmup.
* **Reliability:** zero crashes in 1‑hour soak; automatic recovery from invalid rule packs.
* **Correctness:** > **80%** unit coverage on core; replay tests for all examples.
* **DX:** Hot reload in Editor; structured errors and rule linter; dry‑run mode.
* **Scale:** 100+ active rules w/ steady memory; no GC spikes in hot path.

---

## 1) Architecture Overview

**Core (C#/.NET Standard 2.1)**

* **Rule Loader & Validator** → JSON → typed AST → compiled delegates.
* **Entity Registry** (read-only signals + attributes; typed/unit’d).
* **Service Registry** (callable actions w/ arg schemas & thread affinity).
* **Event Pipeline** (R3 or custom alloc‑free operators): debounce/throttle/cooldown/distinct.
* **Condition Engine** (typed numeric/state/time + small `expr` language).
* **Scheduler/Runtime** (per‑rule executor, modes: single/restart/queued/parallel).
* **Time Abstraction** (game vs realtime clocks).
* **Diagnostics** (traces, metrics, dry‑run, deterministic replay).

**Unity Adapter**

* **Time sources**: `game` (scaled/unscaled) & `realtime`.
* **Input bridge**: Meta XR → engine events (buttons, gestures).
* **Spatial queries**: zones/proximity via Physics/Volumes.
* **Main-thread hop**: UniTask bridge to call Unity APIs safely.
* **XR services**: haptics, camera, scene, object spawn, UI, audio, player state, etc.

**Tooling**

* **Rule Linter/Compiler CLI**: validate + compile JSON to compact runtime model.
* **Editor Window**: live rule status, last trigger, timers, perf counters.
* **Playground Scene**: simulate entities/events & visualize zones.

---

## 2) v0.1 Rule Schema (authoring contract)

> Compact, AOT‑friendly, and deterministic. Jinja2 is removed; we use a tiny boolean expression (`expr`) and safe string interpolation `${…}`.

**Top level**

```jsonc
{
  "schema_version": 1,
  "id": "string",
  "alias": "string?",
  "description": "string?",
  "mode": "single|restart|queued|parallel",
  "max": 10,
  "variables": { },
  "triggers": [ /* Trigger */ ],
  "conditions": [ /* Condition */ ],
  "actions": [ /* Action */ ],
  "capabilities": ["http","zones","haptics"] // optional allowlist
}
```

**Trigger (shared options)**

```jsonc
"options": {
  "debounce_ms": 0,
  "throttle_ms": 0,
  "cooldown_ms": 0,
  "distinct": true
}
```

**Triggers**

```jsonc
// state
{ "trigger": "state", "entity_id": "string|array", "from": "string|array?", "to": "string|array?", "for": "duration?", "attribute": "string?", "id": "string?", "options": { ... } }

// event
{ "trigger": "event", "event_type": "string", "match": { "key": "value" }, "id": "string?", "options": { ... } }

// numeric_state
{ "trigger": "numeric_state", "entity_id": "string|array", "above": "number?", "below": "number?", "for": "duration?", "id": "string?", "options": { ... } }

// time (single + pattern + cron)
{ "trigger": "time", "at": "HH:MM:SS?", "pattern": { "hours": "*/2", "minutes": "0", "seconds": "0" }?, "cron": "string?" }

// zone
{ "trigger": "zone", "entity_id": "string|array", "zone": "string", "event": "enter|leave", "id": "string?", "options": { ... } }
```

**Conditions**

```jsonc
{ "condition": "state", "entity_id": "string", "is": "string|array", "attribute": "string?", "for": "duration?" }
{ "condition": "numeric_state", "entity_id": "string", "above": "number?", "below": "number?" }
{ "condition": "time", "after": "HH:MM:SS?", "before": "HH:MM:SS?", "weekday": ["mon","tue","wed","thu","fri","sat","sun"]? }
{ "condition": "and|or", "conditions": [ /* Condition */ ] }
{ "condition": "not", "conditions": [ /* Condition */ ] }
{ "condition": "trigger", "id": "string|array" }
{ "condition": "expr", "expr": "health < 30 && status == 'alive'" }
```

**Actions**

```jsonc
{ "action": "call", "service": "domain.name", "target": { "entity_id": "string|array?" }, "data": { /* strings may use ${...} interpolation */ } }
{ "action": "delay", "for": "duration" }
{ "action": "wait", "until": { /* Condition or Trigger */ }, "timeout": "duration?", "continue_on_timeout": true }
{ "action": "vars", "set": { "key": "value" } }
{ "action": "choose", "choices": [ { "when": [ /* Conditions */ ], "do": [ /* Actions */ ] } ], "otherwise": [ /* Actions */ ] }
{ "action": "repeat", "count": 3?, "until": [ /* Conditions */ ]?, "do": [ /* Actions */ ] }
{ "action": "parallel", "do": [ /* Actions */ ] }
{ "action": "stop", "reason": "string", "error": false }
```

**Durations**

* Accept: `"200ms"`, `"1.5s"`, `"2s"`, `"00:00:02"`. Internally normalized to milliseconds.

**String interpolation**

* Allowed in **string values only** as `${expr}`.
* `expr` access: `vars.*`, `trigger.*`, `state("id")`, `attr("id","key")`, `now()` (ISO string), `ticks()` (ms since engine start).

---

## 3) Core Engine Design

### 3.1 Rule Loading & Compilation

1. JSON → **AST** (manual switch on `"trigger"/"condition"/"action"` discriminators).
2. **Validation**:

   * Schema (required fields, enums, ranges).
   * Entity existence & type/unit compatibility.
   * Service existence & argument schema; clamp numerics (e.g., `[0..1]` haptic amplitude).
   * Capabilities vs adapter-provided set.
3. **Compilation**:

   * Prebind entity getters/setters.
   * Compile `expr` to IL2CPP‑safe delegates (handwritten parser → RPN → eval).
   * Build trigger pipelines with options (debounce/throttle/cooldown/distinct).
   * Allocate pooled **RunContext** factories (no per‑fire allocations).
4. **Activation**:

   * Subscribe to events; each rule owns a **single-threaded executor** (cooperative), enforcing mode semantics.

### 3.2 Entity Registry (read model)

```csharp
struct EntityMeta { string Id; EntityType Type; string Unit; Func<TypedValue> Read; IReadOnlyDictionary<string, Func<TypedValue>> Attr; }
enum EntityType { Number, Bool, Enum, Vector3, String }
```

* All reads return **typed** values. Avoid string parsing at runtime.
* Provide **change notifications** → event bus.

### 3.3 Service Registry (write/call model)

```csharp
struct ServiceMeta {
  string Name; // e.g., "haptics.pulse"
  ArgSchema Schema; // types, ranges, required
  ThreadAffinity Affinity; // MainThread | Any
  Func<ServiceCallContext, UniTask> Invoke;
  bool AllowsInterpolation; // enable ${...} inside strings
}
```

* Registry lives in adapter; core holds only interface.
* Core validates & builds **arg mappers** at load.

### 3.4 Trigger Pipeline

* Operators implemented allocation‑free:

  * **debounce\_ms**: trailing edge.
  * **throttle\_ms**: leading edge.
  * **cooldown\_ms**: drop triggers during cooldown.
  * **distinct**: drop duplicate consecutive states (deep or by key selector).
* Provide **per‑trigger** monotonic timers using the rule’s chosen **time source**.

### 3.5 Condition Engine

* Fast predicates for `state`, `numeric_state`, `time`.
* `expr` language:

  * Literals: number, string, bool.
  * Idents: `vars.x`, `trigger.id`, `state("foo")`, `attr("foo","bar")`.
  * Ops: `!`, `&&`, `||`, `==`, `!=`, `<`, `<=`, `>`, `>=`.
  * No loops or user functions.
* Pre‑evaluate constant subtrees at load.

### 3.6 Action Execution & Modes

* **single**: ignore new fires while running.
* **restart**: cancel previous run (cooperative cancellation) then start fresh.
* **queued**: FIFO queue size = `max`; drop oldest on overflow or block (engine option).
* **parallel**: run up to `max` concurrent runs (separate RunContexts).
* **parallel/do** spawns child tasks that **inherit** cancellation & share variables by value (copy-on-write for `vars`).

### 3.7 Time Sources

* Per rule default: `"game"` (`Time.time` / `unscaledTime` selectable in adapter settings).
* Allow **per trigger** override via adapter if needed (kept out of schema for v0.1).
* Wall clock for cron/at/weekday uses **realtime**.

### 3.8 Memory & Allocation Strategy

* Struct event payloads, object pools for:

  * RunContext, TriggerFrame, ActionFrame, small lists.
* Pre-size pools at activation based on rule complexity.
* Avoid LINQ in hot path; no closures capturing heap.

### 3.9 Error Handling & Observability

* **Fail‑fast at load** with actionable errors.
* At runtime: per rule **counters** (fired, suppressed, queued, timed out), last error, last exec time.
* **Dry‑run** flag: log intended actions without execution.
* Deterministic **replay harness**: feed recorded events & assert outcomes.

---

## 4) Unity Adapter

### 4.1 Threading & Main Thread Hops

* All services declare **ThreadAffinity**.
* Adapter provides `IUnityMainThread` scheduler using UniTask:

  * `await mainThread.SwitchIfNeeded();`

### 4.2 Input Bridge (Meta XR)

* Map `OVRInput` and hand tracking to entities/events:

  * `xr.controller.right.button_primary` (enum: pressed/released).
  * `xr.hand.gesture` (event type with match data).
  * HMD pose/velocity as **Vector3** entities (read‑only).

### 4.3 Zones / Spatial

* Zone definitions via ScriptableObjects / colliders.
* Zone events: `enter|leave` for entity `player.avatar` (or tracked objects).
* Distance/proximity numeric sensors (meters).

### 4.4 Services (initial allowlist)

* **haptics.pulse** `{ hand: "left|right|both", amplitude[0..1], duration_ms }`
* **haptics.pattern** `{ hand, pattern: "double_click|..." }`
* **log.debug/info/warning/error** `{ message }`
* **http.get/post/put/delete** `{ url, json|body, headers, timeout }` (rate‑limited + domain allowlist)
* **xr.recenter**, **xr.teleport** `{ position: Vector3, rotation_y? }`
* **scene.load** `{ scene, transition? }`
* **object.spawn** `{ prefab, position, rotation?, parent? }`
* **ui.show\_notification** `{ title?, message, type?, duration }`
* **audio.play** `{ sound, volume[0..1] }`
* **player.set\_state** `{ state: "alive|dead|..." }`
* **camera.set\_exposure** `{ exposure, adaptation_time }`
* **vfx.screen\_effect** `{ effect, intensity }`
* **graphics.reduce\_quality** `{ step: int }`
* **quest.complete**, **achievement.unlock**, **cutscene.play** as needed.

> Each service has an **ArgSchema** with ranges and interpolation policy; core enforces at load.

---

## 5) Tooling & Authoring

* **Rule Linter (CLI & Editor):**

  * JSON Schema + semantic validation (entities/services/capabilities).
  * Range clamps + unit checks.
  * Warnings for anti‑patterns (missing cooldown, unbounded repeat).
* **Compiler CLI**:

  * `rules.json` → `rules.compiled` (compact binary/MessagePack).
  * Prints perf estimates (operator counts) & memory budget.
* **Editor Window**:

  * Live rule list; per‑rule state; last trigger; timers/cooldowns; errors.
* **Playground**:

  * Sim UI to tweak entities & fire events; real‑time action preview.
* **Hot reload**:

  * Editor only by default; opt‑in in player builds (`#define RULES_HOT_RELOAD`).

---

## 6) Testing & Performance

**Unit**: AST parsing, expression parser, numeric comparisons, trigger options, mode semantics, service arg mapping.

**Property‑based**: `numeric_state` boundaries, `distinct` equivalence, time window edges.

**Integration**: Curated examples; recorded event traces; run deterministically.

**Perf**:

* Micro-bench operators (debounce/throttle).
* Macro: 100 rules, 1 kHz synthetic events; track allocations (should be \~0 in steady state).

**Device (Quest)**:

* IL2CPP builds; 30‑min soak; memory snapshots (warm→steady); p95/p99 latencies.

---

## 7) Security & Safety

* **Capabilities**: rules declare needs; adapter must provide them.
* **HTTP**: allowlist domains, per‑rule rate limits, small body caps, redact logs.
* **Interpolation**: only `${…}`; evaluation from safe expression VM (no reflection).
* **Numeric clamps** everywhere.
* **Sandbox**: no file I/O; no dynamic code; no reflection at runtime.

---

## 8) CI/CD & Packaging

* **Packages**:

  * `Rules.Core` (pure C# lib, NuGet).
  * `Rules.UnityAdapter` (UPM package).
  * `Rules.Tools` (CLI: linter/compiler).
  * `Rules.Samples` (UPM with scenes & rules).
* **Pipelines**:

  * .NET test/coverage.
  * Unity edit‑mode + play‑mode CI.
  * Quest device smoke (nightly).
* **Artifacts**:

  * IL2CPP compatibility manifest (used features).
  * Performance report per commit.

---

## 9) Phased Delivery Plan

**Phase 1 — Core MVP (2–3 weeks)**

* AST/validation, `state|event|numeric_state|time|zone` triggers with options.
* Conditions (`state|numeric|time|and|or|not|trigger|expr`).
* Actions (`call|delay|vars|choose|repeat|parallel|stop|wait`).
* Entity/Service registries; expression VM; per‑rule executor; dry‑run; logs.

**Gate:** Pass unit/property tests. Bench p95 < 0.5 ms (synthetic).

**Phase 2 — Unity Adapter (2–3 weeks)**

* Time sources, input bridge, zones, main‑thread hops.
* Haptics/log/http/ui/audio/scene/object/camera/vfx basic services.
* Editor Window + Playground.

**Gate:** Curated examples run in Editor, no allocs in hot path; IL2CPP smoke on Quest.

**Phase 3 — Perf & Scale (1–2 weeks)**

* Pools tuning, compiled binary rule packs, stress @ 100 rules.
* Deterministic replay; soak tests.

**Gate:** Device perf + soak metrics; docs completed.

**Phase 4 — Advanced polish (as needed)**

* Cron in `time` trigger; additional services (quest/achievement/cutscene).
* More diagnostics; schema versioning & migration.

---

# Curated Examples (v0.1 Schema)

> All strings may use `${…}` interpolation. Each example is valid standalone.

---

### 1) App Startup Log (minimal)

```json
{
  "schema_version": 1,
  "id": "app_startup_log",
  "alias": "Log on app start",
  "mode": "single",
  "triggers": [ { "trigger": "event", "event_type": "app.started" } ],
  "actions": [
    { "action": "call", "service": "log.info",
      "data": { "message": "Application started at ${now()}" } }
  ]
}
```

---

### 2) Right Controller Button → Haptic Tap (with cooldown)

```json
{
  "schema_version": 1,
  "id": "xr_button_haptic",
  "alias": "Right primary tap",
  "mode": "single",
  "triggers": [
    { "trigger": "state",
      "entity_id": "xr.controller.right.button_primary",
      "to": "pressed",
      "id": "press",
      "options": { "distinct": true, "cooldown_ms": 50 } }
  ],
  "conditions": [
    { "condition": "state", "entity_id": "game.mode", "is": ["playing","paused"] }
  ],
  "actions": [
    { "action": "call", "service": "haptics.pulse",
      "data": { "hand": "right", "amplitude": 0.5, "duration_ms": 50 } }
  ],
  "capabilities": ["haptics"]
}
```

---

### 3) Long Release → Pattern Haptics

```json
{
  "schema_version": 1,
  "id": "xr_button_long_release",
  "mode": "single",
  "triggers": [
    { "trigger": "state",
      "entity_id": "xr.controller.right.button_primary",
      "to": "released",
      "for": "2s",
      "id": "long_release" }
  ],
  "actions": [
    { "action": "call", "service": "haptics.pattern",
      "data": { "hand": "right", "pattern": "double_click" } }
  ],
  "capabilities": ["haptics"]
}
```

---

### 4) Zone Lighting + Camera Exposure (enter/leave with parallel)

```json
{
  "schema_version": 1,
  "id": "zone_lighting",
  "alias": "Adaptive cave lighting",
  "mode": "restart",
  "triggers": [
    { "trigger": "zone", "entity_id": "player.avatar", "zone": "dark_cave", "event": "enter", "id": "enter" },
    { "trigger": "zone", "entity_id": "player.avatar", "zone": "dark_cave", "event": "leave", "id": "leave" }
  ],
  "actions": [
    { "action": "choose",
      "choices": [
        { "when": [ { "condition": "trigger", "id": "enter" } ],
          "do": [
            { "action": "parallel", "do": [
              { "action": "call", "service": "light.turn_on",
                "target": { "entity_id": "light.player_torch" },
                "data": { "brightness": 255, "transition": 2 } },
              { "action": "call", "service": "audio.play", "data": { "sound": "torch_ignite", "volume": 0.7 } },
              { "action": "call", "service": "camera.set_exposure", "data": { "exposure": 1.5, "adaptation_time": 3 } }
            ] }
          ] },
        { "when": [ { "condition": "trigger", "id": "leave" } ],
          "do": [
            { "action": "call", "service": "light.turn_off",
              "target": { "entity_id": "light.player_torch" }, "data": { "transition": 1 } },
            { "action": "call", "service": "camera.set_exposure", "data": { "exposure": 1.0, "adaptation_time": 2 } }
          ] }
      ]
    }
  ],
  "capabilities": ["zones"]
}
```

---

### 5) Low Health Warning (repeat‑until)

```json
{
  "schema_version": 1,
  "id": "combat_low_health",
  "alias": "Low health loop",
  "mode": "single",
  "triggers": [
    { "trigger": "numeric_state", "entity_id": "player.health", "below": 30, "id": "critical" }
  ],
  "conditions": [
    { "condition": "state", "entity_id": "player.status", "is": "alive" },
    { "condition": "not", "conditions": [ { "condition": "state", "entity_id": "game.mode", "is": "cutscene" } ] }
  ],
  "actions": [
    { "action": "repeat",
      "until": [ { "condition": "numeric_state", "entity_id": "player.health", "above": 29 } ],
      "do": [
        { "action": "parallel", "do": [
          { "action": "call", "service": "haptics.pulse", "data": { "hand": "both", "amplitude": 0.8, "duration_ms": 200 } },
          { "action": "call", "service": "vfx.screen_effect", "data": { "effect": "blood_vignette", "intensity": 0.7 } },
          { "action": "call", "service": "audio.play", "data": { "sound": "heartbeat_fast", "volume": 0.9 } }
        ] },
        { "action": "delay", "for": "1s" }
      ]
    }
  ],
  "capabilities": ["haptics"]
}
```

---

### 6) Time‑Based Telemetry (skip when no players)

```json
{
  "schema_version": 1,
  "id": "telemetry_5min",
  "alias": "Periodic telemetry",
  "mode": "single",
  "triggers": [ { "trigger": "time", "pattern": { "minutes": "*/5" } } ],
  "actions": [
    { "action": "choose",
      "choices": [
        { "when": [ { "condition": "expr", "expr": "state('sensor.active_players') == 0" } ],
          "do": [
            { "action": "call", "service": "log.debug", "data": { "message": "No active players, skipping telemetry" } },
            { "action": "stop", "reason": "No active players" }
          ] }
      ],
      "otherwise": [
        { "action": "vars", "set": {
          "telemetry": {
            "timestamp": "${now()}",
            "players": "${state('sensor.active_players')}",
            "fps": "${state('sensor.average_fps')}",
            "memory_mb": "${state('sensor.memory_usage')}",
            "scene": "${state('game.current_scene')}"
          }
        } },
        { "action": "call", "service": "http.post",
          "data": { "url": "https://telemetry.example.com/ingest", "json": "${vars.telemetry}", "timeout": 10 } }
      ]
    }
  ],
  "capabilities": ["http"]
}
```

---

### 7) Dynamic Quest Progression (wait with timeout)

```json
{
  "schema_version": 1,
  "id": "quest_handler",
  "alias": "Quest: ancient_artifact",
  "mode": "parallel",
  "max": 5,
  "triggers": [
    { "trigger": "event", "event_type": "quest.objective_complete", "match": { "quest_id": "ancient_artifact" } }
  ],
  "actions": [
    { "action": "choose",
      "choices": [
        { "when": [ { "condition": "expr", "expr": "trigger.match.objective_id == 'collect_fragments'" } ],
          "do": [
            { "action": "call", "service": "ui.show_notification",
              "data": { "title": "Fragments Collected", "message": "You have all three fragments!", "duration": 5000 } },
            { "action": "call", "service": "object.spawn",
              "data": { "prefab": "ancient_portal", "position": "${state('marker.temple_entrance')}", "activate_after": 2 } },
            { "action": "wait",
              "until": { "condition": "expr", "expr": "state('zone.temple_entrance.occupancy') > 0" },
              "timeout": "10m",
              "continue_on_timeout": false },
            { "action": "call", "service": "scene.load", "data": { "scene": "ancient_temple_interior", "transition": "fade_to_black" } }
          ] },
        { "when": [ { "condition": "expr", "expr": "trigger.match.objective_id == 'defeat_guardian'" } ],
          "do": [
            { "action": "parallel", "do": [
              { "action": "call", "service": "quest.complete", "data": {
                "quest_id": "ancient_artifact", "rewards": { "xp": 5000, "items": ["legendary_sword"] } } },
              { "action": "call", "service": "achievement.unlock", "data": { "achievement_id": "guardian_slayer" } },
              { "action": "call", "service": "cutscene.play", "data": { "cutscene_id": "artifact_revealed" } }
            ] }
          ] }
      ]
    }
  ]
}
```

---

### 8) Performance Optimizer (repeat until FPS recovers)

```json
{
  "schema_version": 1,
  "id": "perf_optimizer",
  "alias": "Auto reduce quality",
  "mode": "single",
  "variables": { "target_fps": 60, "min_fps": 30 },
  "triggers": [
    { "trigger": "numeric_state", "entity_id": "sensor.current_fps", "below": 30, "for": "5s" }
  ],
  "conditions": [
    { "condition": "state", "entity_id": "settings.auto_performance", "is": "enabled" }
  ],
  "actions": [
    { "action": "call", "service": "log.warning",
      "data": { "message": "FPS dropped below ${vars.min_fps}, optimizing..." } },
    { "action": "repeat",
      "until": [ { "condition": "or", "conditions": [
        { "condition": "numeric_state", "entity_id": "sensor.current_fps", "above": 45 },
        { "condition": "state", "entity_id": "graphics.quality", "is": "very_low" }
      ] } ],
      "do": [
        { "action": "call", "service": "graphics.reduce_quality", "data": { "step": 1 } },
        { "action": "delay", "for": "2s" },
        { "action": "call", "service": "log.info",
          "data": { "message": "Quality: ${state('graphics.quality')}, FPS: ${state('sensor.current_fps')}" } }
      ]
    }
  ]
}
```

---

### 9) Time Window + Weekday Guard (cron style)

```json
{
  "schema_version": 1,
  "id": "weekday_daily_summary",
  "alias": "Weekday 8am summary",
  "mode": "single",
  "triggers": [ { "trigger": "time", "at": "08:00:00" } ],
  "conditions": [ { "condition": "time", "weekday": ["mon","tue","wed","thu","fri"] } ],
  "actions": [
    { "action": "call", "service": "log.info",
      "data": { "message": "Weekday summary at ${now()}" } }
  ]
}
```

---

### 10) Wait with Timeout and Fallback (unifies wait\_for\_trigger/template)

```json
{
  "schema_version": 1,
  "id": "portal_wait_then_fallback",
  "alias": "Wait near portal then hint",
  "mode": "single",
  "triggers": [ { "trigger": "event", "event_type": "portal.spawned" } ],
  "actions": [
    { "action": "wait",
      "until": { "condition": "expr", "expr": "state('distance.player_to_portal') < 2.0" },
      "timeout": "30s",
      "continue_on_timeout": true },
    { "action": "choose",
      "choices": [
        { "when": [ { "condition": "expr", "expr": "state('distance.player_to_portal') < 2.0" } ],
          "do": [ { "action": "call", "service": "ui.show_notification",
                   "data": { "message": "Portal engaged!", "duration": 2000 } } ] }
      ],
      "otherwise": [
        { "action": "call", "service": "ui.show_notification",
          "data": { "message": "Move closer to the portal to enter.", "duration": 4000 } }
      ]
    }
  ]
}
```

---

## Appendix A — Recommended Defaults & Policies

* **Mode default**: `single`.
* **Trigger options default**: `{ debounce_ms:0, throttle_ms:0, cooldown_ms:0, distinct:true }`.
* **Time source default**: `game (unscaled)` for rate logic; `realtime` for wall‑clock.
* **HTTP**: 5 req/min per rule by default; 256 KB body cap; redact `Authorization` in logs.
* **Interpolation**: only in `service.data.*` and `log` messages.
* **Drop policy**: In `queued`, if queue full, drop the **newest** (or configurable). Log a warning counter.
* **Entity typing**: every entity has `{type, unit?}`; numeric comparisons fail at load if types mismatch.

---

## Appendix B — Minimal Expression Cheatsheet

* **Values**: `42`, `3.14`, `'text'`, `true/false`
* **State**: `state("player.health")`, `attr("player.avatar","y")`
* **Vars**: `vars.threshold`
* **Trigger**: `trigger.id`, `trigger.match.*` (for event triggers)
* **Time**: `now()` → ISO string (use only for logs), `ticks()` → ms since engine start
* **Ops**: `!`, `&&`, `||`, `==`, `!=`, `<`, `<=`, `>`, `>=`

Examples:

* `state("sensor.temp") > 28.5 && state("game.mode") == 'playing'`
* `vars.min <= state("player.health") && state("player.health") < vars.max`

---

If you want, I can also provide:

* A compact JSON Schema file for v0.1 to feed the linter.
* A “service registry manifest” template (YAML/JSON) to define services, args, ranges, and thread affinity in one place.
