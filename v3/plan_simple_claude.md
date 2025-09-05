# Unity Rules Engine - Weekend Hackathon Plan

## Mission Statement
**Build a working proof-of-concept in 16 hours that demonstrates: LLM writes JSON → Unity reads it → Rules execute → Observable output happens**

Desktop-only, Unity 6.2, structured as a reusable Unity Package Manager (UPM) package.

## Project Structure (Unity Package)

```
UnityProject/
├── Assets/
│   └── (Host project assets)
├── Packages/
│   └── com.yourcompany.rulesengine/
│       ├── package.json
│       ├── README.md
│       ├── CHANGELOG.md
│       ├── LICENSE.md
│       ├── Runtime/
│       │   ├── RulesEngine.Runtime.asmdef
│       │   ├── Core/
│       │   │   ├── RuleEngine.cs
│       │   │   ├── EventBus.cs
│       │   │   ├── GameState.cs
│       │   │   └── PatternSequenceWatcher.cs
│       │   ├── Data/
│       │   │   ├── DTOs/
│       │   │   │   ├── RuleDto.cs
│       │   │   │   ├── TriggerDto.cs
│       │   │   │   ├── ConditionDto.cs
│       │   │   │   └── ActionDto.cs
│       │   │   └── Runtime/
│       │   │       ├── Rule.cs
│       │   │       └── RuleConverter.cs
│       │   ├── Parsing/
│       │   │   ├── RuleParser.cs
│       │   │   ├── RuleCoercion.cs
│       │   │   └── RuleRepository.cs
│       │   ├── Execution/
│       │   │   ├── ActionExecutor.cs
│       │   │   ├── TriggerMatcher.cs
│       │   │   ├── ConditionEvaluator.cs
│       │   │   └── TimerService.cs
│       │   └── Unity/
│       │       ├── DesktopInputAdapter.cs
│       │       ├── EnhancedDragHandler.cs
│       │       ├── AudioService.cs
│       │       └── DevPanel.cs
│       ├── Editor/
│       │   ├── RulesEngine.Editor.asmdef
│       │   └── RuleDebugWindow.cs
│       └── Samples~/      # Note the tilde - won't auto-import
│           └── Demo/
│               ├── Demo.unity
│               ├── Audio/
│               │   ├── beep.wav
│               │   ├── warning.wav
│               │   ├── secret.wav
│               │   ├── charge.wav
│               │   └── tick.wav
│               └── Rules/
│                   ├── 01_click_sound.json
│                   ├── 02_heartbeat.json
│                   ├── 03_low_health.json
│                   ├── 04_combo.json
│                   ├── 05_long_action.json
│                   ├── 06_item_drop.json
│                   ├── 07_konami_code.json
│                   ├── 08_drag_charge.json
│                   └── 09_double_click.json
```

## Package Configuration

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
  },
  "samples": [
    {
      "displayName": "Demo Scene & Rules",
      "description": "Complete demo with 9 example rules including patterns",
      "path": "Samples~/Demo"
    }
  ]
}
```

### RulesEngine.Runtime.asmdef
```json
{
  "name": "RulesEngine.Runtime",
  "references": [
    "UniTask",
    "Newtonsoft.Json"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": []
}
```

### RulesEngine.Editor.asmdef
```json
{
  "name": "RulesEngine.Editor",
  "references": [
    "RulesEngine.Runtime"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": []
}
```

## The Stack (Committed)

- **Unity 6.2** (or 2022.3 LTS minimum)
- **UniTask** (2.5.0) - Async without coroutines
- **Newtonsoft.Json for Unity** (3.2.1) - Robust JSON parsing with error handling
- **No UniRx dependency** - Using lightweight custom EventBus

## Schema Design (LLM-Optimized)

### Core Principles
1. **Arrays always** - Even single values use arrays
2. **Explicit types** - Every object has a `"type"` field
3. **Units in field names** - `duration_ms`, `volume_0_to_1`
4. **String enums** - `"mode": "single"` not `"mode": true`
5. **Permissive parsing** - Coerce strings to numbers, wrap scalars

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

### Triggers (5 types)
```json
// 1. Event
{"type": "event", "name": "mouse.left.down"}

// 2. Timer
{"type": "timer", "every_ms": 2000}

// 3. Value threshold
{"type": "value", "path": "health", "below": 30}

// 4. Drag and drop
{"type": "dragdrop", "dropzone": "inventory", "tag": "item"}

// 5. Pattern sequence
{
  "type": "pattern_sequence",
  "within_ms_10_to_5000": 1500,
  "sequence": [
    {"name": "key.up.down"},
    {"name": "key.down.down"}
  ]
}
```

### Conditions (2 types)
```json
// 1. State equals
{"type": "state_equals", "entity": ["ui.mode"], "equals": ["debug"]}

// 2. Numeric compare
{"type": "numeric_compare", "entity": ["health"], "above": 30}
```

### Actions (4 types)
```json
// 1. Service call
{
  "type": "service_call",
  "service": "audio.play",
  "data": {"clip": "beep", "volume_0_to_1": 0.5}
}

// 2. Wait
{"type": "wait_duration", "duration_ms_0_to_60000": 1000}

// 3. Repeat
{
  "type": "repeat_count",
  "count_1_to_20": 3,
  "actions": [...]
}

// 4. Set value
{"type": "set", "path": "score", "value": "${score + 10}"}
```

## Core Implementation Code

### DTOs for Clean Parsing

#### RuleDto.cs
```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RulesEngine.Data.DTOs
{
    public class RuleDto
    {
        public string id { get; set; }
        public string mode { get; set; } = "single";
        public List<TriggerDto> triggers { get; set; } = new List<TriggerDto>();
        public List<ConditionDto> conditions { get; set; } = new List<ConditionDto>();
        public List<ActionDto> actions { get; set; } = new List<ActionDto>();
    }
    
    public class TriggerDto
    {
        public string type { get; set; }
        
        // Event trigger
        public string name { get; set; }
        
        // Timer trigger
        public int? every_ms { get; set; }
        
        // Value trigger
        public string path { get; set; }
        public float? above { get; set; }
        public float? below { get; set; }
        
        // Dragdrop trigger
        public string dropzone { get; set; }
        public string tag { get; set; }
        
        // Pattern sequence trigger
        public int? within_ms_10_to_5000 { get; set; }
        public List<PatternStep> sequence { get; set; }
    }
    
    public class PatternStep
    {
        public string name { get; set; }
    }
    
    public class ConditionDto
    {
        public string type { get; set; }
        public List<string> entity { get; set; }
        public List<string> equals { get; set; }
        public float? above { get; set; }
        public float? below { get; set; }
    }
    
    public class ActionDto
    {
        public string type { get; set; }
        
        // Service call
        public string service { get; set; }
        public Dictionary<string, object> data { get; set; }
        
        // Wait
        public int? duration_ms_0_to_60000 { get; set; }
        
        // Repeat
        public int? count_1_to_20 { get; set; }
        public List<ActionDto> actions { get; set; }
        
        // Set
        public string path { get; set; }
        public object value { get; set; }
    }
}
```

### Simplified EventBus (No UniRx)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RulesEngine.Core
{
    public static class EventBus
    {
        private static Channel<GameEvent> channel = Channel.CreateUnbounded<GameEvent>();
        
        public static void Emit(string name, Dictionary<string, object> data = null)
        {
            var evt = new GameEvent 
            { 
                name = name, 
                data = data ?? new Dictionary<string, object>(),
                timestamp = Time.time
            };
            
            if (!channel.Writer.TryWrite(evt))
            {
                Debug.LogWarning($"Failed to emit event: {name}");
            }
        }
        
        public static ChannelReader<GameEvent> GetReader() => channel.Reader;
        
        public static void Reset()
        {
            channel = Channel.CreateUnbounded<GameEvent>();
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

### Simplified PatternSequenceWatcher

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RulesEngine.Core
{
    public class PatternSequenceWatcher
    {
        private readonly string[] sequence;
        private readonly float withinSeconds;
        private int currentIndex = 0;
        private float windowStart = -1f;
        
        public string RuleId { get; }
        
        public PatternSequenceWatcher(string ruleId, IEnumerable<string> eventNames, int withinMs)
        {
            RuleId = ruleId;
            sequence = eventNames.ToArray();
            withinSeconds = Mathf.Clamp(withinMs, 10, 5000) / 1000f;
        }
        
        public bool OnEvent(string eventName, float timestamp)
        {
            // Reset if window expired
            if (windowStart >= 0 && (timestamp - windowStart) > withinSeconds)
            {
                Reset();
            }
            
            // Check if this event matches the expected next event
            if (eventName == sequence[currentIndex])
            {
                // Start window on first match
                if (currentIndex == 0)
                {
                    windowStart = timestamp;
                }
                
                currentIndex++;
                
                // Check if sequence complete
                if (currentIndex >= sequence.Length)
                {
                    Reset();
                    return true; // Pattern complete!
                }
            }
            else if (currentIndex > 0)
            {
                // Wrong event in middle of pattern - reset
                Reset();
                
                // But check if this could start a new pattern
                if (eventName == sequence[0])
                {
                    currentIndex = 1;
                    windowStart = timestamp;
                }
            }
            
            return false;
        }
        
        private void Reset()
        {
            currentIndex = 0;
            windowStart = -1f;
        }
    }
}
```

### RuleParser with DTO Conversion

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using RulesEngine.Data.DTOs;
using RulesEngine.Data.Runtime;

namespace RulesEngine.Parsing
{
    public class RuleParser
    {
        private readonly JsonSerializerSettings settings;
        
        public RuleParser()
        {
            settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Error = (sender, args) => 
                {
                    Debug.LogWarning($"JSON parsing warning: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                }
            };
        }
        
        public Rule ParseRule(string json)
        {
            try
            {
                // Parse to DTO first
                var dto = JsonConvert.DeserializeObject<RuleDto>(json, settings);
                if (dto == null) return null;
                
                // Apply coercions
                dto = ApplyCoercions(dto);
                
                // Convert DTO to runtime Rule
                return ConvertToRule(dto);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse rule: {e.Message}");
                return null;
            }
        }
        
        private RuleDto ApplyCoercions(RuleDto dto)
        {
            // Auto-generate ID if missing
            if (string.IsNullOrEmpty(dto.id))
                dto.id = $"rule_{Guid.NewGuid():N}";
            
            // Ensure arrays exist
            dto.triggers = dto.triggers ?? new List<TriggerDto>();
            dto.conditions = dto.conditions ?? new List<ConditionDto>();
            dto.actions = dto.actions ?? new List<ActionDto>();
            
            // Apply trigger coercions
            foreach (var trigger in dto.triggers)
            {
                // Clamp pattern window
                if (trigger.within_ms_10_to_5000.HasValue)
                {
                    trigger.within_ms_10_to_5000 = Mathf.Clamp(
                        trigger.within_ms_10_to_5000.Value, 10, 5000);
                }
            }
            
            return dto;
        }
        
        private Rule ConvertToRule(RuleDto dto)
        {
            return new Rule
            {
                Id = dto.id,
                Mode = ParseMode(dto.mode),
                RawDto = dto // Keep DTO for reference
            };
        }
        
        private RuleMode ParseMode(string mode)
        {
            return mode?.ToLower() switch
            {
                "restart" => RuleMode.Restart,
                _ => RuleMode.Single
            };
        }
    }
}
```

### Runtime Rule Types

```csharp
namespace RulesEngine.Data.Runtime
{
    public class Rule
    {
        public string Id { get; set; }
        public RuleMode Mode { get; set; }
        public DTOs.RuleDto RawDto { get; set; } // Keep DTO for easy access
    }
    
    public enum RuleMode
    {
        Single,
        Restart
    }
}
```

### RuleEngine with Pattern Support

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RulesEngine.Data.Runtime;
using RulesEngine.Parsing;

namespace RulesEngine.Core
{
    public class RuleEngine : MonoBehaviour
    {
        [SerializeField] private string rulesPath = "Rules";
        
        private Dictionary<string, Rule> rules = new Dictionary<string, Rule>();
        private Dictionary<string, CancellationTokenSource> activeRules = new Dictionary<string, CancellationTokenSource>();
        private List<PatternSequenceWatcher> patternWatchers = new List<PatternSequenceWatcher>();
        private RuleParser parser;
        private CancellationTokenSource engineCts;
        
        void Awake()
        {
            parser = new RuleParser();
        }
        
        void Start()
        {
            LoadRules();
            StartEventLoop().Forget();
            StartTimerTriggers();
        }
        
        void LoadRules()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, rulesPath);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"Rules directory not found: {fullPath}");
                return;
            }
            
            foreach (string file in Directory.GetFiles(fullPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var rule = parser.ParseRule(json);
                    
                    if (rule != null)
                    {
                        rules[rule.Id] = rule;
                        RegisterPatternWatchers(rule);
                        Debug.Log($"Loaded rule: {rule.Id}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load {file}: {e.Message}");
                }
            }
            
            Debug.Log($"Loaded {rules.Count} rules, {patternWatchers.Count} pattern watchers");
        }
        
        void RegisterPatternWatchers(Rule rule)
        {
            foreach (var trigger in rule.RawDto.triggers)
            {
                if (trigger.type == "pattern_sequence" && trigger.sequence != null)
                {
                    var eventNames = trigger.sequence.ConvertAll(s => s.name);
                    var watcher = new PatternSequenceWatcher(
                        rule.Id,
                        eventNames,
                        trigger.within_ms_10_to_5000 ?? 1500
                    );
                    patternWatchers.Add(watcher);
                    Debug.Log($"Registered pattern watcher for rule {rule.Id} with {eventNames.Count} events");
                }
            }
        }
        
        async UniTaskVoid StartEventLoop()
        {
            engineCts = new CancellationTokenSource();
            var reader = EventBus.GetReader();
            
            while (!engineCts.Token.IsCancellationRequested)
            {
                try
                {
                    await foreach (var evt in reader.ReadAllAsync(engineCts.Token))
                    {
                        ProcessEvent(evt);
                    }
                }
                catch (System.Exception e)
                {
                    if (!engineCts.Token.IsCancellationRequested)
                        Debug.LogError($"Event loop error: {e}");
                }
            }
        }
        
        void ProcessEvent(GameEvent evt)
        {
            // Check pattern watchers
            foreach (var watcher in patternWatchers)
            {
                if (watcher.OnEvent(evt.name, evt.timestamp))
                {
                    Debug.Log($"Pattern complete for rule: {watcher.RuleId}");
                    
                    // Fire pattern complete event
                    EventBus.Emit($"pattern_complete_{watcher.RuleId}", 
                        new Dictionary<string, object> { { "rule_id", watcher.RuleId } });
                }
            }
            
            // Check regular triggers
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
            foreach (var trigger in rule.RawDto.triggers)
            {
                switch (trigger.type)
                {
                    case "event":
                        if (trigger.name == evt.name)
                            return true;
                        break;
                        
                    case "pattern_sequence":
                        // Pattern completions come as special events
                        if (evt.name == $"pattern_complete_{rule.Id}")
                            return true;
                        break;
                        
                    case "dragdrop":
                        if (evt.name == "dragdrop" &&
                            evt.data.ContainsKey("dropzone") &&
                            evt.data["dropzone"].ToString() == trigger.dropzone)
                            return true;
                        break;
                        
                    case "value":
                        if (evt.name.StartsWith("value.") && 
                            evt.name == $"value.{trigger.path}")
                        {
                            if (evt.data.TryGetValue("value", out var value))
                            {
                                float v = System.Convert.ToSingle(value);
                                if (trigger.below.HasValue && v < trigger.below.Value)
                                    return true;
                                if (trigger.above.HasValue && v > trigger.above.Value)
                                    return true;
                            }
                        }
                        break;
                }
            }
            
            return false;
        }
        
        async UniTaskVoid ExecuteRule(Rule rule, GameEvent evt)
        {
            // Handle mode
            if (activeRules.ContainsKey(rule.Id))
            {
                if (rule.Mode == RuleMode.Restart)
                {
                    Debug.Log($"Restarting rule: {rule.Id}");
                    activeRules[rule.Id].Cancel();
                    activeRules.Remove(rule.Id);
                }
                else
                {
                    Debug.Log($"Rule already running (single mode): {rule.Id}");
                    return;
                }
            }
            
            var cts = new CancellationTokenSource();
            activeRules[rule.Id] = cts;
            
            try
            {
                Debug.Log($"Executing rule: {rule.Id}");
                
                // Check conditions
                if (!CheckConditions(rule))
                {
                    Debug.Log($"Conditions not met for rule: {rule.Id}");
                    return;
                }
                
                // Execute actions
                await ActionExecutor.Execute(rule.RawDto.actions, cts.Token);
                
                Debug.Log($"Rule completed: {rule.Id}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log($"Rule cancelled: {rule.Id}");
            }
            finally
            {
                if (activeRules.ContainsKey(rule.Id))
                    activeRules.Remove(rule.Id);
                cts?.Dispose();
            }
        }
        
        bool CheckConditions(Rule rule)
        {
            // Simplified for hackathon - implement as needed
            if (rule.RawDto.conditions == null || rule.RawDto.conditions.Count == 0)
                return true;
                
            foreach (var condition in rule.RawDto.conditions)
            {
                // Add condition checking logic here
                Debug.Log($"Checking condition: {condition.type}");
            }
            
            return true;
        }
        
        void StartTimerTriggers()
        {
            foreach (var rule in rules.Values)
            {
                foreach (var trigger in rule.RawDto.triggers)
                {
                    if (trigger.type == "timer" && trigger.every_ms.HasValue)
                    {
                        StartTimer(rule.Id, trigger.every_ms.Value).Forget();
                    }
                }
            }
        }
        
        async UniTaskVoid StartTimer(string ruleId, int intervalMs)
        {
            Debug.Log($"Starting timer for rule {ruleId}: {intervalMs}ms");
            
            while (Application.isPlaying)
            {
                await UniTask.Delay(intervalMs);
                EventBus.Emit($"timer.{ruleId}", null);
            }
        }
        
        void OnDestroy()
        {
            engineCts?.Cancel();
            
            foreach (var cts in activeRules.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            
            EventBus.Reset();
        }
    }
}
```

## Implementation Timeline (16 Hours)

### Phase 1: Core Foundation (Hours 0-4)
**Owner: Person A**
- **Hour 0-1**: Project setup, package structure, assembly definitions
- **Hour 1-2**: Install dependencies (UniTask, Newtonsoft.Json)
- **Hour 2-3**: Implement DTOs and parsing infrastructure
- **Hour 3-4**: EventBus and GameState implementation

### Phase 2: Runtime Engine (Hours 4-8)
**Owner: Person B**
- **Hour 4-5**: RuleEngine core with event loop
- **Hour 5-6**: ActionExecutor with all 4 action types
- **Hour 6-7**: TriggerMatcher for basic triggers (event, timer, value)
- **Hour 7-8**: ConditionEvaluator implementation

### Phase 3: Unity Integration (Hours 8-12)
**Owner: Person C**
- **Hour 8-9**: DesktopInputAdapter with all keys for patterns
- **Hour 9-10**: AudioService and basic UI setup
- **Hour 10-11**: EnhancedDragHandler implementation
- **Hour 11-12**: DevPanel with rule loading UI

### Phase 4: Pattern Support (Hours 12-14) - STRETCH GOAL
**All Team**
- **Hour 12-13**: PatternSequenceWatcher implementation
- **Hour 13-14**: Integration and testing with Konami/double-click

### Phase 5: Testing & Polish (Hours 14-16)
**All Team**
- Test all 9 example rules
- Fix critical bugs only
- Record demo video
- Package documentation

## Circuit Breakers & Fallbacks

### Hour 8 Check
- [ ] Core event system working?
- [ ] At least 3 demo rules functional?
- [ ] **Decision**: If NO, skip drag handler and patterns

### Hour 12 Check
- [ ] 6 basic rules working perfectly?
- [ ] On schedule?
- [ ] **Decision**: If NO, skip patterns entirely, polish existing

### Fallback Plans
1. **Pattern Fallback**: If patterns aren't working by hour 14, remove them from demo
2. **Drag Fallback**: Use simple drag without enhanced features
3. **Audio Fallback**: If AudioService issues, use Debug.Log with colors
4. **Condition Fallback**: Skip conditions, focus on triggers/actions only

## The 9 Demo Rules

### Core 6 (Must Work)
1. **Click → Beep** - Basic event trigger
2. **Timer → Log** - Periodic execution  
3. **Threshold → Warning** - Value monitoring
4. **Sequential Actions** - Multiple actions with waits
5. **Mode Test (Restart)** - Testing restart mode
6. **Drag & Drop → Inventory** - Basic drag/drop

### Stretch 3 (Pattern-based)
7. **Konami Code** - 10-event sequence pattern
8. **Long Drag → Power** - Enhanced drag features
9. **Double-Click** - Simple 2-event pattern

## Success Metrics

### Minimum Viable Success (Hour 14)
- [ ] Package imports cleanly
- [ ] 6 core rules execute correctly
- [ ] No crashes on malformed JSON
- [ ] Audio/visual feedback working

### Full Success (Hour 16)
- [ ] All 9 rules working including patterns
- [ ] LLM can generate working rules
- [ ] Clean package structure
- [ ] Demo video recorded
- [ ] Documentation complete

## Team Roles

### Person A: Core Engine & Patterns
- EventBus, RuleEngine
- Pattern system (if time)
- Mode handling
- Final integration

### Person B: Actions & State
- ActionExecutor
- GameState management
- Timer system
- Service implementations

### Person C: Input & UI
- DesktopInputAdapter
- DevPanel
- Audio/visual feedback
- Demo scene setup

## Quick Decision Tree

**JSON won't parse?** → Log with line number, skip rule, continue  
**Pattern not matching?** → Debug.Log each event, check sequence  
**Behind at hour 8?** → Cut patterns and enhanced drag  
**Behind at hour 12?** → Focus on polish, skip new features  
**UniTask issues?** → Fall back to coroutines  
**Assembly issues?** → Check .asmdef references  

## Post-Hackathon Path

1. **Week 1**: Add validation, hot reload, better UI
2. **Week 2**: Complex conditions, expressions, more patterns
3. **Week 3**: Manifest system, rule priority
4. **Week 4**: Begin VR/Quest integration
5. **Month 2**: Full Quest optimization, FSM compilation

---

**Remember: Ship the simplest working version first. Every feature beyond the core 6 rules is a bonus. The pattern system proves sophistication but isn't required for success.**