Below is a single, merged **final proposal** that keeps the best ideas from all three drafts and removes ambiguity. It gives you a **fully updated, comprehensive Implementation Plan** first, then a **curated set of examples** that cover the key patterns your team will actually ship.

---

## Unity Rules Engine (Quest VR) — Final Combined Implementation Plan (v1.0)

### Executive summary (what we’re building)

* **Model:** Event‑Condition‑Action (ECA) rules that compile into **tiny, explicit per‑rule FSMs** for determinism, profiling clarity, and IL2CPP safety.&#x20;
* **Shape:** A **standalone .NET Standard 2.1 core** + **Unity adapter**. Author in JSON; validate, lint, and compile to a compact binary package for fast, zero‑alloc runtime use.&#x20;
* **Perf routing:** A practical **three‑tier execution path**: *Critical* work can run via Burst + Native Collections; *Standard* ECA runs in the core; *Reactive* streams (temporal/patterns) use R3/UniTask when helpful—kept out of hot paths.&#x20;
* **Authoring contract:** A **lean JSON schema** (v1.0) with clear trigger/condition/action primitives, tiny typed expression DSL, safe string interpolation, and guardrails. (We adopt the compact JSON ergonomics while retaining advanced actions like `repeat` and `wait`.)&#x20;

---

### 1) Goals, scope, and success criteria

**Primary goal:** Deliver a deterministic, zero/low‑allocation rules engine for Quest (72–120 Hz) that LLMs and designers can co‑author confidently.

**Ship gates (v1.0):**

* **Latency:** p95 per‑rule evaluation < **0.5 ms** with 100 active rules on device; hot‑path triggers \~**≤100 µs** after warm‑up.&#x20;
* **Reliability:** zero crashes in 60‑minute soak; robust handling of invalid rule packs.&#x20;
* **DX:** strict validation + linter; dry‑run mode; hot reload in Editor; deterministic replay.&#x20;

**In scope:** input/spatial/UI/VFX/SFX/gameplay glue; complex temporal patterns; explicit concurrency semantics. **Out of scope (v1):** behavior trees/GOAP; network determinism (client‑local only).&#x20;

---

### 2) Architecture (how it fits together)

**Core engine (.NET Standard 2.1)**

* **Loader/Validator/Compiler:** JSON → AST → validated → compiled **RulePlan** (FSM per rule). Strings bound to integer IDs; expressions compiled to IL2CPP‑safe bytecode/VM. Produces a compact **.uar** package.&#x20;
* **EventBus:** value‑type events in a lock‑free ring buffer with prebuilt subscription indexes.&#x20;
* **ConditionEval:** tiny, typed, sandboxed expression DSL (no reflection).&#x20;
* **ActionExec:** deterministic, cancellable, coalescing dispatcher with explicit **mode** semantics.&#x20;
* **Scheduler/Timers:** **timer wheel** for O(1) amortized timeouts, monotonic clock abstraction.&#x20;
* **Entity & Service registries:** typed read model + allow‑listed service calls w/ schemas & thread affinity.&#x20;

**Unity adapter**

* PlayerLoop integration; input bridge (Meta XR → canonical `xr.*` events); non‑alloc spatial queries for zones/proximity; main‑thread marshaling; services (haptics, audio, UI, scene, camera, HTTP).&#x20;

**Performance routing (tiers)**

* **Tier: critical.** Burst‑compiled jobs, Native Collections, spatial acceleration (grid/adjacency) for high‑frequency proximity checks and other per‑frame sampling.&#x20;
* **Tier: standard.** Default ECA runtime (core engine) with zero‑alloc hot paths.&#x20;
* **Tier: reactive.** R3/UniTask streams for complex temporal patterns, buffered and bounded; never on the hot path.&#x20;

**Threading:** single‑threaded core from Unity’s perspective; background I/O terminates in the adapter and returns to main thread via controlled hand‑offs.&#x20;

---

### 3) Final authoring model (JSON v1.0)

**Top‑level**

```json
{
  "schema_version": 1,
  "id": "string",
  "alias": "string optional",
  "description": "string optional",
  "mode": "single|restart|queue|parallel",
  "max": 10,
  "vars": {},
  "on": [ /* triggers */ ],
  "if": [ /* optional conditions */ ],
  "do": [ /* actions */ ],
  "hints": { "tier": "critical|standard|reactive", "max_hz": 90 }
}
```

* `mode` semantics:
  `single` (ignore new while running), `restart` (cancel then start fresh), `queue` (size = `max`), `parallel` (≤ `max` instances).&#x20;

**Triggers** (all accept optional `options` with `debounce_ms`, `throttle_ms`, `cooldown_ms`, `distinct:true`)

```json
{ "type": "event",   "name": "string", "data": { }?, "id": "t1" }
{ "type": "state",   "entity": "string", "to": ["state"]?, "from": ["state"]?, "for": 200? , "attr": "value"?, "id": "t2" }
{ "type": "numeric", "entity": "string", "above": 0?, "below": 0?, "for": 200? , "id": "t3" }
{ "type": "zone",    "entity": "string", "zone": "string", "event": "enter|leave", "id": "t4" }
{ "type": "time",    "at": "HH:MM:SS"?, "every": { "minutes": "*/5" }? , "id": "t5" }
{ "type": "pattern", "within": 300, "steps": [ { "event": { "name": "xr.button", "data": { } } }, ... ], "id": "t6" }
```

* Compact JSON surfaces from v0.1, plus `pattern` from the FSM‑first design. &#x20;

**Conditions**

```json
{ "type": "state",   "entity": "string", "is": ["a","b"] , "attr": "value"? }
{ "type": "numeric", "entity": "string", "above": 0?, "below": 0? }
{ "type": "time",    "after": "HH:MM:SS"?, "before": "HH:MM:SS"?, "weekday": ["mon","tue"]? }
{ "type": "expr",    "expr": "state('sensor.fps') < 45 && vars.autoPerf == true" }
{ "type": "trigger", "id": "t2" }
{ "type": "and|or|not", "conditions": [ /* ... */ ] }
```

**Actions**

```json
{ "type": "call",      "service": "domain.action", "target": { "entity": "id" }?, "data": { } }
{ "type": "wait",      "for": 1000 }                          // or {"until": {condition}, "timeout": 5000, "on_timeout": "continue|stop"}
{ "type": "delay",     "for": 500 }                           // alias for wait/for
{ "type": "choose",    "when": [ { "if": [/*conds*/], "do": [/*acts*/] } ], "else": [/*acts*/] }
{ "type": "parallel",  "do": [ /* actions */ ] }
{ "type": "repeat",    "until": [/*conds*/]?, "count": 3?, "do": [ /* actions */ ] }
{ "type": "set_vars",  "vars": { "key": "value" } }
{ "type": "stop",      "reason": "string", "error": false }
```

* We keep `repeat`/`delay` from the simplified schema and `choose`/`parallel`/`pattern`/`set_vars` from the FSM‑first plan. &#x20;

**Expression DSL (sandboxed)**

* Selectors: `state("entity.id")`, `attr("entity.id","attr")`, `vars.*`, `trigger.id`, `now()`, `ticks()`.
* Ops: arithmetic, comparisons, `&& || !`, ternary `cond ? a : b`.
* Only string interpolation inside action `data` values via `"${expr}"`.&#x20;

**Durations**

* Accept `"200ms"`, `"1.5s"`, `"2s"`, `"00:00:02"` in authoring; compiler normalizes to ms.&#x20;

---

### 4) Engine & adapter details

**Event pipeline**

* `struct` events with integer keys; compile‑time subscriber tables; ring buffer with bounded capacity and clear drop policy (warn vs. back‑pressure).&#x20;

**Timers/clock**

* Single `IClock` for determinism; **timer wheel** for `for/timeout/every`.&#x20;

**Action execution**

* Deterministic per‑rule queue; instance cancellation for `restart`; coalescing (e.g., merge multiple haptics in a frame); per‑rule `onError` policy.&#x20;

**Entity & Service registries**

* Typed reads; safe write/call schemas with numeric clamps, units, and thread affinity (MainThread/Any). HTTP is allow‑listed and rate‑limited.&#x20;

**Performance routing**

* **Hints:** `{ "hints": { "tier": "critical|standard|reactive" } }` let the adapter route work: Burst job for dense spatial checks (grid hashing & neighbor queries), core for typical ECA, R3 for buffered temporal sequences—never allocating in hot loops.&#x20;

**Diagnostics**

* Structured logs, per‑rule counters, circular “why didn’t this fire?” buffers, Editor window to visualize active rules/timers, deterministic replay harness.&#x20;

---

### 5) Tooling & authoring experience

* **JSON Schema + Linter:** exhaustive errors, anti‑pattern warnings (unbounded parallel, missing cooldown).&#x20;
* **Compiler CLI** (`uarc`): validate/compile/disassemble/stats → `.uar` binary packages.&#x20;
* **Simulator CLI** (`uarsim`): feed event traces; prints decisions & actions for CI.&#x20;
* **Unity Editor window:** live state, triggers, timers, perf markers; hot reload in Editor; network hot reload in dev builds optional.&#x20;

---

### 6) Testing & performance

* **Unit:** parser/validator/expr/timer wheel/FSM transitions.
* **Property‑based:** numeric/time windows/pattern edges.
* **Integration:** curated examples; determinism under `mode` bursts.
* **Device:** IL2CPP builds; soak 30–60 min; allocation audits; p95/p99 latencies with synthetic loads (≥100 rules).&#x20;

---

### 7) Security & safety

* No reflection/codegen at runtime; expression VM only.
* HTTP domain allow‑list + timeouts + body caps; redact credentials.
* Capabilities allow‑list per rule pack; numeric clamps and type/unit checks at load.&#x20;

---

### 8) Versioning & migration

* `schema_version` increments on breaks; compiler migrates minor changes; `.uar` embeds engine version and is rejected with actionable guidance when incompatible.&#x20;

---

### 9) Phased plan (merge of the strongest sequencing)

**Phase 1 — Core MVP (2–3 weeks)**
AST/validation; triggers (`event|state|numeric|time|zone|pattern`), conditions, actions (`call|wait|delay|choose|parallel|repeat|set_vars|stop`), timer wheel, per‑rule executor, diagnostics, dry‑run. **Gate:** unit/property tests; synthetic p95 < 0.5 ms.&#x20;

**Phase 2 — Unity adapter (2–3 weeks)**
PlayerLoop hook; Meta XR input bridge; zones (non‑alloc queries); services (haptics/audio/ui/scene/http/camera/vfx); Editor window; hot reload (Editor). **Gate:** examples run w/ no steady‑state allocs; IL2CPP smoke on device.&#x20;

**Phase 3 — Perf & tiering (1–2 weeks)**
Burst job path + spatial accelerator for tier\:critical rules; bounded R3 streams for tier\:reactive; pool tuning; compile to `.uar`. **Gate:** 100–150 rules stable in target Hz bands; perf headroom under thermal load.&#x20;

**Phase 4 — Polish & docs (1–2 weeks)**
Cron/time extras, richer diagnostics, migration helpers, doc examples, replay harness UX.&#x20;

---

### 10) Deliverables

* **Packages:** `Rules.Core` (C# lib), `Rules.UnityAdapter` (UPM), `Rules.Tools` (linter/compiler/simulator), `Rules.Samples` (scenes + rules).
* **Docs:** Authoring guide, services manifest, perf/troubleshooting.&#x20;

---

## Curated example set (v1.0 schema)

> Each is copy‑pastable JSON. They cover hot‑path input, temporal patterns, spatial gating, concurrency modes, waits/timeouts, and perf tier hints.

### 1) App start → log (minimal)

```json
{
  "schema_version": 1,
  "id": "app_start_log",
  "mode": "single",
  "on": [ { "type": "event", "name": "app.started" } ],
  "do": [
    { "type": "call", "service": "log.info", "data": { "message": "Started at ${now()}" } }
  ]
}
```

### 2) Right button tap → haptic (cooldown; hot path)

```json
{
  "schema_version": 1,
  "id": "xr_button_haptic",
  "mode": "single",
  "hints": { "tier": "standard" },
  "on": [
    { "type": "state",
      "entity": "xr.controller.right.button_primary",
      "to": ["pressed"],
      "id": "press",
      "options": { "distinct": true, "cooldown_ms": 50 } }
  ],
  "if": [
    { "type": "state", "entity": "game.mode", "is": ["playing","paused"] }
  ],
  "do": [
    { "type": "call", "service": "haptics.pulse", "data": { "hand": "right", "amplitude": 0.5, "duration_ms": 50 } }
  ]
}
```

### 3) Long release → pattern haptics

```json
{
  "schema_version": 1,
  "id": "xr_button_long_release",
  "mode": "single",
  "on": [
    { "type": "state", "entity": "xr.controller.right.button_primary", "to": ["released"], "for": 2000, "id": "long_release" }
  ],
  "do": [
    { "type": "call", "service": "haptics.pattern", "data": { "hand": "right", "pattern": "double_click" } }
  ]
}
```

### 4) Zone entry/exit → torch + exposure (parallel)

```json
{
  "schema_version": 1,
  "id": "zone_lighting",
  "mode": "restart",
  "on": [
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "enter", "id": "enter" },
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "leave", "id": "leave" }
  ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type": "trigger", "id": "enter" } ],
          "do": [
            { "type": "parallel", "do": [
              { "type": "call", "service": "light.turn_on", "target": { "entity": "light.player_torch" }, "data": { "brightness": 255, "transition": 2000 } },
              { "type": "call", "service": "audio.play", "data": { "sound": "torch_ignite", "volume": 0.7 } },
              { "type": "call", "service": "camera.set_exposure", "data": { "exposure": 1.5, "adaptation_ms": 3000 } }
            ] }
          ] },
        { "if": [ { "type": "trigger", "id": "leave" } ],
          "do": [
            { "type": "call", "service": "light.turn_off", "target": { "entity": "light.player_torch" }, "data": { "transition": 1000 } },
            { "type": "call", "service": "camera.set_exposure", "data": { "exposure": 1.0, "adaptation_ms": 2000 } }
          ] }
      ]
    }
  ]
}
```

### 5) Low health loop (repeat‑until; VFX+SFX+haptics)

```json
{
  "schema_version": 1,
  "id": "combat_low_health",
  "mode": "single",
  "on": [ { "type": "numeric", "entity": "player.health", "below": 30, "id": "critical" } ],
  "if": [
    { "type": "state", "entity": "player.status", "is": ["alive"] },
    { "type": "not", "conditions": [ { "type": "state", "entity": "game.mode", "is": ["cutscene"] } ] }
  ],
  "do": [
    { "type": "repeat",
      "until": [ { "type": "numeric", "entity": "player.health", "above": 29 } ],
      "do": [
        { "type": "parallel", "do": [
          { "type": "call", "service": "haptics.pulse", "data": { "hand": "both", "amplitude": 0.8, "duration_ms": 200 } },
          { "type": "call", "service": "vfx.screen_effect", "data": { "effect": "blood_vignette", "intensity": 0.7 } },
          { "type": "call", "service": "audio.play", "data": { "sound": "heartbeat_fast", "volume": 0.9 } }
        ] },
        { "type": "delay", "for": 1000 }
      ]
    }
  ]
}
```

### 6) Periodic telemetry (time.every; skip when empty)

```json
{
  "schema_version": 1,
  "id": "telemetry_5min",
  "mode": "single",
  "on": [ { "type": "time", "every": { "minutes": "*/5" } } ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type": "expr", "expr": "state('sensor.active_players') == 0" } ],
          "do": [
            { "type": "call", "service": "log.debug", "data": { "message": "No active players, skipping" } },
            { "type": "stop", "reason": "No active players" }
          ] }
      ],
      "else": [
        { "type": "call", "service": "http.post",
          "data": {
            "url": "https://telemetry.example.com/ingest",
            "json": {
              "ts": "${now()}",
              "players": "${state('sensor.active_players')}",
              "fps": "${state('sensor.average_fps')}",
              "mem": "${state('sensor.memory_mb')}",
              "scene": "${state('game.current_scene')}"
            },
            "timeout": 10000
          }
        }
      ]
    }
  ]
}
```

### 7) Quest branch → wait for zone → load scene

```json
{
  "schema_version": 1,
  "id": "quest_progress",
  "mode": "parallel",
  "max": 5,
  "on": [ { "type": "event", "name": "quest.objective_complete", "data": { "quest_id": "ancient_artifact" } } ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type": "expr", "expr": "trigger.data.objective_id == 'collect_fragments'" } ],
          "do": [
            { "type": "call", "service": "ui.show_notification", "data": { "title": "Fragments Collected", "message": "You have all three fragments!", "duration": 5000 } },
            { "type": "call", "service": "object.spawn", "data": { "prefab": "ancient_portal", "position": "${state('marker.temple_entrance')}", "activate_after_ms": 2000 } },
            { "type": "wait",
              "until": { "type": "expr", "expr": "state('zone.temple_entrance.occupancy') > 0" },
              "timeout": 600000,
              "on_timeout": "stop" },
            { "type": "call", "service": "scene.load", "data": { "scene": "ancient_temple_interior", "transition": "fade_to_black" } }
          ] },
        { "if": [ { "type": "expr", "expr": "trigger.data.objective_id == 'defeat_guardian'" } ],
          "do": [
            { "type": "parallel", "do": [
              { "type": "call", "service": "quest.complete", "data": { "quest_id": "ancient_artifact", "rewards": { "xp": 5000, "items": ["legendary_sword"] } } },
              { "type": "call", "service": "achievement.unlock", "data": { "achievement_id": "guardian_slayer" } },
              { "type": "call", "service": "cutscene.play", "data": { "cutscene_id": "artifact_revealed" } }
            ] }
          ] }
      ]
    }
  ]
}
```

### 8) Performance optimizer (repeat until fps recovers)

```json
{
  "schema_version": 1,
  "id": "perf_optimizer",
  "mode": "single",
  "vars": { "target_fps": 60 },
  "on": [ { "type": "numeric", "entity": "sensor.current_fps", "below": 30, "for": 5000 } ],
  "if": [ { "type": "state", "entity": "settings.auto_performance", "is": ["enabled"] } ],
  "do": [
    { "type": "call", "service": "log.warning", "data": { "message": "FPS < 30 for 5s, optimizing..." } },
    { "type": "repeat",
      "until": [ { "type": "or", "conditions": [
        { "type": "numeric", "entity": "sensor.current_fps", "above": 45 },
        { "type": "state", "entity": "graphics.quality", "is": ["very_low"] }
      ] } ],
      "do": [
        { "type": "call", "service": "graphics.reduce_quality", "data": { "step": 1 } },
        { "type": "delay", "for": 2000 },
        { "type": "call", "service": "log.info", "data": { "message": "Quality=${state('graphics.quality')} FPS=${state('sensor.current_fps')}" } }
      ]
    }
  ]
}
```

### 9) Gesture pattern: double‑tap within 300 ms

```json
{
  "schema_version": 1,
  "id": "double_tap_action",
  "mode": "single",
  "on": [
    { "type": "pattern", "within": 300, "steps": [
      { "event": { "name": "xr.button", "data": { "hand": "right", "btn": "primary", "state": "pressed" } } },
      { "event": { "name": "xr.button", "data": { "hand": "right", "btn": "primary", "state": "pressed" } } }
    ], "id": "double_tap" }
  ],
  "do": [
    { "type": "call", "service": "ui.show_notification", "data": { "message": "Double tap!", "duration": 1500 } },
    { "type": "call", "service": "haptics.pattern", "data": { "hand": "right", "pattern": "double_click" } }
  ]
}
```

### 10) Weekday 8 AM guard (time + weekday)

```json
{
  "schema_version": 1,
  "id": "weekday_daily_summary",
  "mode": "single",
  "on": [ { "type": "time", "at": "08:00:00" } ],
  "if": [ { "type": "time", "weekday": ["mon","tue","wed","thu","fri"] } ],
  "do": [
    { "type": "call", "service": "log.info", "data": { "message": "Weekday summary at ${now()}" } }
  ]
}
```

### 11) Wait with timeout, then fallback hint

```json
{
  "schema_version": 1,
  "id": "portal_wait_then_hint",
  "mode": "single",
  "on": [ { "type": "event", "name": "portal.spawned" } ],
  "do": [
    { "type": "wait",
      "until": { "type": "expr", "expr": "state('distance.player_to_portal') < 2.0" },
      "timeout": 30000,
      "on_timeout": "continue" },
    { "type": "choose",
      "when": [
        { "if": [ { "type": "expr", "expr": "state('distance.player_to_portal') < 2.0" } ],
          "do": [ { "type": "call", "service": "ui.show_notification", "data": { "message": "Portal engaged!", "duration": 2000 } } ] }
      ],
      "else": [
        { "type": "call", "service": "ui.show_notification", "data": { "message": "Move closer to the portal to enter.", "duration": 4000 } }
      ]
    }
  ]
}
```

### 12) Proximity sampler (tier: critical example)

```json
{
  "schema_version": 1,
  "id": "danger_zone_alert",
  "mode": "single",
  "hints": { "tier": "critical", "max_hz": 90 },
  "on": [
    { "type": "zone", "entity": "player.avatar", "zone": "lava_pit", "event": "enter", "id": "enter" }
  ],
  "do": [
    { "type": "parallel", "do": [
      { "type": "call", "service": "ui.show_notification", "data": { "message": "Warning: Hazardous Area", "duration": 2000 } },
      { "type": "call", "service": "audio.play", "data": { "sound": "warning_beep", "volume": 0.8 } }
    ] }
  ]
}
```

---

## Why this is the “best of” the three drafts

* We kept the **FSM‑compiled ECA core**, `.uar` packaging, **timer wheel**, and deterministic concurrency from the first plan because they maximize on‑device performance and debuggability.&#x20;
* We adopted the **three‑tier performance routing** (Burst jobs for critical sampling, R3/UniTask for bounded reactive patterns) so you can scale patterns without polluting the hot path.&#x20;
* We standardized on a **compact JSON schema** with clear trigger/condition/action primitives, **`repeat`/`delay`** actions, and strong validation to keep authoring simple and AOT‑friendly.&#x20;

If you want, I can turn this into a repo scaffold (projects, folders, stubbed compiler, sample scenes) that matches the phases above.
