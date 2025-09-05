# Unity Rules Engine - Weekend MVP Plan

## Overview
A simplified Event-Condition-Action (ECA) rules engine for Unity VR that can be built in a weekend. Focus on core functionality with JSON-based rules that work reliably with LLMs.

**MVP Goals:**
- JSON rules that execute in Unity
- 3-4 trigger types, 3-4 action types
- Simple event matching and action execution
- No compilation step - direct JSON to runtime
- Use Unity's built-in features wherever possible

## Architecture (Simplified)

```
┌─────────────────────────────────────────┐
│           JSON Rule Files               │
│         (Simple validation)             │
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│          Rules Engine                   │
│  ┌──────────┐  ┌──────────────────┐   │
│  │  Rules   │  │  Event Queue     │   │
│  │Dictionary│  │  (Queue<Event>)  │   │
│  └──────────┘  └──────────────────┘   │
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│         Unity Integration               │
│  ┌──────────┐  ┌──────────────────┐   │
│  │XR Events │  │Service Registry  │   │
│  │          │  │ (UnityEvents)    │   │
│  └──────────┘  └──────────────────┘   │
└──────────────────────────────────────────┘
```

## Dependencies

**Required Unity Packages:**
- **Newtonsoft.Json for Unity** - JSON parsing and validation
- **Meta XR SDK** - For VR input/haptics

**Optional (Nice to Have):**
- **NaughtyAttributes** - Better inspector UI for testing
- **UniTask** - If async operations needed (can use coroutines instead)

## Simplified Schema

### Core Rule Structure
```json
{
  "id": "unique_rule_id",
  "description": "Human readable description",
  "enabled": true,
  "triggers": [],
  "conditions": [],
  "actions": []
}
```

### MVP Trigger Types (Pick 3-4)

#### 1. Value Change Trigger (combines state + numeric)
```json
{
  "type": "value_change",
  "entity": "player.health",
  "from": "normal",  // Optional: previous state/value
  "to": "low",       // Required: new state/value
  "below": 30,       // Optional: numeric threshold
  "above": 0         // Optional: numeric threshold
}
```

#### 2. Zone Trigger
```json
{
  "type": "zone",
  "entity": "player",
  "zone": "dark_cave",
  "event": "enter"  // or "leave"
}
```

#### 3. Input Trigger
```json
{
  "type": "input",
  "control": "right.trigger",
  "event": "pressed"  // or "released", "held"
}
```

#### 4. Timer Trigger
```json
{
  "type": "timer",
  "every_seconds": 5  // Simple recurring timer
}
```

### MVP Condition Types (Pick 2-3)

#### 1. Value Check
```json
{
  "type": "value_check",
  "entity": "player.health",
  "equals": "low",      // For states
  "above": 20,          // For numbers
  "below": 50
}
```

#### 2. Zone Check
```json
{
  "type": "zone_check",
  "entity": "player",
  "zone": "dark_cave",
  "inside": true
}
```

#### 3. Simple Logic
```json
{
  "type": "all_of",  // or "any_of"
  "conditions": [...]
}
```

### MVP Action Types (Pick 3-4)

#### 1. Service Call (Using UnityEvents)
```json
{
  "type": "service_call",
  "service": "haptics.pulse",
  "data": {
    "hand": "right",
    "amplitude": 0.5,
    "duration_ms": 50
  }
}
```

#### 2. Wait
```json
{
  "type": "wait",
  "duration_ms": 1000
}
```

#### 3. Set Value
```json
{
  "type": "set_value",
  "entity": "lights.torch",
  "value": "on"
}
```

#### 4. Sequence
```json
{
  "type": "sequence",
  "actions": [...]  // Execute in order
}
```

## Simple Manifest (Single File)

```json
{
  "entities": {
    "player": {
      "health": {
        "type": "numeric",
        "min": 0,
        "max": 100,
        "states": {
          "critical": [0, 20],
          "low": [20, 40],
          "normal": [40, 80],
          "high": [80, 100]
        }
      },
      "zone": {
        "type": "string",
        "current": "none"
      }
    },
    "lights": {
      "torch": {
        "type": "enum",
        "values": ["on", "off"]
      }
    }
  },
  "zones": {
    "dark_cave": {
      "center": [0, 0, 0],
      "radius": 10
    }
  },
  "services": {
    "haptics.pulse": {
      "parameters": ["hand", "amplitude", "duration_ms"]
    }
  }
}
```

## Implementation (Weekend Scope)

### Core Classes

```csharp
// Main engine - MonoBehaviour
public class SimpleRulesEngine : MonoBehaviour
{
    [SerializeField] TextAsset[] ruleFiles;
    [SerializeField] TextAsset manifestFile;
    
    Dictionary<string, Rule> rules = new Dictionary<string, Rule>();
    Queue<GameEvent> eventQueue = new Queue<GameEvent>();
    ServiceRegistry services;
    
    void Start()
    {
        LoadManifest();
        LoadRules();
        RegisterServices();
        StartCoroutine(ProcessEvents());
    }
    
    void LoadRules()
    {
        foreach (var file in ruleFiles)
        {
            var rule = JsonConvert.DeserializeObject<Rule>(file.text);
            if (ValidateRule(rule))
            {
                rules[rule.id] = rule;
            }
        }
    }
    
    IEnumerator ProcessEvents()
    {
        while (true)
        {
            while (eventQueue.Count > 0)
            {
                var evt = eventQueue.Dequeue();
                
                // Find matching rules
                foreach (var rule in rules.Values)
                {
                    if (rule.enabled && MatchesTrigger(rule, evt))
                    {
                        StartCoroutine(ExecuteRule(rule, evt));
                    }
                }
            }
            yield return null;
        }
    }
    
    IEnumerator ExecuteRule(Rule rule, GameEvent evt)
    {
        // Check conditions
        if (!CheckConditions(rule.conditions))
            yield break;
            
        // Execute actions
        foreach (var action in rule.actions)
        {
            yield return ExecuteAction(action);
        }
    }
}

// Simple data classes
[Serializable]
public class Rule
{
    public string id;
    public string description;
    public bool enabled = true;
    public List<Trigger> triggers;
    public List<Condition> conditions;
    public List<Action> actions;
}

[Serializable]
public class GameEvent
{
    public string type;
    public string entity;
    public object value;
    public Dictionary<string, object> data;
}

// Service registry using UnityEvents
public class ServiceRegistry : MonoBehaviour
{
    [Serializable]
    public class ServiceCall : UnityEvent<Dictionary<string, object>> { }
    
    public Dictionary<string, ServiceCall> services = new Dictionary<string, ServiceCall>();
    
    public void RegisterService(string name, UnityAction<Dictionary<string, object>> handler)
    {
        if (!services.ContainsKey(name))
            services[name] = new ServiceCall();
        services[name].AddListener(handler);
    }
    
    public void CallService(string name, Dictionary<string, object> data)
    {
        if (services.ContainsKey(name))
            services[name].Invoke(data);
    }
}
```

### Unity Integration Points

```csharp
// XR Input Bridge
public class XRInputBridge : MonoBehaviour
{
    SimpleRulesEngine engine;
    
    void Update()
    {
        // Check right trigger
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            engine.QueueEvent(new GameEvent 
            {
                type = "input",
                entity = "right.trigger",
                value = "pressed"
            });
        }
    }
}

// Simple Zone System
public class SimpleZone : MonoBehaviour
{
    public string zoneName;
    SimpleRulesEngine engine;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            engine.QueueEvent(new GameEvent
            {
                type = "zone",
                entity = "player",
                data = new Dictionary<string, object>
                {
                    ["zone"] = zoneName,
                    ["event"] = "enter"
                }
            });
        }
    }
}

// Haptics Service
public class HapticsService : MonoBehaviour
{
    void Start()
    {
        var registry = GetComponent<ServiceRegistry>();
        registry.RegisterService("haptics.pulse", OnHapticsPulse);
    }
    
    void OnHapticsPulse(Dictionary<string, object> data)
    {
        var hand = (string)data["hand"];
        var amplitude = Convert.ToSingle(data["amplitude"]);
        var duration = Convert.ToInt32(data["duration_ms"]);
        
        var controller = hand == "right" ? 
            OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            
        OVRInput.SetControllerVibration(1, amplitude, controller);
        StartCoroutine(StopVibration(controller, duration / 1000f));
    }
}
```

## MVP Examples (Start with These)

### Example 1: Button Haptic Feedback
```json
{
  "id": "button_haptic",
  "description": "Haptic feedback when trigger pressed",
  "enabled": true,
  "triggers": [{
    "type": "input",
    "control": "right.trigger",
    "event": "pressed"
  }],
  "conditions": [],
  "actions": [{
    "type": "service_call",
    "service": "haptics.pulse",
    "data": {
      "hand": "right",
      "amplitude": 0.5,
      "duration_ms": 50
    }
  }]
}
```

### Example 2: Zone-Based Lighting
```json
{
  "id": "cave_torch",
  "description": "Turn on torch when entering cave",
  "enabled": true,
  "triggers": [{
    "type": "zone",
    "entity": "player",
    "zone": "dark_cave",
    "event": "enter"
  }],
  "conditions": [],
  "actions": [
    {
      "type": "set_value",
      "entity": "lights.torch",
      "value": "on"
    },
    {
      "type": "service_call",
      "service": "audio.play",
      "data": {"sound": "torch_ignite"}
    }
  ]
}
```

### Example 3: Low Health Warning
```json
{
  "id": "health_warning",
  "description": "Warn when health is low",
  "enabled": true,
  "triggers": [{
    "type": "value_change",
    "entity": "player.health",
    "to": "low"
  }],
  "conditions": [{
    "type": "value_check",
    "entity": "player.health",
    "above": 0
  }],
  "actions": [
    {
      "type": "service_call",
      "service": "ui.show_warning",
      "data": {"text": "Health Critical!", "color": "red"}
    },
    {
      "type": "service_call",
      "service": "haptics.pulse",
      "data": {
        "hand": "both",
        "amplitude": 0.8,
        "duration_ms": 200
      }
    }
  ]
}
```

## Weekend Timeline

### Saturday Morning (4 hours)
- Set up Unity project with Meta XR SDK
- Install Newtonsoft.Json
- Create basic data structures (Rule, Trigger, Action, etc.)
- Implement JSON loading and basic validation

### Saturday Afternoon (4 hours)
- Build SimpleRulesEngine MonoBehaviour
- Implement event queue and processing loop
- Create trigger matching logic
- Add condition checking

### Sunday Morning (4 hours)
- Build service registry with UnityEvents
- Implement 3 action types (service_call, wait, set_value)
- Create XR input bridge
- Add simple zone system

### Sunday Afternoon (4 hours)
- Create haptics service
- Add UI service for warnings
- Test with 3 example rules
- Debug and polish

## What We're Skipping (For Now)

- AST and compilation pipeline
- Hot reload
- Complex expressions
- Pattern matching
- Variables and state machines
- Custom editor tools
- Performance optimization
- Memory pools
- Thread safety
- Comprehensive error handling

## Success Criteria

✅ Can load rules from JSON files
✅ Button press triggers haptic feedback
✅ Zone entry triggers lighting change
✅ Health threshold triggers warning
✅ All examples work without code changes
✅ No runtime allocations in main loop
✅ Runs at 72+ FPS on Quest 2

## Next Steps After MVP

Once the weekend prototype works:
1. Add more trigger/action types
2. Implement hot reload for faster iteration
3. Build visual rule editor
4. Add expression support
5. Optimize with routing tables
6. Add comprehensive validation
7. Create LLM training examples

This simplified approach gets you a working rules engine in a weekend that you can iterate on and expand!