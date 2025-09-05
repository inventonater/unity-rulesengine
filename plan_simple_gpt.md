# plan_simple_final.md — UPM Weekend MVP (Unity 6.2) with JSON `pattern_sequence`

This is the **integrated weekend plan** for a **Unity Package (UPM)** that lives under a host project's `Packages/` folder. It merges the lean MVP (3 triggers / 2 conditions / 4 actions), the **UPM package** structure, and a **new JSON-expressed `pattern_sequence` trigger** to support Konami Code and Double‑Click as data—not hardcoded—while staying on‑ramp for the Quest plan. fileciteturn0file2 fileciteturn0file1 fileciteturn0file3 fileciteturn0file4

---

## 0) TL;DR (what we will ship this weekend)

- **Triggers (4):** `event`, `numeric_threshold`, `time_schedule`, **`pattern_sequence` (NEW)**. fileciteturn0file2
- **Conditions (2):** `state_equals`, `numeric_compare`. fileciteturn0file2
- **Actions (4):** `service_call`, `wait_duration`, `repeat_count`, `stop`. fileciteturn0file1
- **UPM** package at `Packages/com.inventonater.rules/` with Samples. fileciteturn0file1
- **JSON Konami + Double‑Click via `pattern_sequence`** (sequence within a time window). fileciteturn0file3
- **LLM‑friendly schema**: explicit `type`, arrays‑always, units/ranges in field names, string enums, simple coercions. fileciteturn0file4

**Definition of Done:** Paste/import JSON → click/hold/schedule **and** JSON‑driven **double‑click + Konami** work; logs/toasts/beeps show behavior; desktop‑only; code stays small. fileciteturn0file2

---

## 1) UPM package layout

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
        RuleCoercion.cs
        RuleRepository.cs
      Engine/
        EventBus.cs
        RuleEngine.cs
        ConditionEval.cs
        ActionRunner.cs
        TimerService.cs
        EntityStore.cs
        PatternSequenceWatcher.cs            // NEW
      Desktop/
        DesktopInput.cs
        Services.cs
        DevPanel.cs
        Resources/beep.wav
    Editor/
      RulesEngine.Editor.asmdef              // (optional)
    Samples~/
      Demo/
        Demo.unity
        Rules/click_beep.json
        Rules/hold_to_toast.json
        Rules/periodic_hint.json
        Rules/speed_gate.json
        Rules/double_click_pattern.json      // NEW
        Rules/konami_pattern.json            // NEW
```
Keeps the lean MVP structure and adds a single, tiny new watcher for sequences. fileciteturn0file2

---

## 2) package.json (Unity 6.2 host)

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
    { "displayName": "Demo Scene & Rules", "description": "Click, hold, schedule, double-click, Konami (JSON sequence)", "path": "Samples~/Demo" }
  ]
}
```
Minimal, MIT‑friendly deps as chosen in the MVP candidates. fileciteturn0file1

**Assembly Definitions** (same as prior write‑up) keep runtime/editor split small. fileciteturn0file1

---

## 3) Scope (small but complete, with one new trigger)

### Triggers (4)
- **event** — exact event name. fileciteturn0file2  
- **numeric_threshold** — `above`/`below` numeric entity, optional `for_ms`. fileciteturn0file2  
- **time_schedule** — `every_ms_10_to_600000`. fileciteturn0file1  
- **pattern_sequence (NEW)** — **ordered list of event names** that must occur within `within_ms_10_to_5000`. (Exact match only; no alternation/filters this weekend.) This mirrors the long‑term “pattern” concept in a tiny, weekendable way. fileciteturn0file3

### Conditions (2)
- **state_equals**, **numeric_compare**. fileciteturn0file2

### Actions (4)
- **service_call**, **wait_duration**, **repeat_count**, **stop**. fileciteturn0file1

**Modes:** default `"single"`, optional `"restart"`. fileciteturn0file1

---

## 4) Authoring model (LLM‑friendly)

- Explicit `type`, arrays‑always, **units/ranges** in field names, string enums; unknown fields ignored with warning; scalar→array and string→number coercions (`"2s"`→`2000`). fileciteturn0file4

**Canonical envelope (field order matters):**
```json
{ "id": "optional_rule_id",
  "mode": "single|restart",
  "triggers": [ ... ],
  "conditions": [ ... ],
  "actions": [ ... ] }
```

**Weekend schema subset additions for `pattern_sequence`:**
```json
{ "type": "pattern_sequence",
  "within_ms_10_to_5000": 1500,
  "sequence": [ { "name": "event.name.1" }, { "name": "event.name.2" } ] }
```
We intentionally avoid data filters/alternation to keep the runtime tiny and demoable. fileciteturn0file3

---

## 5) Short code samples (only the NEW/changed bits)

### 5.1 `PatternSequenceWatcher.cs` (NEW)
```csharp
using System.Collections.Generic;
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

### 5.2 `RuleDto.cs` (add fields for `pattern_sequence`)
```csharp
public class TriggerDto {
  public string type; public string name;
  public System.Collections.Generic.List<string> entity;
  public double above, below; public int for_ms_0_to_60000;
  public int every_ms_10_to_600000;
  // NEW
  public int within_ms_10_to_5000;
  public System.Collections.Generic.List<PatternStep> sequence;
}
public class PatternStep { public string name; }
```

### 5.3 `RuleRepository.cs` (track pattern rules)
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

### 5.4 `RuleEngine.cs` (wire pattern watchers)
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

### 5.5 `DesktopInput.cs` (emit canonical key events)
```csharp
void Update(){
  // existing mouse + entities ...
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow))    EventBus.Publish("key.arrow_up.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow))  EventBus.Publish("key.arrow_down.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow))  EventBus.Publish("key.arrow_left.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow)) EventBus.Publish("key.arrow_right.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.A)) EventBus.Publish("key.a.down");
  if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.B)) EventBus.Publish("key.b.down");
}
```

Other MVP code (EventBus, Services, ActionRunner, ConditionEval, TimerService, DevPanel) stays as in the prior MVP write‑ups. fileciteturn0file1 fileciteturn0file2

---

## 6) Curated demo rules (now JSON‑driven Double‑Click & Konami)

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

**C) Scheduled hint (gated by state)**
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

**E) NEW — Double‑click via `pattern_sequence`**
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
_Note:_ Distance gating is out of scope for JSON this weekend; keep the hardcoded distance check in `DesktopInput` **optional** if you want polish. fileciteturn0file2

**F) NEW — Konami Code via `pattern_sequence`**
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
This directly tests an ordered, time‑bounded sequence trigger—useful as a stress test while remaining weekend‑scope. fileciteturn0file3

---

## 7) DevPanel (unchanged behavior, sample‑friendly)

- Paste JSON → `ReplaceAll()`; dropdown to load A–F.
- Emit custom event (name + optional JSON data).
- Overlay feed: last N events/actions and per‑rule status.  
This preserves the minimal iteration loop recommended by the MVP candidates. fileciteturn0file1

---

## 8) Implementation plan (updated with pattern step)

**Day 1**
1) Project setup; install **Newtonsoft.Json** + **UniTask**. fileciteturn0file1  
2) Data & loader: `RuleDto`, `RuleCoercion`, `RuleRepository` (parse + coerce + index). fileciteturn0file2  
3) Runtime: `EventBus` + `RuleEngine` (dispatch + modes). fileciteturn0file2  
4) Desktop input & entities (mouse/keys, `ui.mode`). fileciteturn0file2  
5) Actions & services: `audio.play`, `debug.log`, `ui.toast`; `wait_duration`, `repeat_count`, `stop`. fileciteturn0file1

**Day 2**
1) Conditions: `state_equals`, `numeric_compare`. fileciteturn0file2  
2) Triggers: `time_schedule`. fileciteturn0file1  
3) **NEW:** `pattern_sequence` — implement `PatternSequenceWatcher`, repo list, and engine wiring.  
4) DevPanel: paste/reload/emit/overlay; import samples. fileciteturn0file1  
5) Polish + Demo: verify A–F; confirm `"restart"` behavior; ensure warnings on unknown fields/types; clamp ranges. fileciteturn0file4

---

## 9) Roles (3‑person split)

- **Owner A – Core Engine:** EventBus, RuleEngine, modes, `pattern_sequence` wiring. fileciteturn0file2  
- **Owner B – Actions & State:** Services, ActionRunner, EntityStore. fileciteturn0file2  
- **Owner C – Input & UI:** DesktopInput (key events), DevPanel, audio/visual. fileciteturn0file1

---

## 10) Risks & cutlines

- If time is tight, **fall back** to the hardcoded Konami/double‑click aggregators and keep the JSON versions as optional. (The rest of the MVP remains unchanged.) fileciteturn0file2  
- Keep JSON simple—no filters/alternation—so the watcher stays tiny. The long‑term plan’s richer pattern trigger is deferred. fileciteturn0file3

---

## 11) Path to Quest (unchanged)

- Authoring conventions already match the long‑term plan: explicit types, arrays‑always, units in names; manifests/validation/AST/FSMs are **additive** later. fileciteturn0file3 fileciteturn0file4  
- Runtime evolution: swap in ring‑buffer bus, per‑rule FSMs, timer wheel for deterministic 72–120 Hz. fileciteturn0file3

---

## 12) Success checklist

- [ ] Paste/import JSON → rules load; unknowns warn, engine continues. fileciteturn0file1  
- [ ] Click/hold/schedule work; state gate respected. fileciteturn0file2  
- [ ] **Konami + Double‑Click fire via `pattern_sequence`** within their windows.  
- [ ] Desktop‑only; code ≈ small & readable; UPM package with Samples. fileciteturn0file1

---

### Notes on why this design fits the weekend
We added exactly **one** new trigger (`pattern_sequence`) to make complex sequences like **Konami** expressible in JSON—matching the long‑term plan’s pattern trigger in spirit—without dragging in validation, AST, FSM, or XR dependencies. The rest of the MVP remains the same tiny set proven in the candidate plans, and authoring stays aligned with the schema optimization guidance for LLMs. fileciteturn0file2 fileciteturn0file1 fileciteturn0file3 fileciteturn0file4
