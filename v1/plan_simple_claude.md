# Unity Rules Engine - Weekend Hackathon Plan (Final)

## Mission Statement
**Build a working proof-of-concept in 16 hours that demonstrates: LLM writes JSON → Unity reads it → Rules execute → Observable output happens**

Desktop-only, Unity 6.2, structured as a reusable Unity package.

## Project Structure (Unity Package)

```
UnityProject/
├── Assets/
│   └── Samples/           # Demo scene and assets
│       ├── DemoScene.unity
│       ├── Audio/
│       │   ├── beep.wav
│       │   └── warning.wav
│       └── StreamingAssets/
│           └── Rules/
│               ├── click_sound.json
│               └── ...
├── Packages/
│   └── com.yourcompany.rulesengine/
│       ├── package.json
│       ├── Runtime/
│       │   ├── Core/
│       │   │   ├── RuleEngine.cs
│       │   │   ├── EventBus.cs
│       │   │   └── GameState.cs
│       │   ├── Data/
│       │   │   ├── Rule.cs
│       │   │   └── RuleData.cs
│       │   ├── Execution/
│       │   │   ├── ActionExecutor.cs
│       │   │   ├── TriggerMatcher.cs
│       │   │   └── ConditionEvaluator.cs
│       │   ├── Unity/
│       │   │   ├── DesktopInputAdapter.cs
│       │   │   ├── DragDropHandler.cs
│       │   │   └── AudioService.cs
│       │   └── RulesEngine.asmdef
│       └── Editor/
│           ├── RuleDebugWindow.cs
│           └── RulesEngineEditor.asmdef
```

### package.json
```json
{
  "name": "com.yourcompany.rulesengine",
  "version": "0.1.0",
  "displayName": "LLM Rules Engine",
  "description": "Event-driven rules engine for Unity with LLM-friendly JSON",
  "unity": "2022.3",
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.cysharp.unitask": "2.5.0"
  }
}
```

## The Stack (Committed)

- **Unity 6.2** (or 2022.3 LTS minimum)
- **UniTask** (2.5.0) - Async without coroutines
- **Newtonsoft.Json for Unity** (3.2.1) - Robust JSON parsing with error handling

## Schema Design (LLM-Optimized)

### Core Principles
1. **Arrays always** - Even single values use arrays (LLMs handle this better)
2. **Explicit types** - Every object has a `"type"` field
3. **Units in field names** - `duration_ms`, `volume_0_to_1`
4. **No booleans** - Use string enums: `"mode": "single"` not `"mode": true`
5. **Permissive parsing** - Coerce strings to numbers, wrap scalars in arrays

### Rule Structure
```json
{
  "id": "rule_name",
  "mode": "single",
  "triggers": [...],
  "conditions": [...],
  "actions": [...]
}
```

### Minimal Viable Triggers (4 types)
```json
// 1. Event
{"type": "event", "name": "mouse.left.down"}

// 2. Timer
{"type": "timer", "every_ms": 2000}

// 3. Value threshold
{"type": "value", "path": "health", "below": 30}

// 4. Drag and drop
{"type": "dragdrop", "dropzone": "inventory", "tag": "item"}
```

### Minimal Viable Actions (4 types)
```json
// 1. Log
{"type": "log", "message": "Hello ${variable}"}

// 2. Sound
{"type": "audio", "clip": "beep", "volume_0_to_1": 0.5}

// 3. Set value
{"type": "set", "path": "score", "value": "${score + 10}"}

// 4. Wait
{"type": "wait", "duration_ms": 500}
```

## Core Implementation Code

### EventBus.cs
```csharp
using System;
using System.Collections.Generic;
using UniRx;

namespace RulesEngine.Core
{
    public static class EventBus
    {
        private static readonly Subject<GameEvent> stream = new Subject<GameEvent>();
        
        public static IObservable<GameEvent> Stream => stream;
        
        public static void Emit(string name, Dictionary<string, object> data = null)
        {
            stream.OnNext(new GameEvent 
            { 
                name = name, 
                data = data ?? new Dictionary<string, object>(),
                timestamp = Time.time
            });
        }
        
        public static void Dispose()
        {
            stream?.Dispose();
        }
    }
    
    public class GameEvent
    {
        public string name;
        public Dictionary<string, object> data;
        public float timestamp;
    }
}
```

### RuleEngine.cs
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UniRx;

namespace RulesEngine.Core
{
    public class RuleEngine : MonoBehaviour
    {
        private Dictionary<string, Rule> rules = new Dictionary<string, Rule>();
        private Dictionary<string, CancellationTokenSource> activeRules = new Dictionary<string, CancellationTokenSource>();
        private CompositeDisposable disposables = new CompositeDisposable();
        
        [SerializeField] private string rulesPath = "Rules";
        
        async UniTaskVoid Start()
        {
            LoadRules();
            StartTimerTriggers();
            
            // Subscribe to events
            EventBus.Stream
                .Subscribe(OnEvent)
                .AddTo(disposables);
        }
        
        void LoadRules()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, rulesPath);
            if (!Directory.Exists(fullPath)) return;
            
            foreach (string file in Directory.GetFiles(fullPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var rule = ParseRule(json);
                    if (rule != null)
                    {
                        rules[rule.id] = rule;
                        Debug.Log($"Loaded rule: {rule.id}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load {file}: {e.Message}");
                }
            }
        }
        
        Rule ParseRule(string json)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            
            var jObject = JObject.Parse(json);
            var rule = new Rule
            {
                id = jObject["id"]?.ToString() ?? System.Guid.NewGuid().ToString(),
                mode = jObject["mode"]?.ToString() ?? "single",
                triggers = ParseTriggers(jObject["triggers"]),
                conditions = ParseConditions(jObject["conditions"]),
                actions = ParseActions(jObject["actions"])
            };
            
            return rule;
        }
        
        List<JObject> ParseTriggers(JToken token)
        {
            if (token == null) return new List<JObject>();
            if (token.Type != JTokenType.Array)
                return new List<JObject> { token as JObject };
            return token.ToObject<List<JObject>>();
        }
        
        List<JObject> ParseConditions(JToken token)
        {
            if (token == null) return new List<JObject>();
            if (token.Type != JTokenType.Array)
                return new List<JObject> { token as JObject };
            return token.ToObject<List<JObject>>();
        }
        
        List<JObject> ParseActions(JToken token)
        {
            if (token == null) return new List<JObject>();
            if (token.Type != JTokenType.Array)
                return new List<JObject> { token as JObject };
            return token.ToObject<List<JObject>>();
        }
        
        async void OnEvent(GameEvent evt)
        {
            foreach (var rule in rules.Values)
            {
                if (MatchesTrigger(rule, evt))
                {
                    ExecuteRule(rule, evt).Forget();
                }
            }
        }
        
        bool MatchesTrigger(Rule rule, GameEvent evt)
        {
            return rule.triggers.Any(trigger =>
            {
                string type = trigger["type"]?.ToString();
                if (type == "event")
                {
                    return trigger["name"]?.ToString() == evt.name;
                }
                if (type == "dragdrop")
                {
                    return evt.name == "dragdrop" &&
                           evt.data.ContainsKey("dropzone") &&
                           evt.data["dropzone"].ToString() == trigger["dropzone"]?.ToString();
                }
                if (type == "value")
                {
                    string path = trigger["path"]?.ToString();
                    float threshold = trigger["below"]?.ToObject<float>() ?? trigger["above"]?.ToObject<float>() ?? 0;
                    bool isBelow = trigger["below"] != null;
                    float current = GameState.GetFloat(path);
                    return isBelow ? current < threshold : current > threshold;
                }
                return false;
            });
        }
        
        async UniTaskVoid ExecuteRule(Rule rule, GameEvent evt)
        {
            // Handle mode
            if (activeRules.ContainsKey(rule.id))
            {
                if (rule.mode == "restart")
                {
                    activeRules[rule.id].Cancel();
                    activeRules.Remove(rule.id);
                }
                else if (rule.mode == "single")
                {
                    return; // Skip if already running
                }
            }
            
            var cts = new CancellationTokenSource();
            activeRules[rule.id] = cts;
            
            try
            {
                // Check conditions (simplified for hackathon)
                if (!CheckConditions(rule.conditions))
                    return;
                
                // Execute actions
                await ExecuteActions(rule.actions, cts.Token);
            }
            finally
            {
                if (activeRules.ContainsKey(rule.id))
                    activeRules.Remove(rule.id);
                cts?.Dispose();
            }
        }
        
        bool CheckConditions(List<JObject> conditions)
        {
            // Simplified - implement as needed
            return true;
        }
        
        async UniTask ExecuteActions(List<JObject> actions, CancellationToken ct)
        {
            foreach (var action in actions)
            {
                if (ct.IsCancellationRequested) break;
                await ActionExecutor.Execute(action, ct);
            }
        }
        
        void StartTimerTriggers()
        {
            foreach (var rule in rules.Values)
            {
                foreach (var trigger in rule.triggers)
                {
                    if (trigger["type"]?.ToString() == "timer")
                    {
                        int intervalMs = trigger["every_ms"]?.ToObject<int>() ?? 1000;
                        StartTimer(rule.id, intervalMs).Forget();
                    }
                }
            }
        }
        
        async UniTaskVoid StartTimer(string ruleId, int intervalMs)
        {
            while (Application.isPlaying)
            {
                await UniTask.Delay(intervalMs);
                EventBus.Emit($"timer.{ruleId}", null);
            }
        }
        
        void OnDestroy()
        {
            foreach (var cts in activeRules.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            disposables?.Dispose();
        }
    }
    
    public class Rule
    {
        public string id;
        public string mode;
        public List<JObject> triggers;
        public List<JObject> conditions;
        public List<JObject> actions;
    }
}
```

### GameState.cs
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace RulesEngine.Core
{
    public static class GameState
    {
        private static Dictionary<string, object> values = new Dictionary<string, object>();
        
        static GameState()
        {
            // Initialize default values
            Set("health", 100f);
            Set("score", 0);
            Set("ticks", 0);
            Set("time", 0f);
        }
        
        public static void Set(string path, object value)
        {
            values[path] = value;
            
            // Emit value change event for value triggers
            if (value is float || value is int)
            {
                EventBus.Emit($"value.{path}", new Dictionary<string, object> 
                { 
                    { "path", path }, 
                    { "value", value } 
                });
            }
        }
        
        public static T Get<T>(string path, T defaultValue = default)
        {
            if (values.TryGetValue(path, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                
                // Try to convert
                try
                {
                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    Debug.LogWarning($"Could not convert {path} to {typeof(T)}");
                }
            }
            return defaultValue;
        }
        
        public static float GetFloat(string path) => Get<float>(path, 0f);
        public static int GetInt(string path) => Get<int>(path, 0);
        public static string GetString(string path) => Get<string>(path, "");
        
        public static string Interpolate(string template)
        {
            // Simple variable interpolation
            var result = template;
            foreach (var kvp in values)
            {
                result = result.Replace($"${{{kvp.Key}}}", kvp.Value.ToString());
            }
            result = result.Replace("${time}", Time.time.ToString("F1"));
            return result;
        }
    }
}
```

### ActionExecutor.cs
```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RulesEngine.Execution
{
    public static class ActionExecutor
    {
        public static async UniTask Execute(JObject action, CancellationToken ct)
        {
            string type = action["type"]?.ToString();
            
            switch (type)
            {
                case "log":
                    ExecuteLog(action);
                    break;
                    
                case "audio":
                    ExecuteAudio(action);
                    break;
                    
                case "set":
                    ExecuteSet(action);
                    break;
                    
                case "wait":
                    await ExecuteWait(action, ct);
                    break;
                    
                default:
                    Debug.LogWarning($"Unknown action type: {type}");
                    break;
            }
        }
        
        static void ExecuteLog(JObject action)
        {
            string message = action["message"]?.ToString() ?? "No message";
            message = GameState.Interpolate(message);
            Debug.Log($"[Rule] {message}");
        }
        
        static void ExecuteAudio(JObject action)
        {
            string clip = action["clip"]?.ToString() ?? "beep";
            float volume = action["volume_0_to_1"]?.ToObject<float>() ?? 0.5f;
            
            // Find AudioService in scene
            var audioService = Object.FindObjectOfType<AudioService>();
            if (audioService != null)
            {
                audioService.PlayClip(clip, volume);
            }
            else
            {
                Debug.LogWarning("No AudioService found in scene");
            }
        }
        
        static void ExecuteSet(JObject action)
        {
            string path = action["path"]?.ToString();
            var value = action["value"];
            
            if (path == null) return;
            
            // Handle simple expressions
            string valueStr = value?.ToString();
            if (valueStr != null && valueStr.StartsWith("${") && valueStr.EndsWith("}"))
            {
                valueStr = GameState.Interpolate(valueStr);
                
                // Try to parse as expression (simplified)
                if (valueStr.Contains("+"))
                {
                    var parts = valueStr.Split('+');
                    if (parts.Length == 2 && 
                        float.TryParse(parts[0].Trim(), out float a) && 
                        float.TryParse(parts[1].Trim(), out float b))
                    {
                        GameState.Set(path, a + b);
                        return;
                    }
                }
            }
            
            // Direct value
            if (value?.Type == JTokenType.Integer || value?.Type == JTokenType.Float)
            {
                GameState.Set(path, value.ToObject<float>());
            }
            else
            {
                GameState.Set(path, value?.ToString());
            }
        }
        
        static async UniTask ExecuteWait(JObject action, CancellationToken ct)
        {
            int ms = action["duration_ms"]?.ToObject<int>() ?? 1000;
            await UniTask.Delay(ms, cancellationToken: ct);
        }
    }
}
```

### DesktopInputAdapter.cs
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace RulesEngine.Unity
{
    public class DesktopInputAdapter : MonoBehaviour
    {
        private Vector3 lastMousePos;
        private float mouseSpeed;
        
        void Update()
        {
            // Mouse buttons
            if (Input.GetMouseButtonDown(0))
                EventBus.Emit("mouse.left.down");
            if (Input.GetMouseButtonUp(0))
                EventBus.Emit("mouse.left.up");
            if (Input.GetMouseButtonDown(1))
                EventBus.Emit("mouse.right.down");
            
            // Calculate mouse speed
            mouseSpeed = (Input.mousePosition - lastMousePos).magnitude / Time.deltaTime;
            lastMousePos = Input.mousePosition;
            GameState.Set("mouse_speed", mouseSpeed);
            
            // Keyboard
            if (Input.GetKeyDown(KeyCode.Space))
                EventBus.Emit("key.space.down");
            if (Input.GetKeyDown(KeyCode.R))
                EventBus.Emit("key.r.down");
            if (Input.GetKeyDown(KeyCode.F1))
                EventBus.Emit("key.f1.down");
            
            // Update time
            GameState.Set("time", Time.time);
        }
    }
}
```

### DragDropHandler.cs
```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace RulesEngine.Unity
{
    public class DragDropHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string itemTag = "item";
        private Vector3 startPosition;
        private Transform startParent;
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            startPosition = transform.position;
            startParent = transform.parent;
            
            EventBus.Emit("drag.start", new Dictionary<string, object>
            {
                { "item", gameObject.name },
                { "tag", itemTag }
            });
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Input.mousePosition;
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            // Check if dropped on a drop zone
            GameObject dropZone = eventData.pointerCurrentRaycast.gameObject;
            
            if (dropZone != null && dropZone.CompareTag("DropZone"))
            {
                transform.SetParent(dropZone.transform);
                transform.localPosition = Vector3.zero;
                
                EventBus.Emit("dragdrop", new Dictionary<string, object>
                {
                    { "item", gameObject.name },
                    { "tag", itemTag },
                    { "dropzone", dropZone.name }
                });
            }
            else
            {
                // Return to original position
                transform.position = startPosition;
                transform.SetParent(startParent);
                
                EventBus.Emit("drag.cancel", new Dictionary<string, object>
                {
                    { "item", gameObject.name },
                    { "tag", itemTag }
                });
            }
        }
    }
}
```

### AudioService.cs
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace RulesEngine.Unity
{
    public class AudioService : MonoBehaviour
    {
        [System.Serializable]
        public class AudioClipEntry
        {
            public string name;
            public AudioClip clip;
        }
        
        [SerializeField] private AudioClipEntry[] clips;
        [SerializeField] private AudioSource audioSource;
        
        private Dictionary<string, AudioClip> clipDict;
        
        void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            
            clipDict = new Dictionary<string, AudioClip>();
            foreach (var entry in clips)
            {
                clipDict[entry.name] = entry.clip;
            }
        }
        
        public void PlayClip(string clipName, float volume = 1.0f)
        {
            if (clipDict.TryGetValue(clipName, out var clip))
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                Debug.LogWarning($"Audio clip not found: {clipName}");
            }
        }
    }
}
```

## The 6 Demo Rules (Must Work)

### 1. Click → Beep
```json
{
  "id": "click_sound",
  "triggers": [{"type": "event", "name": "mouse.left.down"}],
  "actions": [
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.7},
    {"type": "log", "message": "Click detected at time ${time}"}
  ]
}
```

### 2. Timer → Log
```json
{
  "id": "heartbeat",
  "triggers": [{"type": "timer", "every_ms": 2000}],
  "actions": [
    {"type": "log", "message": "Heartbeat #${ticks}"},
    {"type": "set", "path": "ticks", "value": "${ticks + 1}"}
  ]
}
```

### 3. Threshold → Warning
```json
{
  "id": "low_health",
  "triggers": [{"type": "value", "path": "health", "below": 30}],
  "actions": [
    {"type": "audio", "clip": "warning", "volume_0_to_1": 1.0},
    {"type": "log", "message": "WARNING: Health at ${health}!"}
  ]
}
```

### 4. Sequential Actions
```json
{
  "id": "combo",
  "triggers": [{"type": "event", "name": "key.space.down"}],
  "actions": [
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.5},
    {"type": "wait", "duration_ms": 200},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.7},
    {"type": "wait", "duration_ms": 200},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 1.0},
    {"type": "log", "message": "Combo complete!"}
  ]
}
```

### 5. Mode Test (Restart)
```json
{
  "id": "long_action",
  "mode": "restart",
  "triggers": [{"type": "event", "name": "key.r.down"}],
  "actions": [
    {"type": "log", "message": "Starting 3 second action..."},
    {"type": "wait", "duration_ms": 3000},
    {"type": "log", "message": "Completed!"},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 1.0}
  ]
}
```

### 6. Drag & Drop → Inventory
```json
{
  "id": "item_drop",
  "triggers": [{"type": "dragdrop", "dropzone": "inventory", "tag": "item"}],
  "actions": [
    {"type": "log", "message": "Item dropped in inventory!"},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.5},
    {"type": "set", "path": "inventory_count", "value": "${inventory_count + 1}"},
    {"type": "log", "message": "Inventory now has ${inventory_count} items"}
  ]
}
```

## Implementation Timeline (16 Hours)

### Hour 0-2: Package & Project Setup
**Owner: Person A**
- Create Unity 6.2 project
- Set up package structure in Packages folder
- Install UniTask and Newtonsoft.Json via Package Manager
- Create assembly definitions
- Set up basic folder structure

### Hour 2-4: Core Systems
**Owner: Person B**
- Implement EventBus.cs
- Implement GameState.cs
- Create data classes (Rule, GameEvent)
- Test event emission

### Hour 4-6: Rule Engine Core
**Owner: Person C**
- Implement RuleEngine.cs
- JSON loading with Newtonsoft
- Trigger matching logic
- Basic rule execution flow

### Hour 6-8: Action System
**Owner: Person A**
- Implement ActionExecutor.cs
- Log, Audio, Set, Wait actions
- Variable interpolation in GameState
- Test with simple rules

### Hour 8-10: Unity Integration
**Owner: Person B**
- DesktopInputAdapter.cs
- AudioService.cs  
- DragDropHandler.cs
- Create test scene with UI

### Hour 10-12: Timer & Value Triggers
**Owner: Person C**
- Timer trigger implementation
- Value threshold monitoring
- Mode handling (single/restart)
- Condition evaluation (basic)

### Hour 12-14: Demo Scene Setup
**All Team**
- Create inventory UI with drop zones
- Add draggable items
- Set up audio clips
- Debug text overlay
- Health slider for testing

### Hour 14-16: Testing & Polish
**All Team**
- Test all 6 example rules
- Fix parsing/execution issues
- Record demo video
- Create more LLM examples
- Package documentation

## Test Scene Setup

Create a scene with:
- **Canvas** 
  - Debug text (top-left) showing last 5 events
  - Health slider (top-right) to test thresholds
  - Inventory panel (center) as drop zone
  - Draggable item icons
- **AudioSource** with AudioService component
- **EventSystem** for UI interaction
- **Main Camera**
- **RuleEngine** GameObject with components:
  - RuleEngine
  - DesktopInputAdapter
  - AudioService

## LLM Prompt for Testing

```
Generate a Unity rules engine JSON rule that:
- Triggers when the player drags an item to the inventory
- Checks if inventory isn't full (less than 10 items)  
- Plays a success sound
- Increments the inventory counter
- Shows a log message

Use this schema:
- triggers: array with type "dragdrop", "event", "timer", or "value"
- actions: array with type "log", "audio", "set", or "wait"
- All numeric fields should have units like duration_ms, volume_0_to_1
- Use arrays for all fields that could have multiple values
```

## Quick Decisions Tree

**JSON won't parse?** → Log error with line number, skip rule  
**UniTask not installed?** → Use Package Manager, add com.cysharp.unitask  
**Audio not playing?** → Check AudioService has clips assigned  
**Drag/drop not working?** → Ensure EventSystem in scene  
**Behind schedule?** → Skip conditions, focus on triggers/actions  
**Package errors?** → Check assembly definitions reference UniTask/Newtonsoft  

## Definition of Success

✅ **Must Have:**
- All 6 demo rules execute correctly
- Package structure works in Unity 6.2
- Drag & drop example functional
- No crashes on malformed JSON
- Clean package for reuse

🎯 **Nice to Have:**
- Simple conditions working
- Visual rule state indicators
- Hot reload button
- More complex expressions
- Rule priority system

❌ **NOT Doing This Weekend:**
- VR/Quest support
- Complex expressions
- Manifests/validation
- FSM compilation
- Save/load persistence
- Networking
- Performance optimization beyond basics

## Post-Hackathon Path

If successful:
1. **Next Weekend**: Add conditions, more trigger types, better UI
2. **Week 3**: Pattern triggers, complex actions, hot reload
3. **Week 4**: Move toward VR with Meta XR SDK
4. **Month 2**: Full manifest system, FSM compilation, optimization

---

**Remember: This is a reusable package that proves the concept. Focus on clean code that can be extended, not perfection.**