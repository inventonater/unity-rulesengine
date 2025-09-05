# Unity Rules Engine - Weekend MVP Plan

## Overview
A minimal Event-Condition-Action (ECA) rules engine for Unity that can be built in a weekend. Desktop-only with mouse/keyboard input. Focus on proving the core loop works.

**MVP Goals:**
- JSON rules that execute in Unity
- 3 trigger types, 2 condition types, 4 action types
- Simple event matching and action execution
- Desktop mouse/keyboard input only
- Use UniTask for async operations

## Architecture (Simplified)

```
┌─────────────────────────────────────────┐
│           JSON Rule Files               │
│         (Minimal validation)            │
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│          Rules Engine                   │
│  ┌──────────┐  ┌──────────────────┐   │
│  │  Rules   │  │  Event Queue     │   │
│  │Dictionary│  │  (AsyncQueue)    │   │
│  └──────────┘  └──────────────────┘   │
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│         Desktop Integration             │
│  ┌──────────┐  ┌──────────────────┐   │
│  │  Mouse/  │  │    Services      │   │
│  │ Keyboard │  │  (Audio/Log)     │   │
│  └──────────┘  └──────────────────┘   │
└──────────────────────────────────────────┘
```

## Dependencies

**Required Unity Packages:**
- **Newtonsoft.Json for Unity** - JSON parsing (MIT licensed)
- **UniTask** - Async operations without coroutines (MIT licensed)

## Simplified Schema

### Core Rule Structure
```json
{
  "id": "unique_rule_id",
  "triggers": [],
  "conditions": [],
  "actions": []
}
```

### MVP Trigger Types (Only 3)

#### 1. Event Trigger
```json
{
  "type": "event",
  "name": "mouse.left.down"
}
```

#### 2. Numeric Threshold
```json
{
  "type": "numeric_threshold",
  "entity": ["sensor.mouse_speed"],
  "above": 400
}
```

#### 3. Timer Trigger
```json
{
  "type": "timer",
  "every_ms": 2000
}
```

### MVP Condition Types (Only 2)

#### 1. State Equals
```json
{
  "type": "state_equals",
  "entity": ["ui.mode"],
  "equals": ["debug"]
}
```

#### 2. Numeric Compare
```json
{
  "type": "numeric_compare",
  "entity": ["sensor.mouse_speed"],
  "above": 300
}
```

### MVP Action Types (Only 4)

#### 1. Service Call
```json
{
  "type": "service_call",
  "service": "audio.play",
  "data": {
    "clip": "beep",
    "volume": 0.5
  }
}
```

#### 2. Wait Duration
```json
{
  "type": "wait_duration",
  "duration_ms": 1000
}
```

#### 3. Log Message
```json
{
  "type": "log",
  "message": "Rule triggered!"
}
```

#### 4. Repeat Count
```json
{
  "type": "repeat_count",
  "count": 3,
  "actions": [...]
}
```

## No Manifest for MVP
Skip manifest validation for weekend. Hardcode entity names and service names directly in code.

## Implementation (Weekend Scope)

### Core Classes

```csharp
// Main engine using UniTask
public class RulesEngine : MonoBehaviour
{
    [SerializeField] TextAsset[] ruleFiles;
    
    Dictionary<string, Rule> rules = new Dictionary<string, Rule>();
    Channel<GameEvent> eventQueue;
    Dictionary<string, CancellationTokenSource> activeRules;
    
    async UniTaskVoid Start()
    {
        eventQueue = Channel.CreateUnbounded<GameEvent>();
        activeRules = new Dictionary<string, CancellationTokenSource>();
        
        LoadRules();
        RegisterServices();
        
        // Start event processing loop
        await ProcessEvents(this.GetCancellationTokenOnDestroy());
    }
    
    void LoadRules()
    {
        foreach (var file in ruleFiles)
        {
            var rule = JsonConvert.DeserializeObject<Rule>(file.text);
            rules[rule.id] = rule;
        }
    }
    
    async UniTask ProcessEvents(CancellationToken ct)
    {
        await foreach (var evt in eventQueue.Reader.ReadAllAsync(ct))
        {
            foreach (var rule in rules.Values)
            {
                if (MatchesTrigger(rule, evt))
                {
                    ExecuteRule(rule, evt).Forget();
                }
            }
        }
    }
    
    async UniTaskVoid ExecuteRule(Rule rule, GameEvent evt)
    {
        // Check conditions
        if (!CheckConditions(rule.conditions))
            return;
            
        // Handle restart mode
        if (activeRules.ContainsKey(rule.id))
        {
            activeRules[rule.id].Cancel();
        }
        
        var cts = new CancellationTokenSource();
        activeRules[rule.id] = cts;
        
        try
        {
            // Execute actions
            await ExecuteActions(rule.actions, cts.Token);
        }
        finally
        {
            activeRules.Remove(rule.id);
            cts.Dispose();
        }
    }
}

// Simple data classes
[Serializable]
public class Rule
{
    public string id;
    public List<JObject> triggers;
    public List<JObject> conditions;
    public List<JObject> actions;
}

[Serializable]
public class GameEvent
{
    public string name;
    public Dictionary<string, object> data;
    public float timestamp;
}
```

### Unity Integration Points

```csharp
// Desktop Input
public class DesktopInput : MonoBehaviour
{
    RulesEngine engine;
    EntityStore entities;
    Vector3 lastMousePos;
    
    void Update()
    {
        // Mouse events
        if (Input.GetMouseButtonDown(0))
        {
            engine.PublishEvent("mouse.left.down", null);
        }
        if (Input.GetMouseButtonUp(0))
        {
            engine.PublishEvent("mouse.left.up", null);
        }
        
        // Calculate mouse speed
        var mouseSpeed = (Input.mousePosition - lastMousePos).magnitude;
        entities.SetNumeric("sensor.mouse_speed", mouseSpeed);
        lastMousePos = Input.mousePosition;
        
        // Keyboard events
        if (Input.GetKeyDown(KeyCode.F1))
        {
            entities.SetState("ui.mode", "debug");
        }
    }
}

// Entity Store
public class EntityStore : MonoBehaviour
{
    Dictionary<string, float> numericValues = new Dictionary<string, float>();
    Dictionary<string, string> stateValues = new Dictionary<string, string>();
    
    public void SetNumeric(string entity, float value)
    {
        numericValues[entity] = value;
    }
    
    public float GetNumeric(string entity)
    {
        return numericValues.TryGetValue(entity, out var val) ? val : 0;
    }
    
    public void SetState(string entity, string value)
    {
        stateValues[entity] = value;
    }
    
    public string GetState(string entity)
    {
        return stateValues.TryGetValue(entity, out var val) ? val : "";
    }
}

// Audio Service
public class AudioService : MonoBehaviour
{
    public AudioClip beepClip;
    AudioSource audioSource;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    
    public async UniTask PlaySound(Dictionary<string, object> data)
    {
        var volume = Convert.ToSingle(data.GetValueOrDefault("volume", 1.0f));
        audioSource.PlayOneShot(beepClip, volume);
    }
}
```

## MVP Examples (Start with These)

### Example 1: Click Sound
```json
{
  "id": "click_beep",
  "triggers": [{
    "type": "event",
    "name": "mouse.left.down"
  }],
  "conditions": [],
  "actions": [{
    "type": "service_call",
    "service": "audio.play",
    "data": {
      "clip": "beep",
      "volume": 0.5
    }
  }]
}
```

### Example 2: Fast Mouse Warning
```json
{
  "id": "speed_warning",
  "triggers": [{
    "type": "numeric_threshold",
    "entity": ["sensor.mouse_speed"],
    "above": 400
  }],
  "conditions": [],
  "actions": [
    {
      "type": "log",
      "message": "Mouse moving fast!"
    },
    {
      "type": "service_call",
      "service": "audio.play",
      "data": {"clip": "beep", "volume": 1.0}
    }
  ]
}
```

### Example 3: Periodic Debug Message
```json
{
  "id": "debug_timer",
  "triggers": [{
    "type": "timer",
    "every_ms": 2000
  }],
  "conditions": [{
    "type": "state_equals",
    "entity": ["ui.mode"],
    "equals": ["debug"]
  }],
  "actions": [
    {
      "type": "log",
      "message": "Debug mode active"
    },
    {
      "type": "repeat_count",
      "count": 3,
      "actions": [
        {
          "type": "service_call",
          "service": "audio.play",
          "data": {"clip": "beep", "volume": 0.3}
        },
        {
          "type": "wait_duration",
          "duration_ms": 100
        }
      ]
    }
  ]
}
```

## Weekend Timeline

### Saturday Morning (4 hours)
- Set up Unity project
- Install Newtonsoft.Json and UniTask
- Create basic data structures (Rule, GameEvent)
- Implement JSON loading

### Saturday Afternoon (4 hours)
- Build RulesEngine with UniTask async
- Implement event queue (Channel)
- Create trigger matching logic
- Add condition checking (state_equals, numeric_compare)

### Sunday Morning (4 hours)
- Implement EntityStore
- Create DesktopInput (mouse/keyboard)
- Implement actions (service_call, wait, log, repeat)
- Add AudioService

### Sunday Afternoon (4 hours)
- Wire up timer triggers
- Test with 3 example rules
- Debug and polish
- Verify all examples work

## What We're Skipping (For Now)

- VR/XR support
- Manifest validation
- Complex expressions
- Pattern matching
- Variables
- Hot reload
- Editor tools
- Performance optimization
- Comprehensive error handling
- DevConsole

## Success Criteria

✅ Can load rules from JSON files
✅ Mouse click plays sound
✅ Fast mouse movement triggers warning
✅ Timer with conditions works
✅ All examples work without code changes
✅ Uses UniTask for async operations
✅ Desktop-only, no VR dependencies

## Next Steps After MVP

Once the weekend prototype works:
1. Add VR/XR support
2. Add more trigger/action types
3. Implement manifest validation
4. Add expression support
5. Build DevConsole for testing
6. Create LLM training examples

This minimal approach proves the core concept works in a weekend!