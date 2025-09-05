Below is a single, consolidated **plan\_simple\_final** that distills the candidate plans into the *simplest possible* weekend‑achievable project to validate the core premise: **LLM JSON → local rule → triggers → conditions → actions → output** on **Unity 6.2 (desktop)** using **UniTask** and **MIT‑licensed JSON** only. Where choices differ from the candidates, I note the rationale briefly.

---

# plan\_simple\_final — Weekend MVP (Unity 6.2)

## 0) What we’re building (and why)

**Goal:** Prove the end‑to‑end loop with the fewest moving parts:

* **Input:** desktop mouse/keyboard + a timer
* **Authoring:** minimal, LLM‑friendly JSON (no manifests, no schema version, permissive coercions)
* **Engine:** direct interpretation (no FSM compile, no timer wheel, no ring buffer)
* **Async:** **UniTask** (no coroutines, no Rx/R3 by default)
* **Outputs:** `Debug.Log` + simple audio “beep” (+ optional on‑screen toast)

This uses the smallest slice from the MVP candidate while keeping the most LLM‑reliable schema conventions (explicit `type`, arrays‑always, units in field names, string enums). &#x20;

**Why not the heavier plans?** The Quest/VR plan’s compilation, FSMs, timer wheel, ring buffer, manifests, and validation are intentionally **deferred**; we only need a desktop proof this weekend.&#x20;

---

## 1) Scope (small, but complete)

**Triggers (3):**

1. `event` (e.g., `"mouse.left.down"`)
2. `numeric_threshold` (`above` *or* `below`, optional `for_ms`)
3. `time_schedule` (`every_ms_10_to_600000`)

**Conditions (2):**

* `state_equals` (string equality)
* `numeric_compare` (`above` or `below` on an entity)

**Actions (4):**

* `service_call` (supports services: `audio.play`, `debug.log`, `ui.toast`)
* `wait_duration` (ms)
* `repeat_count` (N times: run a short action list)
* `stop` (end the rule instance)

**Modes (1 + 1 optional):**

* default `"single"`; optional `"restart"` (cancel prior instance then start a new one)

This scope matches the lean MVP candidate and avoids patterns/parallel/validation complexity while preserving expressive power for the demo.&#x20;

---

## 2) What we’re **not** building this weekend

* No schema/manifest validation; no AST compile; no FSMs; no timer wheel; no ring buffer; no VR/XR libs; no parallel/queue modes; no complex expressions; no hot‑reload daemon (manual reload button only). These live in the long‑term plan, but skipping them doesn’t reduce demo fidelity.&#x20;

---

## 3) Dependencies (MIT‑only, Unity 6.2)

* **UniTask** (`com.cysharp.unitask`) — delays, cancellation, async streams
* **JSON**: `com.unity.nuget.newtonsoft-json` (MIT) — straightforward, reliable parsing
* **Optional**: TextMeshPro for a simple debug overlay (or use a tiny IMGUI panel)

Keep it to these two for the weekend; no Rx/R3 by default. (If your team is Rx‑fluent, you *may* swap UniTask timers/debounces for R3 operators later; it’s optional and not needed to ship this MVP.) &#x20;

---

## 4) Authoring model (LLM‑friendly, minimal ceremony)

* **No `schema_version`** (assume v0 for the hack)
* `id` **optional** (auto‑assign `rule_<n>` if missing)
* `mode` optional (default `"single"`)
* **Arrays‑always** for multi‑value positions (engine uses index 0, logs if extras)
* **Units & ranges in field names** (`every_ms_10_to_600000`, `volume_0_to_1`, `duration_ms_0_to_60000`)
* **String enums**, not booleans (e.g., `"mode": "single|restart"`)
* **Coercions only**: wrap scalars→arrays; `"100"`→100; `"2s"`→2000ms; unknown fields ignored, unknown types warn and skip

These are the highest‑ROI patterns for first‑try LLM validity while keeping the engine tiny.&#x20;

**Canonical rule envelope (field order is intentional for LLMs):**&#x20;

```json
{
  "id": "optional_rule_id",
  "mode": "single|restart",
  "triggers": [ ... ],
  "conditions": [ ... ],
  "actions": [ ... ]
}
```

---

## 5) Architecture (small, testable seams)

```
Scripts/
  Runtime/
    Authoring/
      RuleDto.cs          // minimal DTOs (discriminated unions by "type")
      RuleCoercion.cs     // wrap scalars, parse numbers/durations
      RuleRepository.cs   // add/replace/list rules + build trigger index
    Engine/
      EventBus.cs         // AsyncQueue + Publish/GetStream
      RuleEngine.cs       // dispatch + modes ("single"|"restart")
      ConditionEval.cs    // state_equals, numeric_compare
      ActionRunner.cs     // service_call, wait_duration, repeat_count, stop
      TimerService.cs     // UniTask.Delay + Every(ms)
      EntityStore.cs      // numeric + string dictionaries
    Desktop/
      DesktopInput.cs     // mouse/keys -> events; updates EntityStore (e.g., mouse_speed)
      Services.cs         // audio.play, debug.log, ui.toast
      DevPanel.cs         // paste JSON, reload, fire event, view logs
```

This mirrors the clean seams from the leaner candidate plan (uni‑task event queue, simple repository/index, tiny services) without the heavier compile/runtime scaffolding. &#x20;

**Data flow**
LLM JSON (paste/TextAsset) → Parse+Coerce → Index rules by trigger key → `DesktopInput` emits → `RuleEngine` selects candidates → check conditions → run actions (UniTask delays) → audio/log/toast.&#x20;

---

## 6) Runtime details (concrete, but small)

**EventBus**

* Single producer API: `Publish(EngineEvent e)`
* Single consumer loop: `await foreach (var e in GetStream(ct)) { Dispatch(e); }`
* Overflow policy: if queue > N, drop oldest + warn (keep code simple).&#x20;

**EntityStore**

* `Dictionary<string,double>` numeric (e.g., `"sensor.mouse_speed"`)
* `Dictionary<string,string>` state (e.g., `"ui.mode"`)
* `DesktopInput` updates: `mouse_speed`, `mouse_button_left` (0/1), and toggles `ui.mode="debug"` on `F1`.&#x20;

**Triggers**

* `event`: route by `"event:<name>"`
* `numeric_threshold`: poll in a lightweight UniTask loop (e.g., every 20–33ms), track stable‑for `for_ms` with a cancellation token to debounce
* `time_schedule`: `Every(ms)` using UniTask async enumerable (main PlayerLoop)&#x20;

**Modes**

* `"single"`: ignore new while running (log “skipped”)
* `"restart"`: cancel current CTS, start fresh instance&#x20;

**Actions**

* `service_call`: dispatch table `{ string => Func<Dictionary<string,object>, UniTask> }` with three handlers
* `wait_duration`: `await UniTask.Delay(ms, cancellationToken: ruleCts)`
* `repeat_count`: simple `for` loop over child actions
* `stop`: end instance immediately&#x20;

**Services (desktop)**

* `audio.play { clip, volume_0_to_1 }` → `AudioSource.PlayOneShot(clip, volume)` (default clip: “beep”, default volume: 0.7)
* `debug.log { message }` → `Debug.Log(message)`
* `ui.toast { text, duration_ms_0_to_10000 }` → draw in `DevPanel` with fade‑out
  (Keep parameter checks minimal; missing fields use defaults + warning.)&#x20;

---

## 7) Minimal schema (weekend subset)

> Keep arrays‑always and units/ranges in field names; engine uses first array element and logs extras.&#x20;

**Triggers**

* **event**

  ```json
  { "type": "event", "name": "mouse.left.down" }
  ```
* **numeric\_threshold** (choose **one** of `above` or `below`; optional `for_ms_0_to_60000`)

  ```json
  { "type": "numeric_threshold",
    "entity": ["sensor.mouse_speed"],
    "above": 600,
    "for_ms_0_to_60000": 100 }
  ```
* **time\_schedule**

  ```json
  { "type": "time_schedule", "every_ms_10_to_600000": 2000 }
  ```

**Conditions**

* **state\_equals**

  ```json
  { "type": "state_equals", "entity": ["ui.mode"], "equals": ["debug"] }
  ```
* **numeric\_compare**

  ```json
  { "type": "numeric_compare", "entity": ["sensor.mouse_speed"], "above": 300 }
  ```

**Actions**

* **service\_call**

  ```json
  { "type": "service_call", "service": "audio.play",
    "data": { "clip": "beep", "volume_0_to_1": 0.6 } }
  ```
* **wait\_duration**

  ```json
  { "type": "wait_duration", "duration_ms_0_to_60000": 150 }
  ```
* **repeat\_count**

  ```json
  { "type": "repeat_count", "count_1_to_20": 3, "actions": [ ... ] }
  ```
* **stop**

  ```json
  { "type": "stop", "reason": "completed" }
  ```

---

## 8) Curated examples (copy‑ready for the demo)

> Field order is canonical on purpose; these mirror the minimalist MVP candidate while using the schema optimizations for LLM reliability. &#x20;

**A) Click → Beep**

```json
{
  "id": "click_beep",
  "triggers": [{ "type": "event", "name": "mouse.left.down" }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "audio.play",
      "data": { "clip": "beep", "volume_0_to_1": 0.6 } }
  ]
}
```

**B) Hold‑style threshold → Toast**

```json
{
  "id": "hold_to_toast",
  "mode": "single",
  "triggers": [{
    "type": "numeric_threshold",
    "entity": ["sensor.mouse_button_left"],
    "above": 0.5,
    "for_ms_0_to_60000": 300
  }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "ui.toast",
      "data": { "text": "Held!", "duration_ms_0_to_10000": 1000 } }
  ]
}
```

**C) Scheduled hint gated by state**

```json
{
  "id": "periodic_hint",
  "triggers": [{ "type": "time_schedule", "every_ms_10_to_600000": 2000 }],
  "conditions": [{ "type": "state_equals", "entity": ["ui.mode"], "equals": ["debug"] }],
  "actions": [
    { "type": "service_call", "service": "debug.log", "data": { "message": "Debug hint tick" } }
  ]
}
```

**D) Speed spike → 3 short beeps (restart)**

```json
{
  "id": "speed_gate",
  "mode": "restart",
  "triggers": [{
    "type": "numeric_threshold",
    "entity": ["sensor.mouse_speed"],
    "above": 600,
    "for_ms_0_to_60000": 100
  }],
  "conditions": [],
  "actions": [
    { "type": "repeat_count", "count_1_to_20": 3, "actions": [
      { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 0.8 } },
      { "type": "wait_duration", "duration_ms_0_to_60000": 150 }
    ]}
  ]
}
```

---

## 9) Editor/dev affordances (just enough)

* **Paste JSON & Reload**: single DevPanel with a multiline field + “Replace Rules” button; also a dropdown to load one of the curated examples.
* **Emit Event**: input box for event name + optional JSON data → `Publish()`.
* **Overlay feed**: last N events/actions w/ timestamps; minimal per‑rule status (Idle/Running).
  (Hot‑reload file watcher is deferred; one “Reload” button is enough this weekend.)&#x20;

---

## 10) Implementation order (one day each)

**Day 1**

1. **Project setup**: install UniTask + Newtonsoft (MIT).
2. **Data + Loader**: `RuleDto`, `RuleCoercion`, `RuleRepository` (parse + coerce + index).
3. **Core runtime**: `EventBus` (AsyncQueue) + `RuleEngine` (dispatch + modes).
4. **Desktop input + entities**: mouse button/speed, `ui.mode` hotkey; simple timer ticks.
5. **Actions + services**: `audio.play`, `debug.log`, `ui.toast`; `wait_duration`, `repeat_count`, `stop`.
   (You now have A/B working.) &#x20;

**Day 2**

1. **Conditions**: `state_equals`, `numeric_compare`.
2. **time\_schedule** trigger (`Every(ms)`).
3. **DevPanel**: paste JSON, reload, emit event, overlay logs.
4. **Polish**: single‑issue error messages (coercion failures, unknown type), defaults, volume/clip fallbacks.
5. **Demo scenarios**: wire A–D; confirm `"mode": "restart"` works; sanity on “held” debounce. &#x20;

---

## 11) Success criteria (weekend)

* Paste JSON → rules load with no schema step; unknowns warn, engine continues.
* Click plays a sound; held input produces a toast; scheduled hint respects `ui.mode`.
* Speed threshold fires 3 beeps with waits; `"restart"` cancels overlapping runs.
* All of the above run on desktop in Unity 6.2, no XR packages.
  This matches the MVP candidate’s intent while adopting the schema practices that raise LLM success. &#x20;

---

## 12) Notes on R3 (decision)

* **Default: don’t add R3** this weekend. UniTask already covers delay/debounce/schedules with fewer concepts and less wiring.
* If a teammate is Rx‑fluent and wants *pattern* or *debounce* operators, you can wrap R3 behind helpers later; not needed to hit the demo goals. The v4.0 candidate that leans on Rx has more moving parts than we need right now. &#x20;

---

## 13) What this plan carries forward

* Keeps JSON shapes and conventions that scale to the bigger architecture (explicit types, arrays‑always, units/ranges in field names, string enums), so swapping in FSMs, manifests, a timer wheel, or XR I/O later won’t change authoring. We’re just proving the loop now, intentionally deferring the heavyweight runtime from the Quest plan. &#x20;

---

### Summary of candidate analysis that drove this plan

* **MVP plan** (tiny: 3 triggers / 2 conditions / 4 actions) is the right *base* for speed. We adopt its spirit and tighten schema naming to be more LLM‑robust.&#x20;
* **Lean UniTask plan** adds helpful seams (service registry, DevPanel, arrays‑always, units) without heavy runtime; we keep those.&#x20;
* **VR plan** is over‑featured for the weekend (FSMs/timer wheel/ring buffer/manifests). We explicitly defer those.&#x20;
* **LLM schema optimizations** (explicit type discrimination, arrays‑always, units in names, consistent field order, string enums) are retained because they increase first‑try validity without adding runtime burden.&#x20;

---

If you want, I can convert this into a one‑page checklist for task assignment (owner + ETA per item) so the team can parallelize immediately.
