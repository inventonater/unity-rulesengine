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
│       │   ├── warning.wav
│       │   ├── secret.wav
│       │   ├── charge.wav
│       │   └── tick.wav
│       └── StreamingAssets/
│           └── Rules/
│               ├── click_sound.json
│               ├── heartbeat.json
│               ├── low_health.json
│               ├── combo.json
│               ├── long_action.json
│               ├── item_drop.json
│               ├── konami_code.json
│               ├── drag_charge.json
│               └── double_click.json
├── Packages/
│   └── com.yourcompany.rulesengine/
│       ├── package.json
│       ├── Runtime/
│       │   ├── Core/
│       │   │   ├── RuleEngine.cs
│       │   │   ├── EventBus.cs
│       │   │   ├── GameState.cs
│       │   │   └── PatternTracker.cs
│       │   ├── Data/
│       │   │   ├── Rule.cs
│       │   │   └── RuleData.cs
│       │   ├── Execution/
│       │   │   ├── ActionExecutor.cs
│       │   │   ├── TriggerMatcher.cs
│       │   │   └── ConditionEvaluator.cs
│       │   ├── Unity/
│       │   │   ├── DesktopInputAdapter.cs
│       │   │   ├── EnhancedDragHandler.cs
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
- **UniRx** (for EventBus Subject) - Reactive extensions

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

### Minimal Viable Triggers (5 types)
```json
// 1. Event
{"type": "event", "name": "mouse.left.down"}

// 2. Timer
{"type": "timer", "every_ms": 2000}

// 3. Value threshold
{"type": "value", "path": "health", "below": 30}

// 4. Drag and drop
{"type": "dragdrop", "dropzone": "inventory", "tag": "item"}

// 5. Pattern (sequence of events)
{"type": "pattern", "within_ms": 5000, "sequence": [
  {"event": "key.up.down"},
  {"event": "key.down.down"}
]}
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

## The 9 Demo Rules (All Must Work)

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

### 4. Sequential Actions (Combo)
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

### 7. Konami Code → Secret Mode (Pattern Example)
```json
{
  "id": "konami_code",
  "mode": "single",
  "triggers": [{
    "type": "pattern",
    "within_ms": 5000,
    "sequence": [
      {"event": "key.up.down"},
      {"event": "key.up.down"},
      {"event": "key.down.down"},
      {"event": "key.down.down"},
      {"event": "key.left.down"},
      {"event": "key.right.down"},
      {"event": "key.left.down"},
      {"event": "key.right.down"},
      {"event": "key.b.down"},
      {"event": "key.a.down"}
    ]
  }],
  "conditions": [],
  "actions": [
    {"type": "log", "message": "⬆⬆⬇⬇⬅➡⬅➡BA - KONAMI CODE ACTIVATED!"},
    {"type": "audio", "clip": "secret", "volume_0_to_1": 1.0},
    {"type": "set", "path": "godmode", "value": "true"},
    {"type": "set", "path": "lives", "value": "30"},
    {"type": "wait", "duration_ms": 100},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.3},
    {"type": "wait", "duration_ms": 100},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.6},
    {"type": "wait", "duration_ms": 100},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 1.0},
    {"type": "log", "message": "God mode enabled! Lives set to 30!"}
  ]
}
```

### 8. Long Drag → Power Charge
```json
{
  "id": "drag_charge",
  "triggers": [{"type": "event", "name": "drag.active"}],
  "conditions": [{"type": "value", "path": "drag.duration_ms", "above": 1000}],
  "actions": [
    {"type": "audio", "clip": "charge", "volume_0_to_1": 0.8},
    {"type": "log", "message": "Power charging! Distance: ${drag.delta}"},
    {"type": "set", "path": "charge_power", "value": "${drag.duration_ms / 1000}"}
  ]
}
```

### 9. Double-Click Pattern (Simple Pattern)
```json
{
  "id": "double_click",
  "triggers": [{
    "type": "pattern",
    "within_ms": 500,
    "sequence": [
      {"event": "mouse.left.down"},
      {"event": "mouse.left.up"},
      {"event": "mouse.left.down"},
      {"event": "mouse.left.up"}
    ]
  }],
  "actions": [
    {"type": "log", "message": "Double-click detected!"},
    {"type": "audio", "clip": "beep", "volume_0_to_1": 0.5},
    {"type": "set", "path": "double_clicks", "value": "${double_clicks + 1}"}
  ]
}
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

### RuleEngine.cs (With Pattern Support)
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
        private PatternTracker patternTracker;
        
        [SerializeField] private string rulesPath = "Rules";
        
        public IEnumerable<Rule> GetRules() => rules.Values;
        
        async UniTaskVoid Start()
        {
            // Add PatternTracker component if not present
            patternTracker = GetComponent<PatternTracker>();
            if (patternTracker == null)
                patternTracker = gameObject.AddComponent<PatternTracker>();
            
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
                        
                        // Convert pattern triggers to event triggers
                        ConvertPatternTriggers(rule);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load {file}: {e.Message}");
                }
            }
        }
        
        void ConvertPatternTriggers(Rule rule)
        {
            // Replace pattern triggers with event triggers that fire when pattern completes
            for (int i = 0; i < rule.triggers.Count; i++)
            {
                var trigger = rule.triggers[i];
                if (trigger["type"]?.ToString() == "pattern")
                {
                    // Store original pattern trigger for reference
                    rule.originalPatternTriggers.Add(trigger);
                    
                    // Replace with event trigger that listens for pattern completion
                    var newTrigger = new JObject();
                    newTrigger["type"] = "event";
                    newTrigger["name"] = $"pattern_complete_{rule.id}";
                    rule.triggers[i] = newTrigger;
                    
                    Debug.Log($"Converted pattern trigger for rule {rule.id}");
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
                actions = ParseActions(jObject["actions"]),
                originalPatternTriggers = new List<JObject>()
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
                // Pattern triggers are converted to event triggers at load time
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
                // Check conditions
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
            if (conditions == null || conditions.Count == 0)
                return true;
            
            foreach (var condition in conditions)
            {
                string type = condition["type"]?.ToString();
                
                if (type == "value")
                {
                    string path = condition["path"]?.ToString();
                    float threshold = condition["below"]?.ToObject<float>() ?? condition["above"]?.ToObject<float>() ?? 0;
                    bool isBelow = condition["below"] != null;
                    float current = GameState.GetFloat(path);
                    bool passes = isBelow ? current < threshold : current > threshold;
                    if (!passes) return false;
                }
                // Add more condition types as needed
            }
            
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
        public List<JObject> originalPatternTriggers; // Keep original for reference
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
            Set("inventory_count", 0);
            Set("double_clicks", 0);
            Set("godmode", "false");
            Set("lives", 3);
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
            
            // Keyboard - Basic keys
            if (Input.GetKeyDown(KeyCode.Space))
                EventBus.Emit("key.space.down");
            if (Input.GetKeyDown(KeyCode.R))
                EventBus.Emit("key.r.down");
            if (Input.GetKeyDown(KeyCode.F1))
                EventBus.Emit("key.f1.down");
            
            // Keyboard - Konami Code keys
            if (Input.GetKeyDown(KeyCode.UpArrow))
                EventBus.Emit("key.up.down");
            if (Input.GetKeyDown(KeyCode.DownArrow))
                EventBus.Emit("key.down.down");
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                EventBus.Emit("key.left.down");
            if (Input.GetKeyDown(KeyCode.RightArrow))
                EventBus.Emit("key.right.down");
            if (Input.GetKeyDown(KeyCode.A))
                EventBus.Emit("key.a.down");
            if (Input.GetKeyDown(KeyCode.B))
                EventBus.Emit("key.b.down");
            
            // Update time
            GameState.Set("time", Time.time);
        }
    }
}
```

### EnhancedDragHandler.cs
```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace RulesEngine.Unity
{
    public class EnhancedDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string itemTag = "item";
        [SerializeField] private bool playDragAudio = true;
        [SerializeField] private float audioTickInterval = 0.3f; // Seconds between audio ticks
        
        private Vector3 startPosition;
        private Transform startParent;
        private float dragStartTime;
        private Vector3 lastDragPosition;
        private float totalDragDistance;
        private CancellationTokenSource dragCts;
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            startPosition = transform.position;
            startParent = transform.parent;
            dragStartTime = Time.time;
            lastDragPosition = Input.mousePosition;
            totalDragDistance = 0;
            
            // Start drag active monitoring
            dragCts = new CancellationTokenSource();
            if (playDragAudio)
                PlayDragAudio(dragCts.Token).Forget();
            
            EventBus.Emit("drag.start", new Dictionary<string, object>
            {
                { "item", gameObject.name },
                { "tag", itemTag },
                { "start_pos", startPosition }
            });
            
            // Set initial drag state
            GameState.Set("drag.active", true);
            GameState.Set("drag.item", gameObject.name);
            GameState.Set("drag.duration_ms", 0f);
            GameState.Set("drag.delta", 0f);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Input.mousePosition;
            
            // Calculate delta
            float frameDelta = Vector3.Distance(Input.mousePosition, lastDragPosition);
            totalDragDistance += frameDelta;
            lastDragPosition = Input.mousePosition;
            
            // Update drag state
            float duration = (Time.time - dragStartTime) * 1000; // Convert to ms
            GameState.Set("drag.duration_ms", duration);
            GameState.Set("drag.delta", totalDragDistance);
            GameState.Set("drag.current_pos", Input.mousePosition);
            
            // Emit periodic drag active events
            EventBus.Emit("drag.active", new Dictionary<string, object>
            {
                { "item", gameObject.name },
                { "duration_ms", duration },
                { "delta", totalDragDistance },
                { "speed", frameDelta / Time.deltaTime }
            });
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            // Cancel audio
            dragCts?.Cancel();
            dragCts?.Dispose();
            
            float finalDuration = (Time.time - dragStartTime) * 1000;
            
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
                    { "dropzone", dropZone.name },
                    { "duration_ms", finalDuration },
                    { "delta", totalDragDistance }
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
                    { "tag", itemTag },
                    { "duration_ms", finalDuration },
                    { "delta", totalDragDistance }
                });
            }
            
            // Clear drag state
            GameState.Set("drag.active", false);
            GameState.Set("drag.item", "");
            GameState.Set("drag.duration_ms", 0f);
            GameState.Set("drag.delta", 0f);
        }
        
        private async UniTaskVoid PlayDragAudio(CancellationToken ct)
        {
            await UniTask.Delay(100, cancellationToken: ct); // Small initial delay
            
            while (!ct.IsCancellationRequested)
            {
                // Play tick sound
                EventBus.Emit("drag.audio.tick", new Dictionary<string, object>
                {
                    { "item", gameObject.name },
                    { "duration_ms", (Time.time - dragStartTime) * 1000 }
                });
                
                // Could directly play audio here if you have reference to AudioService
                var audioService = FindObjectOfType<AudioService>();
                if (audioService != null)
                {
                    float volume = Mathf.Clamp01(totalDragDistance / 500f); // Volume based on distance
                    audioService.PlayClip("tick", volume);
                }
                
                await UniTask.Delay((int)(audioTickInterval * 1000), cancellationToken: ct);
            }
        }
    }
}
```

### PatternTracker.cs (Data-Driven)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using Newtonsoft.Json.Linq;

namespace RulesEngine.Core
{
    // Simplified pattern matching - tracks sequences and fires events when complete
    public class PatternTracker : MonoBehaviour
    {
        private class ActivePattern
        {
            public string ruleId;
            public List<string> expectedSequence;
            public List<string> collectedEvents = new List<string>();
            public float startTime;
            public float timeoutMs;
        }
        
        private List<ActivePattern> activePatterns = new List<ActivePattern>();
        private IDisposable eventSubscription;
        
        void Start()
        {
            // Subscribe to all events
            eventSubscription = EventBus.Stream.Subscribe(OnEvent);
        }
        
        private void OnEvent(GameEvent evt)
        {
            // Check all rules for pattern triggers that could start with this event
            var engine = GetComponent<RuleEngine>();
            if (engine == null) return;
            
            foreach (var rule in engine.GetRules())
            {
                foreach (var originalTrigger in rule.originalPatternTriggers)
                {
                    var sequence = originalTrigger["sequence"]?.ToObject<List<JObject>>();
                    if (sequence != null && sequence.Count > 0)
                    {
                        string firstEvent = sequence[0]["event"]?.ToString();
                        
                        // If this event matches the first in sequence, start tracking
                        if (firstEvent == evt.name)
                        {
                            var pattern = new ActivePattern
                            {
                                ruleId = rule.id,
                                expectedSequence = sequence.Select(s => s["event"]?.ToString()).ToList(),
                                startTime = Time.time,
                                timeoutMs = originalTrigger["within_ms"]?.ToObject<float>() ?? 5000
                            };
                            pattern.collectedEvents.Add(evt.name);
                            activePatterns.Add(pattern);
                            
                            Debug.Log($"Started tracking pattern for rule {rule.id}");
                        }
                    }
                }
            }
            
            // Check active patterns
            var toRemove = new List<ActivePattern>();
            
            foreach (var pattern in activePatterns.ToList())
            {
                // Skip the pattern we just started
                if (pattern.collectedEvents.Count == 1 && pattern.collectedEvents[0] == evt.name)
                    continue;
                
                // Check timeout
                if ((Time.time - pattern.startTime) * 1000 > pattern.timeoutMs)
                {
                    toRemove.Add(pattern);
                    Debug.Log($"Pattern {pattern.ruleId} timed out");
                    continue;
                }
                
                // Check if this event is next in sequence
                int nextIndex = pattern.collectedEvents.Count;
                if (nextIndex < pattern.expectedSequence.Count)
                {
                    if (pattern.expectedSequence[nextIndex] == evt.name)
                    {
                        pattern.collectedEvents.Add(evt.name);
                        Debug.Log($"Pattern {pattern.ruleId} progress: {pattern.collectedEvents.Count}/{pattern.expectedSequence.Count}");
                        
                        // Check if complete
                        if (pattern.collectedEvents.Count == pattern.expectedSequence.Count)
                        {
                            Debug.Log($"Pattern {pattern.ruleId} COMPLETE!");
                            
                            // Fire a regular event that the rule can catch
                            EventBus.Emit($"pattern_complete_{pattern.ruleId}", new Dictionary<string, object>
                            {
                                { "pattern_id", pattern.ruleId },
                                { "duration_ms", (Time.time - pattern.startTime) * 1000 }
                            });
                            
                            toRemove.Add(pattern);
                        }
                    }
                    else
                    {
                        // Wrong event, cancel this pattern attempt
                        toRemove.Add(pattern);
                        Debug.Log($"Pattern {pattern.ruleId} broken by {evt.name}");
                    }
                }
            }
            
            // Clean up completed/failed patterns
            foreach (var pattern in toRemove)
            {
                activePatterns.Remove(pattern);
            }
        }
        
        void OnDestroy()
        {
            eventSubscription?.Dispose();
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
- Implement RuleEngine.cs (basic version)
- JSON loading with Newtonsoft
- Trigger matching logic
- Basic rule execution flow

### Hour 6-8: Action System
**Owner: Person A**
- Implement ActionExecutor.cs
- Log, Audio, Set, Wait actions
- Variable interpolation in GameState
- Test with simple rules (1-3)

### Hour 8-10: Unity Integration
**Owner: Person B**
- DesktopInputAdapter.cs with Konami keys
- AudioService.cs  
- EnhancedDragHandler.cs
- Create test scene with UI

### Hour 10-12: Pattern & Advanced Triggers
**Owner: Person C**
- PatternTracker.cs implementation
- Timer trigger implementation
- Value threshold monitoring
- Mode handling (single/restart)
- Test Konami code (Rule 7)

### Hour 12-14: Demo Scene Setup
**All Team**
- Create inventory UI with drop zones
- Add draggable items
- Set up all audio clips
- Debug text overlay showing events
- Health slider for testing

### Hour 14-16: Testing & Polish
**All Team**
- Test all 9 example rules work
- Verify Konami code fires correctly
- Fix parsing/execution issues
- Record demo video
- Create more LLM examples
- Package documentation

## Test Scene Setup

Create a scene with:
- **Canvas** 
  - Debug text (top-left) showing last 10 events/actions
  - Health slider (top-right) to test thresholds
  - Inventory panel (center) as drop zone
  - Draggable item icons
  - Live values display (godmode, lives, etc.)
- **AudioSource** with AudioService component
- **EventSystem** for UI interaction
- **Main Camera**
- **RuleEngine** GameObject with components:
  - RuleEngine
  - PatternTracker
  - DesktopInputAdapter
  - AudioService

## The Two-Minute Demo Script

1. "Here's our rules engine running in Unity"
2. "An LLM wrote this JSON rule" (show JSON)
3. "When I click..." (click) "...you hear the sound"
4. "This rule fires every 2 seconds" (show timer)
5. "This one triggers on low health" (adjust slider)
6. "Watch this - the Konami code!" (enter ⬆⬆⬇⬇⬅➡⬅➡BA)
7. "The LLM generated this 10-event pattern from a simple prompt"
8. "It can track drag gestures too" (demonstrate enhanced drag)
9. "It works with slightly broken JSON too" (show coercion)
10. "Next steps: VR support and complex conditions"

## LLM Prompt for Testing

### Basic Rule Generation
```
Generate a Unity rules engine JSON rule that:
- Triggers when the player drags an item to the inventory
- Checks if inventory isn't full (less than 10 items)  
- Plays a success sound
- Increments the inventory counter
- Shows a log message

Use this schema:
- triggers: array with type "dragdrop", "event", "timer", "value", or "pattern"
- For patterns, use {"type": "pattern", "within_ms": number, "sequence": [{"event": "name"}...]}
- actions: array with type "log", "audio", "set", or "wait"
- All numeric fields should have units like duration_ms, volume_0_to_1
- Use arrays for all fields that could have multiple values
```

### Pattern Generation Testing
```
Generate a pattern trigger for a "hadouken" fighting game move:
- Down, down-forward, forward, punch
- Must be entered within 1 second
- When triggered, play a special sound and log "Hadouken!"

Generate a pattern trigger for the Konami code:
- The sequence is: up, up, down, down, left, right, left, right, B, A
- Must be completed within 5 seconds
- Should activate god mode and give 30 lives
```

## Why the Pattern System Matters

The Konami code and pattern matching serve as an excellent stress test for your hackathon:

**Technical Validation:**
- Tests event ordering and timing precision
- Validates that the event bus handles rapid sequences
- Proves the engine can track complex state without hardcoding
- Shows the system can handle concurrent pattern attempts

**LLM Compatibility:**
- Patterns are fully expressible in JSON (no code needed)
- LLMs understand sequences naturally
- Easy to generate variations (fighting game combos, gestures, etc.)
- Tests the engine's tolerance for LLM-generated complexity

**Demo Impact:**
- Everyone recognizes the Konami code
- Visceral "wow" moment when it works
- Shows sophistication beyond simple triggers
- Easy to explain to non-technical stakeholders

## Quick Decisions Tree

**JSON won't parse?** → Log error with line number, skip rule  
**UniTask not installed?** → Use Package Manager, add com.cysharp.unitask  
**Pattern not working?** → Check PatternTracker is on same GameObject as RuleEngine  
**Audio not playing?** → Check AudioService has clips assigned  
**Drag/drop not working?** → Ensure EventSystem in scene  
**Behind schedule?** → Skip conditions, focus on triggers/actions  
**Package errors?** → Check assembly definitions reference UniTask/Newtonsoft  

## Definition of Success

✅ **Must Have:**
- All 9 demo rules execute correctly (especially Konami code!)
- Package structure works in Unity 6.2
- Drag & drop example functional
- Pattern matching works with LLM-generated sequences
- No crashes on malformed JSON
- Clean package for reuse

🎯 **Nice to Have:**
- More complex conditions working
- Visual rule state indicators
- Hot reload button
- More pattern examples (fighting combos)
- Rule priority system

❌ **NOT Doing This Weekend:**
- VR/Quest support
- Complex expressions beyond ${var + num}
- Manifests/validation
- FSM compilation
- Save/load persistence
- Networking
- Performance optimization beyond basics

## Post-Hackathon Path

If successful:
1. **Next Weekend**: Add more conditions, better UI, hot reload
2. **Week 3**: Complex actions, more pattern types
3. **Week 4**: Move toward VR with Meta XR SDK
4. **Month 2**: Full manifest system, FSM compilation, optimization

---

**Remember: The Konami code working proves your entire architecture. If an LLM can generate a 10-event sequence that executes perfectly, you've validated the core premise.**