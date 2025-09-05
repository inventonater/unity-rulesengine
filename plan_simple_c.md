# Unity Rules Engine - Weekend Hackathon Plan

## Project Goal
Build a working rules engine that proves LLM-generated JSON can drive Unity gameplay. Desktop-only, 16 hours total effort.

**Success = LLM writes JSON → Unity reads it → Gameplay happens → We can see/hear it working**

## Tech Stack
- **Unity 2022.3 LTS** (whatever you have installed)
- **UniTask** - For async operations (Package Manager: `com.cysharp.unitask`)
- **UniRx** - For event bus (Package Manager: `com.neuecc.unirx`)
- **SimpleJSON** - For parsing ([Download this file](https://raw.githubusercontent.com/Bunny83/SimpleJSON/master/SimpleJSON.cs), drop in Assets/Plugins/)

## Core Architecture (Keep It Simple)

```
JSON Files (StreamingAssets/Rules/) 
    ↓
Parse with SimpleJSON (no validation)
    ↓
Rule Engine (processes events)
    ↓
Observable Output (audio/visual/logs)
```

## The Schema (What LLMs Will Write)

### Rule Structure
```json
{
  "id": "rule_name",
  "mode": "single",  // or "restart" 
  "triggers": [...],
  "conditions": [...], 
  "actions": [...]
}
```

### Triggers (5 types)
```json
{"type": "event", "name": "game_start"}
{"type": "mouse", "button": "left", "event": "down"}
{"type": "key", "key": "space", "event": "down"}
{"type": "timer", "interval_ms": 2000}
{"type": "value", "path": "score", "above": 100}
```

### Conditions (3 types)
```json
{"type": "compare", "left": "health", "op": "<", "right": 30}
{"type": "state", "path": "game.mode", "equals": "playing"}
{"type": "all", "conditions": [...]}
```

### Actions (6 types)
```json
{"type": "log", "message": "Hello ${player}"}
{"type": "audio", "clip": "beep", "volume_0_to_1": 0.5}
{"type": "set", "path": "score", "value": "${score + 10}"}
{"type": "visual", "effect": "flash"}
{"type": "wait", "duration_ms": 500}
{"type": "service", "name": "custom_action", "data": {...}}
```

## Core Classes to Build

### 1. RuleEngine.cs
```csharp
public class RuleEngine : MonoBehaviour {
    List<Rule> rules;
    Dictionary<string, CancellationTokenSource> activeRules;
    
    void Start() {
        LoadRules();
        EventBus.Stream.Subscribe(OnEvent);
    }
    
    void LoadRules() {
        // Load all .json from StreamingAssets/Rules/
        // Parse with SimpleJSON
        // Store in rules list
    }
    
    async void OnEvent(GameEvent evt) {
        // Find matching rules
        // Check conditions  
        // Execute actions with UniTask
    }
}
```

### 2. EventBus.cs
```csharp
public static class EventBus {
    static Subject<GameEvent> stream = new();
    public static IObservable<GameEvent> Stream => stream;
    public static void Emit(string name, Dictionary<string,object> data = null);
}
```

### 3. GameState.cs
```csharp
public static class GameState {
    static Dictionary<string, object> values;
    public static T Get<T>(string path);
    public static void Set(string path, object value);
}
```

### 4. InputAdapter.cs
```csharp
public class InputAdapter : MonoBehaviour {
    void Update() {
        if (Input.GetMouseButtonDown(0))
            EventBus.Emit("mouse.left.down");
        if (Input.GetKeyDown(KeyCode.Space))
            EventBus.Emit("key.space.down");
    }
}
```

### 5. ActionExecutor.cs
```csharp
public class ActionExecutor {
    public async UniTask Execute(ActionData action) {
        switch(action.type) {
            case "log": Debug.Log(Interpolate(action.message)); break;
            case "audio": PlaySound(action.clip, action.volume); break;
            case "set": GameState.Set(action.path, Evaluate(action.value)); break;
            case "visual": FlashScreen(); break;
            case "wait": await UniTask.Delay(action.duration_ms); break;
        }
    }
}
```

## Test Scene Setup

Create a simple test scene with:
- **Canvas** with Text element showing game state
- **AudioSource** for sound effects  
- **Plane** that changes color for visual feedback
- **Debug Panel** showing last 10 events/actions

## The 5 Demo Rules (Start with These)

### 1. click_counter.json
```json
{
  "id": "click_counter",
  "mode": "single",
  "triggers": [{"type": "mouse", "button": "left", "event": "down"}],
  "actions": [
    {"type": "set", "path": "clicks", "value": "${clicks + 1}"},
    {"type": "log", "message": "Clicks: ${clicks}"},
    {"type": "audio", "clip": "click", "volume_0_to_1": 0.3}
  ]
}
```

### 2. low_health.json
```json
{
  "id": "low_health_warning",
  "mode": "restart",
  "triggers": [{"type": "value", "path": "health", "below": 30}],
  "conditions": [{"type": "state", "path": "player.alive", "equals": true}],
  "actions": [
    {"type": "audio", "clip": "warning", "volume_0_to_1": 0.8},
    {"type": "visual", "effect": "flash"},
    {"type": "log", "message": "WARNING: Low health!", "level": "error"}
  ]
}
```

### 3. auto_save.json
```json
{
  "id": "auto_save",
  "mode": "single", 
  "triggers": [{"type": "timer", "interval_ms": 5000}],
  "conditions": [{"type": "compare", "left": "unsaved_changes", "op": ">", "right": 0}],
  "actions": [
    {"type": "log", "message": "Auto-saving..."},
    {"type": "set", "path": "unsaved_changes", "value": 0},
    {"type": "visual", "effect": "flash"}
  ]
}
```

### 4. combo.json
```json
{
  "id": "double_tap",
  "mode": "restart",
  "triggers": [{"type": "key", "key": "space", "event": "down"}],
  "conditions": [
    {"type": "compare", "left": "${time - last_tap}", "op": "<", "right": 0.3}
  ],
  "actions": [
    {"type": "set", "path": "combo", "value": "${combo + 1}"},
    {"type": "audio", "clip": "combo", "volume_0_to_1": 1.0},
    {"type": "log", "message": "COMBO x${combo}!"}
  ]
}
```

### 5. debug_mode.json
```json
{
  "id": "toggle_debug",
  "mode": "single",
  "triggers": [{"type": "event", "name": "f1_pressed"}],
  "actions": [
    {"type": "set", "path": "debug_mode", "value": "${!debug_mode}"},
    {"type": "log", "message": "Debug mode: ${debug_mode}"},
    {"type": "visual", "effect": "flash"}
  ]
}
```

## Implementation Schedule

### Saturday Morning (4 hours)
**9am-10am: Setup**
- Create Unity project
- Install packages (UniTask, UniRx)
- Add SimpleJSON.cs
- Create folder structure

**10am-11am: Core Systems**
- EventBus.cs
- GameState.cs
- Basic data classes (Rule, Trigger, Action)

**11am-12pm: JSON Loading**
- Load from StreamingAssets
- Parse with SimpleJSON
- Store rules in memory

**12pm-1pm: Input & Testing**
- InputAdapter.cs
- Test scene with UI
- Verify events are firing

### Saturday Afternoon (4 hours)
**2pm-3pm: Rule Matching**
- Match triggers to events
- Implement "mode" behavior (single/restart)

**3pm-4pm: Conditions**
- Evaluate conditions
- Simple expression parsing (${variable})

**4pm-5pm: Actions**
- ActionExecutor with UniTask
- Log, Set, Audio actions

**5pm-6pm: First Demo**
- Get click_counter.json working
- Debug and fix issues

### Sunday Morning (4 hours)
**9am-10am: More Actions**
- Visual feedback (screen flash)
- Wait action
- Service pattern for extensibility

**10am-11am: Value Triggers**
- Monitor GameState changes
- Trigger when thresholds crossed

**11am-12pm: Timer Triggers**
- Periodic firing with UniTask
- Cancellation handling

**12pm-1pm: All Demos Working**
- Test all 5 example rules
- Fix remaining bugs

### Sunday Afternoon (4 hours)
**2pm-3pm: Hot Reload**
- FileSystemWatcher on StreamingAssets
- Reload rules without restart

**3pm-4pm: Polish**
- Better debug UI
- Error handling (don't crash on bad JSON)
- Visual indicators for rule firing

**4pm-5pm: LLM Testing**
- Write prompts that generate rules
- Test with actual LLM output
- Document what works

**5pm-6pm: Demo Prep**
- Record video of working system
- Clean up code
- Write README with examples

## Division of Labor (3 people)

**Person A: Core Engine**
- RuleEngine.cs
- EventBus.cs
- Rule matching/evaluation

**Person B: Actions & State**
- GameState.cs
- ActionExecutor.cs
- All action implementations

**Person C: Input & UI**
- InputAdapter.cs
- Test scene setup
- Debug UI
- Visual/audio feedback

## Definition of "Done"

✅ All 5 example rules execute correctly  
✅ Can drop new JSON in folder and it works  
✅ Visual/audio feedback proves rules are firing  
✅ LLM can generate working rules from prompts  
✅ No crashes on malformed JSON (just log errors)

## What We're NOT Doing

- ❌ Complex validation
- ❌ Performance optimization  
- ❌ VR support
- ❌ Visual rule editor
- ❌ Networking
- ❌ Save/load
- ❌ Complex expressions beyond ${var + 10}

## Quick Decisions

**JSON is malformed?** Log error, skip rule, continue  
**Performance is bad?** Limit to 20 rules for demo  
**Expression parsing is hard?** Only support ${variable} replacement  
**Behind schedule?** Cut hot reload and polish, focus on core loop  
**LLM output is wrong?** Hand-edit for demo, document the issue

## Success Metrics

1. **It works** - Rules load and execute
2. **It's observable** - Clear feedback when rules fire
3. **It's fast** - <5 seconds from save to behavior change
4. **LLM compatible** - Accepts slightly broken JSON
5. **It's demoable** - Can show the full pipeline in 2 minutes

---

**Remember: This is a proof of concept. If it works, we can build the full system later. Keep it simple, make it work, then make it better.**