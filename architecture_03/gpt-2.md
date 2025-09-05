Below is a consolidated **final proposal** that merges the strongest ideas across the three engine proposals and the LLM‑authoring guidance. It includes a fully updated **Implementation Plan** and a **curated, “golden” example set** that demonstrates all key patterns in the unified schema.

---

## Unified ECA Rules Engine for Quest VR — **Implementation Plan (v1.1)**

### What we’re keeping (proven-good)

* **Deterministic, low/zero‑allocation ECA core** with typed entities/services, hot‑path rule evaluation ≤0.5 ms, and guardrails for VR framerate.&#x20;
* **Per‑rule execution modes** (`single|restart|queue|parallel`) with bounded concurrency and clear cancellation semantics.&#x20;
* **Compact, IL2CPP‑safe expression VM** + string interpolation **only** in action data.&#x20;
* **Tiny, explicit per‑rule FSM compilation** + **timer wheel** for O(1) timeouts + `.uar` compiled binary packs for fast load.&#x20;

### What we’re adding (to stretch on perf & authoring)

* **Three‑tier perf model**: a Burst‑compiled, NativeCollections‑based fast lane for spatial/ per‑frame logic; a clean .NET core for business logic; and an optional reactive layer for temporal patterns.&#x20;
* **LLM‑robust authoring contract**: explicit discriminators, consistent arrays, enums instead of booleans, inline units/ranges in field names, and staged validation with precise, corrective error messages.&#x20;

---

### 0) Goals & Success Criteria

* **Perf:** p95 evaluation per fired rule **≤ 0.5 ms** (Quest 2/3); hot path steady‑state allocs \~0.
* **Reliability:** 1‑hour soak with zero crashes; automatic recovery from invalid rule packs.
* **Scale:** 100–150 active rules at 72–120 Hz without frame degradation.
* **DX:** Editor hot‑reload, “why didn’t it fire?” traces, linter with fix‑its, replay simulator.&#x20;

---

### 1) Architecture

**Core (.NET Standard 2.1, IL2CPP‑safe)**

* **RuleLoader → Validator → Compiler**: JSON → AST → per‑rule **FSM** + expression bytecode; strings resolved to **int IDs** (entities, zones, services).&#x20;
* **EventBus**: lock‑free ring buffer of value‑type events; preindexed subscriber lists (no string hashing at runtime).&#x20;
* **ConditionEval**: tiny, typed DSL; constant folding at load; no reflection.&#x20;
* **ActionExec**: deterministic per‑rule queue, cooperative cancellation, coalescing.&#x20;
* **Timers**: **timer wheel**/calendar queue; all durations normalized to `*_ms` ints at compile time.&#x20;
* **Registries**: typed **EntityRegistry** (read‑model) and **ServiceRegistry** (write/calls) with argument schemas & thread‑affinity.&#x20;

**Unity Adapter**

* PlayerLoop hook (Update/FixedUpdate), **main‑thread hop** for Unity APIs, non‑alloc physics & zones, Meta XR input bridge, and services (haptics, UI, audio, scene, camera, HTTP).&#x20;

**Perf Tiers (optional, opt‑in where it pays)**

* **Tier‑1 (Critical):** Burst jobs + Native collections for spatial queries/common reductions.
* **Tier‑2 (Core):** Managed .NET core (single‑thread, deterministic).
* **Tier‑3 (Reactive):** R3/UniTask for complex temporal patterns (kept off hot paths).&#x20;

---

### 2) Unified Authoring Schema (v1.1)

> JSON authoring compiled to a compact `.uar` binary. The schema is **LLM‑robust**: explicit discriminators, arrays where multiplicity is possible, enum strings instead of booleans, and inline units/ranges in field names.&#x20;

**Top level**

```jsonc
{
  "schema_version": "1.1.0",
  "id": "string",
  "alias": "string?",
  "description": "string?",
  "mode": "single|restart|queue|parallel",
  "max_instances_1_32": 1,
  "vars": { },
  "on": [ /* Trigger[] */ ],
  "if": [ /* Condition[] */ ],
  "do": [ /* Action[] */ ],
  "capabilities": ["http","zones","haptics"]?,
  "hints": { "tier": "critical|standard|reactive", "estimated_ms": 0.10, "can_batch": true }?
}
```

**Trigger (shared options)**

```jsonc
"options": {
  "debounce_ms_min_0": 0,
  "throttle_ms_min_0": 0,
  "cooldown_ms_min_0": 0,
  "distinct_enum": "deep|shallow|off"
}
```

**Triggers**

```jsonc
{ "type": "event", "name": "string", "data_match": { }, "id": "t?" }

{ "type": "state", "entity": "string", "from": ["enum"]?, "to": ["enum"]?, "for_ms_min_0": 0, "attr": "string?", "id": "t?" }

{ "type": "numeric", "entity": "string", "above": 0.0?, "below": 0.0?, "for_ms_min_0": 0, "id": "t?" }

{ "type": "zone", "entity": "string", "zone": "string", "event": "enter|leave", "id": "t?" }

{ "type": "time", "at": "HH:MM:SS"?, "every": { "hours": "*/n"?, "minutes": "*/n"?, "seconds": "*/n"?, "align": "HH:MM:SS" }?, "cron": "string?" }

{ "type": "pattern", "within_ms": 300, "steps": [ { "event": { "name":"string", "data": { } } } ], "id": "t?" }
```

**Conditions**

```jsonc
{ "type": "state",   "entity": "string", "is": ["enum"], "attr": "string?" }
{ "type": "numeric", "entity": "string", "above": 0.0?, "below": 0.0? }
{ "type": "time",    "after": "HH:MM:SS"?, "before": "HH:MM:SS"?, "weekday": ["mon","tue","wed","thu","fri","sat","sun"]? }
{ "type": "trigger", "id": ["t","t2"] }                 // gate by which trigger(s) fired
{ "type": "expr",    "expr": "typed mini-DSL" }
{ "type": "and|or|not", "conditions": [ /* Condition[] */ ] }
```

**Actions**

```jsonc
{ "type": "call", "service": "domain.name",
  "target": { "entity": ["string"]? },
  "data": { /* string interpolation allowed as ${...} */ } }

{ "type": "wait", "for_ms": 1000 }
{ "type": "wait", "until": { /* Condition */ }, "timeout_ms": 30000, "on_timeout": "continue|stop" }

{ "type": "choose",
  "when": [ { "if": [ /* Condition[] */ ], "do": [ /* Action[] */ ] } ],
  "else": [ /* Action[] */ ] }

{ "type": "parallel", "do": [ /* Action[] */ ] }
{ "type": "repeat", "count_1_100": 3?, "until": [ /* Condition[] */ ]?, "do": [ /* Action[] */ ] }
{ "type": "set_vars", "vars": { "k": "v" } }
{ "type": "stop", "reason": "string", "error": "no|soft|hard" }   // enum, not boolean
```

**Expressions & interpolation (engine‑safe)**

* `state("entity.id")`, `attr("entity.id","name")`, `vars.x`, `trigger.id`, `trigger.data.*`, `ticks()` (ms since engine start).
* Ops: `! && || == != < <= > >=` and arithmetic; funcs: `clamp|min|max|abs`.
* **Interpolation only in strings** (action `data`/`log`), e.g., `"message": "FPS=${state('sensor.fps')}"`.&#x20;

**Why these choices?** They reflect the v0.1 JSON surface and trigger options, the v1 FSM compilation model, and the v2 performance hints—**but** expressed with LLM‑friendly constraints (explicit enums, consistent arrays, inline `*_ms` units, and corrective errors).

---

### 3) Threading & Time

* **Single‑threaded core** ticked from `Update`. Adapter marshals Unity calls onto main thread. Async IO completes back on a controlled point.&#x20;
* **IClock** is the single source of time; scheduler uses **timer wheel**. Wall‑clock for cron/weekday; unscaled game time for rate logic.&#x20;
* Opt‑in **Burst jobs** for spatial grids/proximity/fan‑out scans.&#x20;

---

### 4) Memory & Allocation Strategy

* Value‑type event payloads; pre‑sized subscriber lists and object pools (RunContext, TriggerFrame).
* No LINQ or closure allocations in hot path; IL2CPP‑safe delegates only.&#x20;

---

### 5) Tooling & Developer Experience

* **Linter** (JSON Schema + semantic validation + perf heuristics).
* **Compiler CLI**: `validate → compile (.uar) → disasm → stats`.
* **Simulator**: deterministic replay from recorded event traces.
* **Editor Window**: live rule states, last trigger, cooldowns/timers, counters, and **“why didn’t this fire?”** traces.&#x20;
* **LLM guardrails**: enforce consistent arrays, enum strings, inline units; emit **structured error messages** with “Did you mean …?” suggestions.&#x20;

---

### 6) Security & Safety

* **Capabilities allowlist** per rule; service arg schemas with numeric clamps and thread affinity.
* **HTTP**: domain allowlist, timeouts, body caps, and secret redaction in logs.&#x20;

---

### 7) Testing & Performance

* **Unit**: parser, expression VM, timer wheel, FSM transitions.
* **Property‑based**: numeric boundaries, time windows, pattern triggers.
* **Integration**: adapter with mocked XR/zones; determinism tests for mode semantics.
* **Device perf**: synthetic workloads (N rules × M events/s), IL2CPP on Quest, p95/p99 latencies, allocation snapshots.

---

### 8) Packaging & CI

* Packages: `Rules.Core` (NuGet), `Rules.UnityAdapter` (UPM), `Rules.Tools` (CLI), `Rules.Samples`.
* CI: .NET tests + Unity edit/play mode, device smoke, perf baseline per PR.&#x20;

---

### 9) Phased Delivery (pragmatic)

* **Phase 1 — Core MVP (2–3 wks):** AST/validation, `event|state|numeric|time|zone|pattern`, `state|numeric|time|expr|trigger` conditions, `call|wait|choose|parallel|repeat|set_vars|stop` actions, FSM compiler, timer wheel, registries, dry‑run + logs. Gate: unit/property tests + synthetic p95 < 0.5 ms.
* **Phase 2 — Unity Adapter (2–3 wks):** Meta XR input; zones; services (haptics/ui/audio/scene/camera/http); Editor Window + Playground; IL2CPP smoke.&#x20;
* **Phase 3 — Perf & Scale (1–2 wks):** pools tuning; compiled `.uar` packs; stress @100+ rules; soak tests; **optional Burst spatial fast‑lane** for hotspots.&#x20;
* **Phase 4 — Polish (as needed):** cron, more diagnostics, schema migration helpers, authoring docs & golden set.

---

### 10) Risks & Mitigations

* **Thermal throttling / spikes:** dynamic evaluation‑rate caps + quality nudges; keep 30% headroom.&#x20;
* **LLM authoring errors:** schema patterns that coerce safely; progressive validation with corrective suggestions.&#x20;
* **IL2CPP edge cases:** zero runtime codegen; explicit delegates; weekly device tests.&#x20;

---

## Curated “Golden” Example Set (v1.1 schema)

> All times are `*_ms`. Arrays are used consistently for fields that can be plural. Interpolation allowed **only** in action strings (`data.*`). (These examples adapt the original samples to the unified v1.1 surface.)

### 1) App start → log (minimal)

```json
{
  "schema_version": "1.1.0",
  "id": "app_start_log",
  "mode": "single",
  "on": [ { "type": "event", "name": "app.started", "id": "start" } ],
  "do": [
    { "type": "call", "service": "log.info",
      "data": { "message": "Application started at ${ticks()} ms" } }
  ]
}
```

Pattern source: minimal event→action, good for smoke tests.&#x20;

### 2) Right button press → haptic tap (cooldown)

```json
{
  "schema_version": "1.1.0",
  "id": "xr_button_haptic",
  "mode": "single",
  "on": [
    { "type": "state", "entity": "xr.controller.right.button_primary",
      "to": ["pressed"], "id": "press",
      "options": { "distinct_enum": "deep", "cooldown_ms_min_0": 50 } }
  ],
  "if": [ { "type": "state", "entity": "game.mode", "is": ["playing","paused"] } ],
  "do": [
    { "type": "call", "service": "haptics.pulse",
      "data": { "hand": "right", "amplitude_0_to_1": 0.5, "duration_ms": 50 } }
  ],
  "capabilities": ["haptics"]
}
```

Pattern source: button→haptics with de‑bouncing.&#x20;

### 3) Long release → haptic pattern

```json
{
  "schema_version": "1.1.0",
  "id": "xr_button_long_release",
  "mode": "single",
  "on": [
    { "type": "state", "entity": "xr.controller.right.button_primary",
      "to": ["released"], "for_ms_min_0": 2000, "id": "long_release" }
  ],
  "do": [
    { "type": "call", "service": "haptics.pattern",
      "data": { "hand": "right", "pattern": "double_click" } }
  ],
  "capabilities": ["haptics"]
}
```

Pattern source: state persistence (`for_ms`) gating.&#x20;

### 4) Zone enter/leave → torch + exposure (parallel)

```json
{
  "schema_version": "1.1.0",
  "id": "zone_lighting",
  "mode": "restart",
  "on": [
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "enter", "id": "enter" },
    { "type": "zone", "entity": "player.avatar", "zone": "dark_cave", "event": "leave", "id": "leave" }
  ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type":"trigger", "id": ["enter"] } ],
          "do": [
            { "type": "parallel", "do": [
              { "type": "call", "service": "light.turn_on",
                "target": { "entity": ["light.player_torch"] },
                "data": { "brightness_0_255": 255, "transition_ms": 2000 } },
              { "type": "call", "service": "audio.play", "data": { "sound": "torch_ignite", "volume_0_to_1": 0.7 } },
              { "type": "call", "service": "camera.set_exposure", "data": { "exposure": 1.5, "adaptation_ms": 3000 } }
            ] }
          ] },
        { "if": [ { "type":"trigger", "id": ["leave"] } ],
          "do": [
            { "type": "call", "service": "light.turn_off",
              "target": { "entity": ["light.player_torch"] },
              "data": { "transition_ms": 1000 } },
            { "type": "call", "service": "camera.set_exposure", "data": { "exposure": 1.0, "adaptation_ms": 2000 } }
          ] }
      ] }
  ],
  "capabilities": ["zones"]
}
```

Pattern source: multi‑trigger + `choose` + `parallel`.&#x20;

### 5) Low‑health loop → haptics + VFX until recovered

```json
{
  "schema_version": "1.1.0",
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
          { "type": "call", "service": "haptics.pulse", "data": { "hand": "both", "amplitude_0_to_1": 0.8, "duration_ms": 200 } },
          { "type": "call", "service": "vfx.screen_effect", "data": { "effect": "blood_vignette", "intensity_0_to_1": 0.7 } },
          { "type": "call", "service": "audio.play", "data": { "sound": "heartbeat_fast", "volume_0_to_1": 0.9 } }
        ] },
        { "type": "wait", "for_ms": 1000 }
      ]
    }
  ],
  "capabilities": ["haptics"]
}
```

Pattern source: `repeat…until` with numeric guard.&#x20;

### 6) Every 5 min → telemetry (skip when no players; stop path)

```json
{
  "schema_version": "1.1.0",
  "id": "telemetry_5min",
  "mode": "single",
  "on": [ { "type": "time", "every": { "minutes": "*/5", "align": "00:00:00" }, "id": "tick" } ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type":"expr", "expr":"state('sensor.active_players') == 0" } ],
          "do": [
            { "type": "call", "service": "log.debug", "data": { "message": "No active players, skipping telemetry" } },
            { "type": "stop", "reason": "No active players", "error": "no" }
          ] }
      ],
      "else": [
        { "type": "call", "service": "http.post",
          "data": {
            "url": "https://telemetry.example/ingest",
            "json": {
              "ts_ms": "${ticks()}",
              "players": "${state('sensor.active_players')}",
              "fps": "${state('sensor.average_fps')}",
              "mem_mb": "${state('sensor.memory_usage')}",
              "scene": "${state('game.current_scene')}"
            },
            "timeout_ms": 10000
          } }
      ]
    }
  ],
  "capabilities": ["http"]
}
```

Pattern source: time pattern + conditional stop.&#x20;

### 7) Quest progression → wait for zone, then load scene

```json
{
  "schema_version": "1.1.0",
  "id": "quest_handler",
  "mode": "parallel",
  "max_instances_1_32": 5,
  "on": [ { "type": "event", "name": "quest.objective_complete", "id": "q" } ],
  "do": [
    { "type": "choose",
      "when": [
        { "if": [ { "type":"expr", "expr":"trigger.data.quest_id == 'ancient_artifact' && trigger.data.objective_id == 'collect_fragments'" } ],
          "do": [
            { "type": "call", "service": "ui.show_notification",
              "data": { "title": "Fragments Collected", "message": "You have all three fragments!", "duration_ms": 5000 } },
            { "type": "call", "service": "object.spawn",
              "data": { "prefab": "ancient_portal", "position": "${state('marker.temple_entrance')}", "activate_after_ms": 2000 } },
            { "type": "wait",
              "until": { "type": "expr", "expr": "state('zone.temple_entrance.occupancy') > 0" },
              "timeout_ms": 600000, "on_timeout": "stop" },
            { "type": "call", "service": "scene.load", "data": { "scene": "ancient_temple_interior", "transition": "fade_to_black" } }
          ] }
      ],
      "else": []
    }
  ]
}
```

Pattern source: event branching + guarded `wait`.&#x20;

### 8) Auto performance reduction until FPS recovers

```json
{
  "schema_version": "1.1.0",
  "id": "perf_optimizer",
  "mode": "single",
  "vars": { "min_fps": 30 },
  "on": [ { "type": "numeric", "entity": "sensor.current_fps", "below": 30, "for_ms_min_0": 5000, "id": "low_fps" } ],
  "if": [ { "type": "state", "entity": "settings.auto_performance", "is": ["enabled"] } ],
  "do": [
    { "type": "call", "service": "log.warning", "data": { "message": "FPS < ${vars.min_fps} for 5s, optimizing..." } },
    { "type": "repeat",
      "until": [ { "type": "or", "conditions": [
        { "type": "numeric", "entity": "sensor.current_fps", "above": 45 },
        { "type": "state",   "entity": "graphics.quality", "is": ["very_low"] }
      ] } ],
      "do": [
        { "type": "call", "service": "graphics.reduce_quality", "data": { "step_1_3": 1 } },
        { "type": "wait", "for_ms": 2000 },
        { "type": "call", "service": "log.info",
          "data": { "message": "Quality=${state('graphics.quality')} FPS=${state('sensor.current_fps')}" } }
      ]
    }
  ]
}
```

Pattern source: repeat‑until with dual exit condition.&#x20;

### 9) Weekday 08:00 summary

```json
{
  "schema_version": "1.1.0",
  "id": "weekday_daily_summary",
  "mode": "single",
  "on": [ { "type": "time", "at": "08:00:00", "id": "morning" } ],
  "if": [ { "type": "time", "weekday": ["mon","tue","wed","thu","fri"] } ],
  "do": [
    { "type": "call", "service": "log.info",
      "data": { "message": "Weekday summary at ${ticks()} ms" } }
  ]
}
```

Pattern source: wall‑clock + weekday guard.&#x20;

### 10) Wait with timeout → fallback hint

```json
{
  "schema_version": "1.1.0",
  "id": "portal_wait_then_hint",
  "mode": "single",
  "on": [ { "type": "event", "name": "portal.spawned", "id": "spawn" } ],
  "do": [
    { "type": "wait",
      "until": { "type": "expr", "expr": "state('distance.player_to_portal') < 2.0" },
      "timeout_ms": 30000, "on_timeout": "continue" },
    { "type": "choose",
      "when": [
        { "if": [ { "type": "expr", "expr": "state('distance.player_to_portal') < 2.0" } ],
          "do": [ { "type": "call", "service": "ui.show_notification", "data": { "message": "Portal engaged!", "duration_ms": 2000 } } ] }
      ],
      "else": [
        { "type": "call", "service": "ui.show_notification",
          "data": { "message": "Move closer to the portal to enter.", "duration_ms": 4000 } }
      ]
    }
  ]
}
```

Pattern source: unified `wait` + fallback.&#x20;

### 11) Double‑tap pattern (within 300 ms)

```json
{
  "schema_version": "1.1.0",
  "id": "double_tap_action",
  "mode": "single",
  "on": [
    { "type": "pattern", "within_ms": 300, "id": "double_tap",
      "steps": [
        { "event": { "name":"xr.button", "data": { "hand":"right", "btn":"primary", "state":"pressed" } } },
        { "event": { "name":"xr.button", "data": { "hand":"right", "btn":"primary", "state":"pressed" } } }
      ] }
  ],
  "do": [
    { "type": "call", "service": "ui.show_notification", "data": { "message": "Double tap!", "duration_ms": 1500 } },
    { "type": "call", "service": "haptics.pattern", "data": { "hand":"right", "pattern":"double_click" } }
  ]
}
```

Pattern source: compiled sequence matcher.&#x20;

### 12) Contextual grab (gesture + raycast + haptics preview)

```json
{
  "schema_version": "1.1.0",
  "id": "context_grab",
  "mode": "single",
  "hints": { "tier": "critical" },
  "on": [
    { "type": "event", "name": "xr.gesture", "data_match": { "gesture": "pinch", "confidence": 0.8 }, "id": "pinch" }
  ],
  "if": [
    { "type": "expr", "expr": "state('raycast.hit') == true && state('raycast.layer') == 'grabbable' && state('player.hands_free') == true" }
  ],
  "do": [
    { "type": "call", "service": "object.attach_to_hand", "data": { "hand": "trigger_hand", "method": "physics_joint" } },
    { "type": "call", "service": "haptics.pulse", "data": { "hand": "trigger_hand", "amplitude_0_to_1": 0.6, "duration_ms": 30 } },
    { "type": "call", "service": "audio.spatial", "data": { "sound": "grab_success", "position": "${state('object.position')}" } }
  ],
  "capabilities": ["haptics","zones"]
}
```

Pattern source: tier hint + composite conditions; aligns with the “fast lane” when needed.&#x20;

---

## Implementation Notes (for making this shippable)

* **Entity/Service/Zone manifests**: provide a single registry source (JSON/YAML → codegen) so LLMs and designers autocomplete valid names. FSM compiler binds to IDs at build time.&#x20;
* **Authoring reliability**: enforce explicit arrays and enum strings; embed units/ranges in field names; document type‑coercion (e.g., `"2000"` → `2000`). Provide **structured, corrective errors** with “Did you mean?” suggestions.&#x20;
* **Perf hotspots**: consider Burst‑compiled spatial acceleration (uniform grid / cell hashing) for large zone counts or dense object queries; keep it optional and internal to the adapter.&#x20;

---

### Definition of Done (v1.1)

* Core + adapter pass unit/integration tests; p95 eval ≤0.5 ms on device with 100 rules; hot reload; Editor diagnostics; **this example set** runs end‑to‑end in the playground scene.

---

If you’d like, I can turn this into:

* a compact **JSON Schema** for v1.1,
* a **service registry manifest** template, and
* a **starter repo layout** (Core/Adapter/Tools/Samples) pre‑wired for IL2CPP + Quest builds.
