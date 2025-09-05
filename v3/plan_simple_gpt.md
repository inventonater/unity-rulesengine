# plan_simple_final.md — UPM Weekend MVP (Unity 6.2) with JSON `pattern_sequence`, aliases, and extra samples

This is the **final weekend plan** for a **Unity Package (UPM)** that lives under a host project's `Packages/` folder. It merges the lean MVP, UPM packaging, the **JSON-expressed `pattern_sequence` trigger** (for Konami/Double‑Click), plus **compatibility aliases** and a tiny **`state.set`** service—while staying aligned with the longer‑term Quest plan and LLM schema guidance. fileciteturn0file0 fileciteturn0file3 fileciteturn0file5 fileciteturn0file6

---

## 0) TL;DR (what we ship this weekend)

- **UPM package** at `Packages/com.inventonater.rules/` with Samples. fileciteturn0file0  
- **Triggers (4):** `event`, `numeric_threshold`, `time_schedule`, **`pattern_sequence`** (ordered names within a time window). fileciteturn0file3  
- **Conditions (2):** `state_equals`, `numeric_compare`. fileciteturn0file4  
- **Actions (4):** `service_call`, `wait_duration`, `repeat_count`, `stop` + tiny **`service_call: "state.set"`** convenience. fileciteturn0file4  
- **LLM‑friendly schema**: explicit `type`, arrays‑always, units/ranges in names, string enums, scalar→array & string→number coercions. fileciteturn0file6  
- **Compat aliases (loader‑only):** accept `timer/value/pattern` as input and normalize to our canonical `time_schedule/numeric_threshold/pattern_sequence`. (Docs show only canonical names.) fileciteturn0file1 fileciteturn0file2

**Definition of Done:** Paste/import JSON → click/hold/schedule **and** JSON‑driven **double‑click + Konami** work; logs/toasts/beeps show behavior; desktop‑only; code footprint remains small. fileciteturn0file0

---

## 1) UPM package layout (drop in `Packages/`)

```
Packages/
  com.inventonater.rules/
    package.json
    README.md
    CHANGELOG.md
    LICENSE.md
    Runtime/
      RulesEngine.Runtime.asmdef
      Authoring/
        RuleDto.cs
        RuleCoercion.cs            // NEW (alias normalization)
        RuleRepository.cs
      Engine/
        EventBus.cs
        RuleEngine.cs
        ConditionEval.cs
        ActionRunner.cs
        TimerService.cs
        EntityStore.cs
        PatternSequenceWatcher.cs  // NEW
      Desktop/
        DesktopInput.cs            // + mouse.left.up, key.space.down
        Services.cs                // + service_call: "state.set"
        DevPanel.cs
        Resources/beep.wav
    Editor/
      RulesEngine.Editor.asmdef    // (optional)
    Samples~/
      Demo/
        Demo.unity
        Rules/click_beep.json
        Rules/hold_to_toast.json
        Rules/periodic_hint.json
        Rules/speed_gate.json
        Rules/double_click_pattern.json
        Rules/konami_pattern.json
        Rules/heartbeat_log.json           // NEW
        Rules/space_combo_triple_beep.json // NEW
```
Keeps the lean MVP structure, adds a tiny watcher for sequences and a loader for aliases inspired by the other candidate plans. fileciteturn0file0 fileciteturn0file1 fileciteturn0file2

---

## 2) `package.json` (Unity 6.2 host)

```json
{
  "name": "com.inventonater.rules",
  "displayName": "Inventonater Rules (LLM JSON → Unity)",
  "version": "0.1.0",
  "unity": "6000.0.0",
  "description": "Weekend MVP rules engine: LLM JSON → triggers/conditions/actions → services (audio/log/ui). Desktop now, Quest-ready later.",
  "author": { "name": "Inventonater" },
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.cysharp.unitask": "2.5.0"
  },
  "samples": [
    { "displayName": "Demo Scene & Rules", "description": "Click, hold, schedule, double-click, Konami (JSON sequence) + extras", "path": "Samples~/Demo" }
  ]
}
```
Minimal, MIT‑friendly deps; mirrors the weekend scope. fileciteturn0file0

**Assembly Definitions** (unchanged): runtime/editor split, auto‑referenced runtime. fileciteturn0file0

---

## 3) Scope (small, complete) and authoring model

### Triggers (4)
- **event** — exact event name (e.g., `mouse.left.down`). fileciteturn0file4  
- **numeric_threshold** — `above` / `below` on an entity, optional `for_ms`. fileciteturn0file4  
- **time_schedule** — `every_ms_10_to_600000`. fileciteturn0file0  
- **pattern_sequence** — ordered list of event `name`s within `within_ms_10_to_5000`. (Exact match only; no alternation/filters this weekend.) fileciteturn0file3

### Conditions (2)
- **state_equals**, **numeric_compare**. fileciteturn0file4

### Actions (4)
- **service_call**, **wait_duration**, **repeat_count**, **stop**. Plus **`service_call: "state.set"`** to set `EntityStore` (numeric or string) without adding a new action type. fileciteturn0file1

### Authoring conventions (LLM‑friendly)
Explicit `type`, **arrays‑always**, **units/ranges** in names, string enums; unknown fields ignored with a warning; scalar→array and string→number (`"2s"`→`2000`) coercions. Field order: `id, mode, triggers, conditions, actions`. fileciteturn0file6

**Compat aliases accepted by the loader (input only):**  
- `{"type":"timer","every_ms":N}` → `{"type":"time_schedule","every_ms_10_to_600000":N}`. fileciteturn0file2  
- `{"type":"pattern","within_ms":N,"sequence":[{"event":"..."}]}` → `{"type":"pattern_sequence","within_ms_10_to_5000":N,"sequence":[{"name":"..."}]}`. fileciteturn0file1  
- `{"type":"value","path":"foo","above":X}` → `{"type":"numeric_threshold","entity":["foo"],"above":X}` (or `below`). fileciteturn0file1

We keep **one canonical schema** outwardly to match the long‑term Quest plan’s “one way” rule authoring. fileciteturn0file5

---

## 4) Short code samples (only NEW/changed bits)

> The rest (EventBus, ConditionEval, ActionRunner, TimerService, DevPanel) matches the earlier MVP snippets—kept tiny. fileciteturn0file0

### 4.1 `PatternSequenceWatcher.cs` (NEW)
```csharp
public sealed class PatternSequenceWatcher {
  readonly string[] seq; readonly float within;
  int i = 0; float windowStart = -1f;
  public PatternSequenceWatcher(IEnumerable<string> names, int withinMs){
    seq = System.Linq.Enumerable.ToArray(names); within = withinMs / 1000f;
  }
  // return true when sequence completes
  public bool OnEvent(string name, float now){
    if (i == 0) { if (name == seq[0]) { i = 1; windowStart = now; } return false; }
    if (now - windowStart > within) { i = 0; windowStart = -1f; if (name == seq[0]) { i = 1; windowStart = now; } return false; }
    if (name == seq[i]) { if (++i >= seq.Length) { i = 0; windowStart = -1f; return true; } return false; }
    i = (name == seq[0]) ? 1 : 0; if (i == 1 && windowStart < 0) windowStart = now; return false;
  }
}
```

### 4.2 `RuleDto.cs` (add fields for pattern + alias inputs)
```csharp
public class TriggerDto {
  public string type; public string name;
  public System.Collections.Generic.List<string> entity;
  public double above, below; public int for_ms_0_to_60000;
  public int every_ms_10_to_600000;
  // pattern_sequence
  public int within_ms_10_to_5000;
  public System.Collections.Generic.List<PatternStep> sequence;
  // aliases (loader only)
  public int every_ms; public int within_ms; public string path;
}
public class PatternStep { public string name; public string @event; } // "@event" alias accepted
```

### 4.3 `RuleCoercion.cs` (normalize aliases → canonical)
```csharp
public static class RuleCoercion {
  public static void CoerceTrigger(TriggerDto t) {
    if (t.type == "timer") { t.type = "time_schedule"; if (t.every_ms_10_to_600000 == 0) t.every_ms_10_to_600000 = t.every_ms; }
    if (t.type == "pattern") {
      t.type = "pattern_sequence";
      if (t.within_ms_10_to_5000 == 0) t.within_ms_10_to_5000 = t.within_ms;
      if (t.sequence != null) foreach (var s in t.sequence) if (string.IsNullOrEmpty(s.name) && !string.IsNullOrEmpty(s.@event)) s.name = s.@event;
    }
    if (t.type == "value" && !string.IsNullOrEmpty(t.path)) { t.type = "numeric_threshold"; t.entity = new() { t.path }; }
  }
}
```
Alias handling learned from the candidate plans; outward docs remain canonical. fileciteturn0file1 fileciteturn0file2

### 4.4 `RuleRepository.cs` (track pattern rules)
```csharp
readonly System.Collections.Generic.List<RuleDto> _patternRules = new();
public void ReplaceAll(IEnumerable<RuleDto> rules){
  _byTrigger.Clear(); _patternRules.Clear();
  foreach (var r in rules) foreach (var t in r.triggers){
    if (t.type == "pattern_sequence") { _patternRules.Add(r); continue; }
    var key = t.type switch {
      "event" => $"event:{t.name}",
      "numeric_threshold" => $"num:{(t.above!=0?"above":"below")}:{t.entity?[0]}",
      "time_schedule" => $"time:{t.every_ms_10_to_600000}",
      _ => null
    };
    if (key==null) continue;
    if (!_byTrigger.TryGetValue(key, out var list)) _byTrigger[key]=list=new();
    list.Add(r);
  }
}
public System.Collections.Generic.IEnumerable<RuleDto> GetPatternRules() => _patternRules;
```

### 4.5 `RuleEngine.cs` (wire pattern watchers)
```csharp
System.Collections.Generic.List<(RuleDto rule, PatternSequenceWatcher watcher)> _pattern;
public void Initialize(IRuleRepository repo, EntityStore store, Services services){
  _repo=repo; _store=store; _services=services; _runner=new ActionRunner(_services);
  _pattern = new();
  foreach (var r in _repo.GetPatternRules())
    foreach (var t in r.triggers) if (t.type=="pattern_sequence")
      _pattern.Add((r, new PatternSequenceWatcher(t.sequence?.ConvertAll(s=>s.name) ?? new(), 
        UnityEngine.Mathf.Clamp(t.within_ms_10_to_5000, 10, 5000))));
  _loopCts = new System.Threading.CancellationTokenSource();
  Run(_loopCts.Token).Forget();
}
async Cysharp.Threading.Tasks.UniTask Run(System.Threading.CancellationToken ct){
  await foreach (var e in EventBus.GetStream(ct)){
    foreach (var r in _repo.GetCandidatesFor($"event:{e.Name}")) TryStartRule(r, ct);
    foreach (var (r, w) in _pattern) if (w.OnEvent(e.Name, e.Timestamp)) TryStartRule(r, ct);
  }
}
```

### 4.6 `DesktopInput.cs` (add canonical key/mouse events)
```csharp
void Update(){
  // existing mouse.left.down and speed...
  if (UnityEngine.Input.GetMouseButtonUp(0)) EventBus.Publish("mouse.left.up");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space)) EventBus.Publish("key.space.down");
  // Konami keys (names align with samples)
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow))    EventBus.Publish("key.arrow_up.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow))  EventBus.Publish("key.arrow_down.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow))  EventBus.Publish("key.arrow_left.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) EventBus.Publish("key.arrow_right.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.A)) EventBus.Publish("key.a.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.B)) EventBus.Publish("key.b.down");
}
```

### 4.7 `Services.cs` (add `state.set`)
```csharp
case "state.set":
  var key = data.TryGetValue("key", out var k) ? k?.ToString() : null;
  var val = data.TryGetValue("value", out var v) ? v?.ToString() : null;
  if (!string.IsNullOrEmpty(key) && val != null) {
    var store = FindObjectOfType<EntityStore>();
    if (double.TryParse(val, out var n)) store.SetNumeric(key, n);
    else store.SetState(key, val);
  } else Debug.LogWarning("state.set requires {key, value}");
  break;
```
A tiny convenience so rules can toggle flags/set constants without introducing a new action type. fileciteturn0file1

---

## 5) Curated demo rules (JSON you can drop in)

> **Arrays‑always**, explicit `type`, units/ranges in names; field order: `id, mode, triggers, conditions, actions`. fileciteturn0file6

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

**C) Scheduled hint (state‑gated)**
```json
{
  "id": "periodic_hint",
  "triggers": [{ "type": "time_schedule", "every_ms_10_to_600000": 2000 }],
  "conditions": [{ "type": "state_equals", "entity": ["ui.mode"], "equals": ["debug"] }],
  "actions": [
    { "type": "service_call", "service": "debug.log",
      "data": { "message": "Debug hint tick" } }
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

**E) Double‑click via `pattern_sequence`**
```json
{
  "id": "double_click_pattern",
  "mode": "single",
  "triggers": [{
    "type": "pattern_sequence",
    "within_ms_10_to_5000": 250,
    "sequence": [
      { "name": "mouse.left.down" },
      { "name": "mouse.left.down" }
    ]
  }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "audio.play",
      "data": { "clip": "beep", "volume_0_to_1": 1.0 } }
  ]
}
```

**F) Konami Code via `pattern_sequence`**
```json
{
  "id": "konami_pattern",
  "mode": "single",
  "triggers": [{
    "type": "pattern_sequence",
    "within_ms_10_to_5000": 1500,
    "sequence": [
      { "name": "key.arrow_up.down" },
      { "name": "key.arrow_up.down" },
      { "name": "key.arrow_down.down" },
      { "name": "key.arrow_down.down" },
      { "name": "key.arrow_left.down" },
      { "name": "key.arrow_right.down" },
      { "name": "key.arrow_left.down" },
      { "name": "key.arrow_right.down" },
      { "name": "key.b.down" },
      { "name": "key.a.down" }
    ]
  }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "ui.toast",
      "data": { "text": "KONAMI!", "duration_ms_0_to_10000": 1200 } },
    { "type": "repeat_count", "count_1_to_20": 3, "actions": [
      { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 0.9 } },
      { "type": "wait_duration", "duration_ms_0_to_60000": 120 }
    ]}
  ]
}
```
These JSON sequences provide a realistic stress test for ordered, time‑bounded triggers within a tiny runtime. fileciteturn0file3

**G) Heartbeat (time_schedule → log)** — extended sample
```json
{
  "id": "heartbeat_log",
  "triggers": [{ "type": "time_schedule", "every_ms_10_to_600000": 2000 }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "debug.log", "data": { "message": "Heartbeat tick" } }
  ]
}
```

**H) Space‑combo (pattern → triple beep)** — extended sample
```json
{
  "id": "space_combo_triple_beep",
  "triggers": [{
    "type": "pattern_sequence",
    "within_ms_10_to_5000": 300,
    "sequence": [{ "name": "key.space.down" }, { "name": "key.space.down" }]
  }],
  "conditions": [],
  "actions": [
    { "type": "repeat_count", "count_1_to_20": 3, "actions": [
      { "type": "service_call", "service": "audio.play", "data": { "clip": "beep", "volume_0_to_1": 0.8 } },
      { "type": "wait_duration", "duration_ms_0_to_60000": 120 }
    ]}
  ]
}
```

---

## 6) Using the package in a host project

1) Create/clone a Unity **6.2** project.  
2) Put this folder into the project: `Packages/com.inventonater.rules/`.  
3) Open the **Demo** sample (Package Manager → Inventonater Rules → Samples → **Import**).  
4) Hit **Play**. Try: click, **double‑click**, hold LMB, press **F1**, press **Space** twice, or enter **Konami**. fileciteturn0file0

---

## 7) What we learned & adopted from the other candidate plans (compat notes)

- **Aliases in the loader** increase first‑try LLM success without expanding the public schema. We accept `timer/value/pattern` as inputs and normalize to canonical names during parse. fileciteturn0file1 fileciteturn0file2  
- **A couple more input events** (`mouse.left.up`, `key.space.down`) cover their common examples without new trigger types. fileciteturn0file1 fileciteturn0file2  
- **Minimal state mutation** via `service_call: "state.set"` mirrors the “set” examples in a tiny, non‑intrusive way. fileciteturn0file1

These changes are cheap now and keep **one canonical schema** outwardly, which aligns with the Quest plan’s design discipline. fileciteturn0file5

---

## 8) Implementation plan (by hour blocks)

**Day 1**  
1) Install **Newtonsoft.Json** + **UniTask**; create UPM package structure. fileciteturn0file0  
2) Data & loader: `RuleDto`, **`RuleCoercion`** (aliases), `RuleRepository`. fileciteturn0file2  
3) Runtime: `EventBus` + `RuleEngine` (dispatch + `"single"/"restart"`). fileciteturn0file4  
4) Desktop input & entities: mouse/keys + `ui.mode` toggle; mouse speed/button entities. fileciteturn0file4  
5) Actions & services: `audio.play`, `debug.log`, `ui.toast`, `state.set`; `wait_duration`, `repeat_count`, `stop`. fileciteturn0file1

**Day 2**  
1) Conditions: `state_equals`, `numeric_compare`. fileciteturn0file4  
2) Triggers: `time_schedule`. fileciteturn0file4  
3) **`pattern_sequence`**: `PatternSequenceWatcher`, repo list, engine wiring. fileciteturn0file3  
4) DevPanel: paste/reload/emit/overlay; import samples. fileciteturn0file0  
5) Polish + Demo: verify A–H; confirm `"restart"`; warnings on unknown fields/types; clamp ranges. fileciteturn0file6

---

## 9) Roles (3‑person split)

- **Owner A – Core Engine:** EventBus, RuleEngine, modes, pattern wiring. fileciteturn0file0  
- **Owner B – Actions & State:** Services, ActionRunner, EntityStore. fileciteturn0file0  
- **Owner C – Input & UI:** DesktopInput, DevPanel, audio/visual. fileciteturn0file0

---

## 10) Success checklist

- [ ] Paste/import JSON → rules load; unknowns warn, engine continues. fileciteturn0file0  
- [ ] Click/hold/schedule work; state gate respected. fileciteturn0file4  
- [ ] **Konami + Double‑Click fire via `pattern_sequence`** within their windows. fileciteturn0file3  
- [ ] Desktop‑only; code remains small; UPM package with Samples. fileciteturn0file0

---

## 11) What we are *not* adding this weekend (unchanged)

No manifests, validation, expression engine, parallel/pattern alternation, ring buffer, timer wheel, or XR bridge—these belong to the Quest/VR plan and can be added without changing authoring. fileciteturn0file5

---

## 12) Path to Quest (how this plan carries forward)

Authoring conventions here (explicit types, arrays‑always, units/ranges, string enums) match the long‑term Quest plan. Later we can add manifests/validation, compile to FSMs, integrate a timer wheel and ring buffer, and swap the desktop adapter for XR. The weekend JSON remains forward‑compatible. fileciteturn0file5 fileciteturn0file6

---

### Appendix: Schema cheat‑sheet (for prompting)
```
Return a single JSON object with fields in this exact order:
id, mode, triggers, conditions, actions.
- Arrays‑always (even singletons).
- Triggers: event | numeric_threshold | time_schedule | pattern_sequence
- Conditions: state_equals | numeric_compare
- Actions: service_call | wait_duration | repeat_count | stop
- Use units/ranges in field names (e.g., every_ms_10_to_600000).
- Keep names exact; unknown fields are ignored with a warning.
```
Designed per the LLM schema optimization notes to maximize first‑try validity. fileciteturn0file6
