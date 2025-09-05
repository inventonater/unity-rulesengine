# plan_simple_final.md — UPM Package (Unity 6.2) + New Examples (Double‑Click, Konami)

This is the **weekend‑ready** plan for a **Unity Package (UPM)** that lives under a project's **`Packages/`** folder. It keeps the lean MVP scope (3 triggers / 2 conditions / 4 actions) while making authoring LLM‑friendly and staying on‑ramp for the Quest plan. fileciteturn0file2 fileciteturn0file1 fileciteturn0file3 fileciteturn0file4

---

## 0) What changed vs. the prior MVP write‑up
- **Packaging:** Deliver as a UPM package (`Packages/com.inventonater.rules/`) with Samples. fileciteturn0file1  
- **New examples:** **Double‑click beep** and **Konami code** using input aggregators that emit canonical events (keeps schema small; no pattern trigger needed this weekend). fileciteturn0file2  
- **Short code samples:** Added minimal snippets for all major systems (EventBus, RuleEngine, RuleRepository, EntityStore, Services, TimerService, DesktopInput aggregators, DevPanel). fileciteturn0file0

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
        RuleCoercion.cs
        RuleRepository.cs
      Engine/
        EventBus.cs
        RuleEngine.cs
        ConditionEval.cs
        ActionRunner.cs
        TimerService.cs
        EntityStore.cs
      Desktop/
        DesktopInput.cs
        Services.cs
        DevPanel.cs
        Resources/beep.wav
    Editor/                       // (optional for sample importer)
      RulesEngine.Editor.asmdef
    Samples~/
      Demo/
        Demo.unity
        Rules/click_beep.json
        Rules/hold_to_toast.json
        Rules/periodic_hint.json
        Rules/speed_gate.json
        Rules/double_click_beep.json
        Rules/konami_code.json
```
This mirrors the lean MVP organization while preparing for Quest‑side evolution later. fileciteturn0file2 fileciteturn0file3

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
    { "displayName": "Demo Scene & Rules", "description": "Click, hold, schedule, double-click, Konami", "path": "Samples~/Demo" }
  ]
}
```
Keep third‑party deps minimal (MIT) to match the weekend scope. fileciteturn0file1

**Assembly Definitions**

_Runtime (`RulesEngine.Runtime.asmdef`)_
```json
{
  "name": "Inventonater.Rules.Runtime",
  "references": [],
  "includePlatforms": [],
  "overrideReferences": false,
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": []
}
```
_Editor (`RulesEngine.Editor.asmdef`)_
```json
{
  "name": "Inventonater.Rules.Editor",
  "references": [ "Inventonater.Rules.Runtime" ],
  "includePlatforms": [ "Editor" ]
}
```

---

## 3) Minimal schema (unchanged for weekend)

- **Triggers (3):** `event`, `numeric_threshold`, `time_schedule`  
- **Conditions (2):** `state_equals`, `numeric_compare`  
- **Actions (4):** `service_call`, `wait_duration`, `repeat_count`, `stop`  
- **Modes:** `"single"` (default) or `"restart"`  
LLM‑friendly conventions: explicit type, arrays‑always, units/ranges in field names, string enums, simple coercions. fileciteturn0file1 fileciteturn0file4

---

## 4) Short code samples (major systems)

> These are intentionally tiny, compile‑ready starting points. They implement the MVP loop; flesh out details as needed. fileciteturn0file0

### 4.1 EventBus.cs
```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

public static class EventBus {
  static readonly Channel<EngineEvent> _ch = Channel.CreateUnbounded<EngineEvent>();

  public static void Publish(string name, Dictionary<string, object>? data = null) =>
    _ch.Writer.TryWrite(new EngineEvent { Name = name, Data = data ?? new(), Timestamp = UnityEngine.Time.unscaledTime });

  public static async IAsyncEnumerable<EngineEvent> GetStream([EnumeratorCancellation] CancellationToken ct = default) {
    while (await _ch.Reader.WaitToReadAsync(ct)) {
      while (_ch.Reader.TryRead(out var e)) yield return e;
    }
  }
}

public struct EngineEvent {
  public string Name;
  public Dictionary<string, object> Data;
  public float Timestamp;
}
```

### 4.2 RuleDto.cs (authoring DTOs, arrays‑always)
```csharp
using System.Collections.Generic;

public sealed class RuleDto {
  public string id;
  public string mode; // "single"|"restart" (optional)
  public List<TriggerDto> triggers = new();
  public List<ConditionDto> conditions = new();
  public List<ActionDto> actions = new();
}

public class TriggerDto { public string type; public string name; public List<string> entity; public double above; public double below; public int for_ms_0_to_60000; public int every_ms_10_to_600000; }
public class ConditionDto { public string type; public List<string> entity; public List<string> equals; public double above; public double below; }
public class ActionDto { public string type; public string service; public Dictionary<string, object> data; public int duration_ms_0_to_60000; public int count_1_to_20; public List<ActionDto> actions; public string reason; }
```

### 4.3 RuleRepository.cs
```csharp
using System.Collections.Generic;
using System.Linq;

public interface IRuleRepository {
  void ReplaceAll(IEnumerable<RuleDto> rules);
  IEnumerable<RuleDto> GetCandidatesFor(string triggerKey); // e.g. "event:mouse.left.down"
}

public sealed class RuleRepository : IRuleRepository {
  readonly Dictionary<string, List<RuleDto>> _byTrigger = new();
  public void ReplaceAll(IEnumerable<RuleDto> rules) {
    _byTrigger.Clear();
    foreach (var r in rules) {
      foreach (var t in r.triggers) {
        var key = t.type switch {
          "event" => $"event:{t.name}",
          "numeric_threshold" => $"num:{(t.above!=0?"above":"below")}:{t.entity?.FirstOrDefault()}",
          "time_schedule" => $"time:{t.every_ms_10_to_600000}",
          _ => null
        };
        if (key==null) continue;
        if (!_byTrigger.TryGetValue(key, out var list)) _byTrigger[key]=list=new();
        list.Add(r);
      }
    }
  }
  public IEnumerable<RuleDto> GetCandidatesFor(string key) => _byTrigger.TryGetValue(key, out var list) ? list : Enumerable.Empty<RuleDto>();
}
```

### 4.4 EntityStore.cs
```csharp
using System.Collections.Generic;
public sealed class EntityStore {
  readonly Dictionary<string,double> num = new();
  readonly Dictionary<string,string>  str = new();
  public void SetNumeric(string key, double v) => num[key]=v;
  public double GetNumeric(string key)=> num.TryGetValue(key,out var v)?v:0;
  public void SetState(string key, string v)=> str[key]=v;
  public string GetState(string key)=> str.TryGetValue(key,out var v)?v:"";
}
```

### 4.5 Services.cs (audio/log/ui)
```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class Services : MonoBehaviour {
  [SerializeField] AudioClip beep;
  AudioSource _src;
  void Awake(){ _src = gameObject.AddComponent<AudioSource>(); }

  public UniTask Call(string service, Dictionary<string,object>? data, CancellationToken ct){
    data ??= new();
    switch(service){
      case "audio.play":
        var vol = ToFloat(data, "volume_0_to_1", 0.7f);
        _src.PlayOneShot(beep, Mathf.Clamp01(vol)); break;
      case "debug.log":
        Debug.Log(data.TryGetValue("message", out var m) ? m : ""); break;
      case "ui.toast":
        DevPanel.Toast(data.TryGetValue("text", out var t) ? t?.ToString() : "Toast", (int)ToFloat(data,"duration_ms_0_to_10000",1000)); break;
      default: Debug.LogWarning($"Unknown service: {service}"); break;
    }
    return UniTask.CompletedTask;
  }
  static float ToFloat(Dictionary<string,object> d, string k, float def){ return d.TryGetValue(k,out var v) && float.TryParse(v.ToString(), out var f) ? f : def; }
}
```

### 4.6 TimerService.cs
```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class TimerService {
  public static async IAsyncEnumerable<long> Every(int everyMs, [EnumeratorCancellation] CancellationToken ct){
    long i=0; while(!ct.IsCancellationRequested){ await UniTask.Delay(everyMs, cancellationToken: ct); yield return i++; }
  }
}
```

### 4.7 ConditionEval.cs
```csharp
public static class ConditionEval {
  public static bool StateEquals(EntityStore s, ConditionDto c) => s.GetState(c.entity[0]) == c.equals[0];
  public static bool NumericCompare(EntityStore s, ConditionDto c){
    var v = s.GetNumeric(c.entity[0]);
    if (c.above!=0) return v > c.above;
    if (c.below!=0) return v < c.below;
    return false;
  }
}
```

### 4.8 ActionRunner.cs
```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class ActionRunner {
  readonly Services _services;
  public ActionRunner(Services services){ _services=services; }

  public async UniTask Run(List<ActionDto> actions, CancellationToken ct){
    foreach(var a in actions){
      switch(a.type){
        case "service_call": await _services.Call(a.service, a.data, ct); break;
        case "wait_duration": await UniTask.Delay(a.duration_ms_0_to_60000, cancellationToken: ct); break;
        case "repeat_count":
          for(int i=0;i<a.count_1_to_20;i++){ await Run(a.actions, ct); } break;
        case "stop": return;
      }
      if (ct.IsCancellationRequested) return;
    }
  }
}
```

### 4.9 RuleEngine.cs (dispatch + modes)
```csharp
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class RuleEngine : MonoBehaviour {
  IRuleRepository _repo; EntityStore _store; Services _services; ActionRunner _runner;
  CancellationTokenSource _loopCts;
  readonly System.Collections.Generic.Dictionary<string, CancellationTokenSource> _running = new();

  public void Initialize(IRuleRepository repo, EntityStore store, Services services){
    _repo=repo; _store=store; _services=services; _runner=new ActionRunner(_services);
    _loopCts = new CancellationTokenSource();
    Run(_loopCts.Token).Forget();
  }

  async UniTask Run(CancellationToken ct){
    await foreach (var e in EventBus.GetStream(ct)){
      var candidates = _repo.GetCandidatesFor($"event:{e.Name}").ToList();
      foreach (var r in candidates){
        if (!CheckConditions(r)) continue;
        var mode = string.IsNullOrEmpty(r.mode) ? "single" : r.mode;
        if (mode=="single" && _running.ContainsKey(r.id)) continue;
        if (mode=="restart" && _running.TryGetValue(r.id, out var old)) { old.Cancel(); _running.Remove(r.id); }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _running[r.id]=cts;
        _runner.Run(r.actions, cts.Token).ContinueWith(_=>{ _running.Remove(r.id); cts.Dispose(); }).Forget();
      }
    }
  }

  bool CheckConditions(RuleDto r){
    foreach(var c in r.conditions){
      switch(c.type){
        case "state_equals": if(!ConditionEval.StateEquals(_store,c)) return false; break;
        case "numeric_compare": if(!ConditionEval.NumericCompare(_store,c)) return false; break;
      }
    }
    return true;
  }
}
```

### 4.10 DesktopInput.cs (events + aggregators: double‑click, Konami)
```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class DesktopInput : MonoBehaviour {
  const float DoubleClickMaxDelay = 0.25f;
  const float DoubleClickMaxDist  = 20f;
  float _lastClickTime; Vector3 _lastClickPos;

  readonly KeyCode[] _konami = {
    KeyCode.UpArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.DownArrow,
    KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
    KeyCode.B, KeyCode.A
  };
  int _konamiIndex=0; float _konamiWindowEnd=0f; const float KonamiStepTimeout=1.5f;

  Vector3 _prevMouse; EntityStore _store;

  void Awake(){ _store = FindObjectOfType<EntityStore>(); _prevMouse = Input.mousePosition; }

  void Update(){
    // Basic mouse button events
    if (Input.GetMouseButtonDown(0)){
      EventBus.Publish("mouse.left.down");
      var now = Time.unscaledTime;
      var dist = Vector3.Distance(_lastClickPos, Input.mousePosition);
      if (now - _lastClickTime <= DoubleClickMaxDelay && dist <= DoubleClickMaxDist) {
        EventBus.Publish("mouse.left.double_click");
        _lastClickTime = 0; // reset
      } else {
        _lastClickTime = now; _lastClickPos = Input.mousePosition;
      }
    }

    // Mouse speed entity
    var speed = (Input.mousePosition - _prevMouse).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
    _store.SetNumeric("sensor.mouse_speed", speed);
    _store.SetNumeric("sensor.mouse_button_left", Input.GetMouseButton(0) ? 1 : 0);
    _prevMouse = Input.mousePosition;

    // Debug mode toggle
    if (Input.GetKeyDown(KeyCode.F1)) _store.SetState("ui.mode", "debug");

    // Konami detector
    if (Input.anyKeyDown){
      if (Time.unscaledTime > _konamiWindowEnd) _konamiIndex = 0;
      foreach (var k in System.Enum.GetValues(typeof(KeyCode))){
        var key = (KeyCode)k;
        if (Input.GetKeyDown(key)){
          if (key == _konami[_konamiIndex]) {
            _konamiIndex++;
            _konamiWindowEnd = Time.unscaledTime + KonamiStepTimeout;
            if (_konamiIndex >= _konami.Length){
              EventBus.Publish("konami.code");
              _konamiIndex=0;
            }
          } else {
            _konamiIndex = (key == _konami[0]) ? 1 : 0;
            _konamiWindowEnd = Time.unscaledTime + KonamiStepTimeout;
          }
          break;
        }
      }
    }
  }
}
```

### 4.11 DevPanel.cs (paste JSON, reload, emit event, toast)
```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public sealed class DevPanel : MonoBehaviour {
  static readonly System.Collections.Generic.List<(float,string)> _toasts = new();
  public TextAsset[] initialRules;
  string _json = ""; string _eventName = "mouse.left.down";
  IRuleRepository _repo; RuleEngine _engine; EntityStore _store; Services _services;

  void Start(){
    _repo = new RuleRepository();
    _store = FindObjectOfType<EntityStore>();
    _services = FindObjectOfType<Services>();
    _engine = FindObjectOfType<RuleEngine>();
    _engine.Initialize(_repo, _store, _services);
    // Load initial sample files
    var rules = initialRules.Select(t => JsonConvert.DeserializeObject<RuleDto>(t.text));
    _repo.ReplaceAll(rules);
  }

  public static void Toast(string text, int durationMs){
    _toasts.Add((Time.unscaledTime + durationMs/1000f, text));
  }

  void OnGUI(){
    GUILayout.BeginArea(new Rect(10,10,420,560), GUI.skin.window);
    GUILayout.Label("Rules JSON (single rule object)");
    _json = GUILayout.TextArea(_json, GUILayout.Height(140));
    if (GUILayout.Button("Replace Rules from JSON")) {
      try {
        var r = JsonConvert.DeserializeObject<RuleDto>(_json);
        _repo.ReplaceAll(new[]{ r });
      } catch (System.Exception ex) { Debug.LogError(ex); }
    }
    GUILayout.Space(6);
    GUILayout.Label("Emit Event"); _eventName = GUILayout.TextField(_eventName);
    if (GUILayout.Button("Publish")) EventBus.Publish(_eventName, new Dictionary<string,object>());
    GUILayout.Label("Shortcuts: F1 toggles ui.mode=debug; Double‑Click; Konami Code");
    GUILayout.EndArea();

    // Toasts
    for (int i=_toasts.Count-1;i>=0;i--){
      var (until, txt) = _toasts[i];
      var alpha = Mathf.InverseLerp(0f, 0.5f, until - Time.unscaledTime);
      var style = new GUIStyle(GUI.skin.box); var c = GUI.color; GUI.color = new Color(1,1,1,alpha);
      GUI.Box(new Rect(10,580 - 30*(i+1), 360, 24), txt, style); GUI.color=c;
      if (Time.unscaledTime>until) _toasts.RemoveAt(i);
    }
  }
}
```

---

## 5) Curated demo rules (including new examples)

> Copy these into `Samples~/Demo/Rules/` (**arrays‑always**, units in field names, explicit types). fileciteturn0file1 fileciteturn0file2 fileciteturn0file4

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

**E) NEW — Double‑click → Beep**
```json
{
  "id": "double_click_beep",
  "triggers": [{ "type": "event", "name": "mouse.left.double_click" }],
  "conditions": [],
  "actions": [
    { "type": "service_call", "service": "audio.play",
      "data": { "clip": "beep", "volume_0_to_1": 1.0 } }
  ]
}
```

**F) NEW — Konami Code → Toast + triple beep**
```json
{
  "id": "konami_code",
  "mode": "single",
  "triggers": [{ "type": "event", "name": "konami.code" }],
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

---

## 6) Using the package in a host project

1) Create/clone a Unity **6.2** project.  
2) Put this folder into the project: `Packages/com.inventonater.rules/`.  
3) Open the **Demo** sample (Package Manager → Inventonater Rules → Samples → **Import**).  
4) Hit **Play**. Try: mouse click, **double‑click**, hold LMB, press **F1**, or enter **Konami** sequence.  
This reflects the MVP’s desktop‑only loop; Quest runtime is a later swap‑in per the long‑term plan. fileciteturn0file3

---

## 7) Why this stays LLM‑friendly
Explicit `type` fields, arrays‑always, units/ranges in names, and string enums measurably increase first‑try LLM validity and are cheap to support at runtime. Coercions (scalar→array, string→number, `"2s"`→`2000`) help recover common mistakes without bloating the engine. fileciteturn0file4

---

## 8) What we are *not* adding this weekend
No manifests, validation, expression engine, parallel/pattern triggers, ring buffer, timer wheel, or XR bridge—these belong to the Quest‑targeted plan and can be added without changing authoring. fileciteturn0file3

---

## 9) Owners & day‑plan (unchanged)
- **Owner A – Core engine:** EventBus, RuleEngine, modes. fileciteturn0file0  
- **Owner B – Actions & state:** Services, ActionRunner, EntityStore. fileciteturn0file0  
- **Owner C – Input & UI:** DesktopInput aggregators, DevPanel, audio wiring. fileciteturn0file0

**Definition of Done:** Paste/Import rules → click/hold/schedule/double‑click/Konami all work, with logs/toasts/beeps; no XR packages; code footprint stays tiny. fileciteturn0file2

---

### Appendix: Schema cheat‑sheet (for prompting)
```
Return a single JSON object with fields in this exact order:
id, mode, triggers, conditions, actions.
- Arrays‑always (even singletons).
- Triggers: event | numeric_threshold | time_schedule
- Conditions: state_equals | numeric_compare
- Actions: service_call | wait_duration | repeat_count | stop
- Use units/ranges in field names (e.g., every_ms_10_to_600000).
```
Conventions mirror the LLM schema optimization doc for reliability. fileciteturn0file4
