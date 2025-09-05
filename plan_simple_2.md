Below is a **weekend‑scope, UniTask‑based** implementation plan that honors your constraints:

* **No coroutines** → use **UniTask** patterns
* **MIT‑licensed JSON** → use **com.unity.nuget.newtonsoft-json** (MIT) only; **no schema libs** this weekend
* **Minimal/optional validation** → permissive parsing + gentle coercions
* **No Quest/XR libs** → desktop mouse/keyboard inputs; audio/logging as outputs
* **Goal** → *E2E demo*: LLM JSON → local rule → triggers → conditions → actions → audible/logged result

The plan preserves your authoring principles (explicit types, arrays‑always, units‑in‑field‑names, string enums) so what you validate this weekend scales to your longer‑term design. &#x20;

---

## 0) Scope & Non‑Goals

**In**:

* Trigger types: `event`, `numeric_threshold`, `pattern`, `time_schedule`
* Condition types: `state_equals`, `numeric_compare`, `expression` (tiny interpreter), `trigger_fired`
* Actions: `service_call`, `wait_duration`, `branch`, `repeat_count`, `stop`
* Modes: `"single"`, `"restart"` only

**Out (defer)**: per‑rule FSM generation, timer wheel, ring buffer, schema validation, spatial zones, XR input, parallel/queue modes. These are in your long‑term plan, just not needed to prove the loop this weekend.&#x20;

---

## 1) Target Runtime & Dependencies (MIT‑friendly)

* **Unity**: 2022.3 LTS (Mono or IL2CPP both fine for desktop)
* **UniTask** (MIT): `com.cysharp.unitask` — async scheduling, timers, cancellation, async enumerables
* **JSON**: `com.unity.nuget.newtonsoft-json` (MIT). *Use this only*; **do not add Newtonsoft.Json.Schema** (commercial).
* **Optional** (nice‑to‑have, still MIT):

  * **VContainer** (light DI) if you want explicit composition, otherwise skip
  * **NAudio** (Windows) if you want pure‑code tones; otherwise use Unity `AudioSource.PlayOneShot`

**No** Meta/Oculus, no XR Toolkit. Inputs = mouse/keyboard. Outputs = `Debug.Log`, on‑screen toast, and simple audio cue.&#x20;

---

## 2) Project Structure (simple, testable)

```
Assets/
  Rules/                 // TextAsset JSON or live paste
  Scripts/
    Authoring/
      RuleDto.cs         // DTOs (discriminated unions via "type")
      RuleCoercion.cs    // gentle fixes: wrap scalars, parse numbers, durations
      RuleRepository.cs  // Add/Replace/Enumerate rules
    Engine/
      EventBus.cs        // UniTask AsyncQueue/event stream
      RuleEngine.cs      // dispatcher + per-rule instance manager
      Evaluators/
        ConditionEval.cs // state_equals, numeric_compare, expression, trigger_fired
        ExpressionEval.cs // tiny Pratt/token eval (==, !=, <, <=, >, >=, &&, ||, !)
      Actions/
        ActionRunner.cs  // service_call, wait_duration, branch, repeat_count, stop
      Time/
        TimerService.cs  // UniTask.Delay wrappers + cancellation
      State/
        EntityStore.cs   // numeric/string stores; lastTriggerId
    Integration/
      DesktopInput.cs    // mouse/keyboard → events
      Services.cs        // audio.play, debug.log, ui.toast
      DevConsole.cs      // overlay: last events/actions, paste JSON, “Fire Event”
```

Keep everything `internal` except one `RulesRuntime` façade with `LoadJson(string)`, `Emit(string name, Dictionary<string,object> data = null)`.

---

## 3) Authoring Model (lean, LLM‑friendly)

**Guiding patterns to keep**: explicit `type`, arrays‑always, string enums, units/ranges in field names, consistent field order.&#x20;

**Rule envelope (v0.1)**
Fields shown in the **canonical order** LLMs will copy:

```json
{
  "schema_version": "0.1.0",
  "id": "rule_id",
  "description": "optional",
  "mode": "single|restart",
  "triggers": [ /* ... */ ],
  "conditions": [ /* ... */ ],
  "actions": [ /* ... */ ]
}
```

### Triggers

* `event`: `{ "type": "event", "name": "mouse.left.down", "filter": { /* optional */ } }`
* `numeric_threshold`: `{ "type": "numeric_threshold", "entity": ["sensor.mouse_speed"], "above": 400, "for_ms_0_to_60000": 200 }`
* `pattern`: double‑press, A→D within window:
  `{"type":"pattern","within_ms_10_to_5000":300,"sequence":[{"event":"key.A"},{"event":"key.D"}]}`
* `time_schedule`: `{"type":"time_schedule","every_ms_10_to_600000": 2000}`

### Conditions

* `state_equals`: `{ "type":"state_equals","entity":["ui.mode"],"equals":["debug"] }`
* `numeric_compare`: `{ "type":"numeric_compare","entity":["sensor.fps"],"above":50 }`
* `expression` (subset): identifiers, ints/floats, `== != < <= > >= && || !`, string literals in single quotes
  `{ "type":"expression","expr":"sensor.mouse_speed > 600 && ui.mode == 'debug'" }`
* `trigger_fired`: `{ "type":"trigger_fired","trigger_id":["hotkey_toggle"] }`

### Actions

* `service_call`: `{ "type":"service_call","service":"audio.play","data":{"clip":"beep","volume_0_to_1":0.7} }`
* `wait_duration`: `{ "type":"wait_duration","duration_ms_0_to_60000": 500 }`
* `branch`:
  `{ "type":"branch","if":{...},"then":[...],"else":[...] }`
* `repeat_count`: `{ "type":"repeat_count","count_1_to_20":3,"actions":[...]}`
* `stop`: `{ "type":"stop","reason": "completed" }`

**Coercions (minimal validation)**

* Wrap scalars to arrays (e.g., `"entity":"x"` → `["x"]`)
* `"100"` → `100`; `"0.7"` → `0.7`
* Durations accept `"2s"`, `"2000"`, `2000` → normalized to `ms`
* Unknown fields ignored; unknown trigger/action types → soft error `Debug.LogWarning` with path
  These match your LLM‑robustness recommendations without heavy schema validation.&#x20;

---

## 4) Data Flow (end‑to‑end)

1. **LLM output** (or Paste) → `RuleRepository.AddFromJson(json)`

   * Parse → Coerce → Store `RuleDto`
2. **Index** rules by trigger keys at load (e.g., `"event:mouse.left.down"` → rule list).
3. **DesktopInput** emits events via `EventBus.Publish(Event)`.
4. **RuleEngine** consumes events (async loop with UniTask) →

   * Select candidate rules by trigger key
   * Per rule: evaluate conditions → run actions (async) under **mode semantics**
5. **Actions** call **Services** (audio/log/toast) and waits via **TimerService**.

This aligns with your plan’s routing intent but keeps it lean (no compile step).&#x20;

---

## 5) UniTask Patterns (no coroutines)

* **Event stream**: an `AsyncQueue<EngineEvent>` pattern:

  * `Publish()` enqueues; `ConsumeAsync()` awaits dequeue.
  * Backpressure: if queue > N, drop oldest + `Debug.LogWarning` (simple policy).
* **Delays/Timers**: `await UniTask.Delay(durationMs, delayTiming: PlayerLoopTiming.Update, cancellationToken)`
* **Debounce (`for_ms`)**: keep last‑seen timestamp per `(trigger, entity)`; schedule a delay and cancel if condition breaks.
* **Cancellation**: per‑rule `CancellationTokenSource` (one per active instance). `"restart"` mode cancels old instance and starts a new one; `"single"` ignores new while running.
* **Async sequences**: use `IAsyncEnumerable<EngineEvent>` (UniTask) for pattern windows (collect within `within_ms`).
* **Main thread**: All engine work stays on main thread; Services that need it are already there.

---

## 6) Core Components (implementation notes)

### 6.1 EventBus

* **API**: `Publish(EngineEvent e)`, `IUniTaskAsyncEnumerable<EngineEvent> GetStream()`
* **Shape**: `EngineEvent { string Name; Dictionary<string,object> Data; double TimestampMs }`
* **Routing Key**: for `event`: `"event:{e.Name}"`; for schedule: `"time"`; for pattern worker: internal.

### 6.2 RuleRepository

* Holds `List<RuleDto>` and two maps:

  * `eventKey → List<RuleHandle>` (precomputed for triggers of kind `event`)
  * `aux watchers`: numeric thresholds & pattern trackers register callbacks into Engine at load time

Minimal sanity check: ensure `id`, `triggers[]`, `actions[]` exist; else reject with one‑line error. Everything else is best‑effort.

### 6.3 State / Entities

* `EntityStore` with simple tables:

  * `Dictionary<string,double> numeric` (e.g., `"sensor.mouse_speed"`, `"sensor.fps"`)
  * `Dictionary<string,string> state` (e.g., `"ui.mode"`)
* **DesktopInput** updates `sensor.mouse_position`, `sensor.mouse_speed`; a tiny FPS sampler updates `sensor.fps`.

### 6.4 ConditionEval

* `state_equals`: membership test over array
* `numeric_compare`: evaluate `above`/`below` bounds
* `trigger_fired`: check `RuleContext.LastTriggerId`
* `expression`: tiny Pratt parser over the two stores; string literals allowed with single quotes; identifiers resolve first from numeric then state (string comparisons allowed).

### 6.5 TimerService (UniTask)

* `Delay(ms, token)`;
* `Every(ms, token)` → `IAsyncEnumerable<long>` for schedule trigger;
* `Within(ms)` helper for pattern windows.

### 6.6 Pattern Trigger

* For each pattern rule, on first event match start a window task: buffer sequence until matched or time out; at success, *emit a synthetic event* `"pattern:{ruleId}"` and set `trigger_id` for the rule.

### 6.7 RuleEngine & Modes

* **single**: if an instance is running, ignore new fire (log “skipped”).
* **restart**: cancel running instance (via CTS), start a fresh one.
* Instance = async method that:

  1. Captures `triggerId`
  2. Evaluates conditions (fast, sync)
  3. Runs actions sequentially; `wait_duration` uses `TimerService.Delay`
  4. On cancellation, stop immediately.

### 6.8 Services (Desktop)

* `audio.play { clip, volume_0_to_1 }` → `AudioSource.PlayOneShot(clip)`
* `debug.log { message }` → `Debug.Log`
* `ui.toast { text, duration_ms_0_to_10000 }` → overlay `DevConsole` line w/ fade

Service registry is just a `Dictionary<string, Func<Dictionary<string,object>, UniTask>>`. Keep parameter checks minimal (missing keys → default, log warning). This mirrors the manifest idea but stays light.&#x20;

---

## 7) Editor/Dev Tools (fast iteration)

* **Paste JSON Panel** (DevConsole): multiline field + “Load/Replace” button → `RuleRepository.AddFromJson`.
* **Event Fire Panel**: enter `"event.name"` + JSON data; click “Emit”.
* **Overlay**: last 25 events/actions with timestamps; and per‑rule status (Idle/Running/Cancelled).
* **Hot Reload**: If a `TextAsset` is referenced, “Reload” button reparses and replaces rules in place.

---

## 8) Simplified Error Handling (single‑issue messages)

* Keep to short, actionable, one‑fix hints (good for LLM self‑repair):

  * `ERROR actions[0].data.duration_ms_0_to_60000: "80000" > 60000. Try 3000.`
  * `ERROR triggers[0].type: "evnt". Did you mean "event"?`
    This follows your “structured, one fix at a time” guidance without full schema.&#x20;

---

## 9) Minimal Build/Boot Sequence

1. Boot `RulesRuntime` (creates EventBus, TimerService, Services, RuleEngine, DevConsole).
2. Load default example rules (TextAsset) or paste JSON.
3. Start `DesktopInput` (mouse/keyboard → EventBus; update `EntityStore`).
4. Start engine’s Consume loop: `await foreach (var e in EventBus.GetStream()) { Dispatch(e); }`

---

## 10) Curated Example Set (copy‑ready for the weekend)

> These examples use the canonical field order and “arrays‑always” to maximize LLM success.&#x20;

### A) Click → Beep (pure event → service\_call)

```json
{
  "schema_version": "0.1.0",
  "id": "click_beep",
  "description": "Left click plays a short beep",
  "mode": "single",
  "triggers": [{ "type": "event", "name": "mouse.left.down" }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 0.6 } }
  ]
}
```

### B) Hold ≥ 300ms → Toast (debounce/for\_ms)

```json
{
  "schema_version": "0.1.0",
  "id": "hold_to_toast",
  "description": "Left button held for 300ms shows toast",
  "mode": "single",
  "triggers": [{ "type": "numeric_threshold", "entity": ["sensor.mouse_button_left"], "above": 0.5, "for_ms_0_to_60000": 300 }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "ui.toast", "data": { "text": "Held!", "duration_ms_0_to_10000": 1000 } }
  ]
}
```

*(Implement `sensor.mouse_button_left` as 1 while pressed, 0 otherwise.)*

### C) Double‑Click Pattern → Beep + Log

```json
{
  "schema_version": "0.1.0",
  "id": "double_click",
  "description": "Two left clicks within 250ms",
  "mode": "restart",
  "triggers": [{ "type": "pattern", "within_ms_10_to_5000": 250, "sequence": [ { "event": "mouse.left.down" }, { "event": "mouse.left.down" } ] }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 1.0 } },
    { "type": "service_call", "service": "debug.log", "data": { "message": "Double click detected" } }
  ]
}
```

### D) Scheduled Hint (time\_schedule + branch)

```json
{
  "schema_version": "0.1.0",
  "id": "periodic_hint",
  "description": "Every 2s, if UI in debug mode, show hint",
  "mode": "single",
  "triggers": [{ "type": "time_schedule", "every_ms_10_to_600000": 2000 }],
  "conditions": [ { "type": "state_equals", "entity": ["ui.mode"], "equals": ["debug"] } ],
  "actions": [
    { "type": "service_call", "service": "ui.toast", "data": { "text": "Tip: Try a double click", "duration_ms_0_to_10000": 1000 } }
  ]
}
```

### E) Mouse Speed Gate (numeric + expression + wait)

```json
{
  "schema_version": "0.1.0",
  "id": "speed_gate",
  "description": "If mouse speed spikes, beep thrice unless debug off",
  "mode": "restart",
  "triggers": [{ "type": "numeric_threshold", "entity": ["sensor.mouse_speed"], "above": 600, "for_ms_0_to_60000": 100 }],
  "conditions": [ { "type": "expression", "expr": "ui.mode != 'off'" } ],
  "actions": [
    { "type": "repeat_count", "count_1_to_20": 3, "actions": [
      { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 0.8 } },
      { "type": "wait_duration", "duration_ms_0_to_60000": 150 }
    ]}
  ]
}
```

---

## 11) Test Plan (90 minutes)

1. **Boot sanity**: Load A) and click → hear beep/log.
2. **Debounce**: Load B) and hold <300ms (no toast) vs ≥300ms (toast).
3. **Pattern**: Load C), confirm spacing sensitivity.
4. **Schedule**: Load D), toggle `ui.mode` via hotkey (`F1` sets `ui.mode="debug"`) to observe gated toast.
5. **Numeric**: Load E), move mouse quickly to exceed speed threshold; see 3 beeps with waits.

---

## 12) Future‑proofing Notes (kept in mind, not implemented now)

* The current dispatcher and service registry are structured so you can later **swap in** ring buffer, timer wheel, and per‑rule FSMs without changing JSON/LLM contracts.&#x20;
* Keep the **canonical examples** small and consistently ordered; they are your highest ROI for first‑try LLM validity.&#x20;

---

## 13) Quick TODO Checklist

* [ ] Add UniTask via UPM; add Newtonsoft (MIT) via UPM
* [ ] Implement `RulesRuntime` façade + `RuleRepository` (parse/coerce/index)
* [ ] Implement `EventBus` (async queue)
* [ ] Implement `EntityStore` + DesktopInput (mouse, keys, fps, speed)
* [ ] Implement `ConditionEval` + `ExpressionEval` (subset)
* [ ] Implement `TimerService` (UniTask.Delay/Every)
* [ ] Implement `ActionRunner` + Services (audio/log/toast/log)
* [ ] DevConsole overlay with paste panel, event fire, history
* [ ] Load curated examples; verify 5 test scenarios

---

### Why this fits your goals

* **Fast to wire up** (a few focused classes) yet **faithful** to the authoring style you want to test (explicit, arrays‑always, units, enums).&#x20;
* **Async correctness** without coroutines using **UniTask** patterns (delays, cancellation, async streams).
* **No XR/Quest** dependencies; desktop inputs + audio/logging still exercise the full E2E path.
* **Minimal validation** keeps momentum while still applying safe coercions so LLM output usually “just works.”&#x20;

If you want, I can draft the **interfaces/signatures** (very short) for `RulesRuntime`, `EventBus`, `TimerService`, and `ActionRunner` to lock the seams before you start wiring.
