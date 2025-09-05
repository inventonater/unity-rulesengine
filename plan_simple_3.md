# Unity Rules Engine Weekend Prototype - Implementation Plan v4.0

## Executive Summary

A streamlined Event-Condition-Action (ECA) rules engine for Unity desktop, designed to validate the LLM-to-gameplay pipeline in a weekend. Uses UniTask for async operations, MIT-licensed JSON parsing, and minimal validation to achieve rapid end-to-end demonstration of AI-authored game logic.

**Core Flow**: LLM generates JSON → Parse to runtime rules → Events trigger evaluation → Actions execute → Observable output

## Architecture Overview

### Simplified System Components

```
┌─────────────────────────────────────────────────────────────┐
│                    LLM-Generated JSON                        │
│                  (Minimal Schema v4.0)                       │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                   Quick Parser                               │
│         (SimpleJSON or System.Text.Json)                     │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                 Runtime Engine                               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────┐  │
│  │Event Bus │→ │Rule Match│→ │Evaluation│→ │Action Queue│  │
│  │(UniRx)   │  │(Dict)    │  │(Simple)  │  │(UniTask)   │  │
│  └──────────┘  └──────────┘  └──────────┘  └────────────┘  │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                  Output Systems                              │
│     Debug.Log | Audio | Visual Indicators | UI Text         │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow
1. **Input**: Mouse clicks, keyboard, timer ticks → Events
2. **Processing**: Event → Find matching rules → Check conditions → Queue actions
3. **Output**: Execute actions with UniTask → Observable feedback

## Dependencies (All MIT/Apache/Free)

### Required Unity Packages
```json
{
  "dependencies": {
    "com.unity.textmeshpro": "3.0.6",
    "com.unity.mathematics": "1.2.6",
    "com.cysharp.unitask": "2.5.0",
    "com.neuecc.unirx": "7.1.0"
  }
}
```

### Single-File Additions
- **SimpleJSON.cs** - MIT licensed, drop into Plugins/
- **Optional**: System.Text.Json via Unity Package Manager (built-in .NET)

## Minimal Schema v4.0

### Core Rule Structure
```json
{
  "id": "rule_identifier",
  "triggers": [...],
  "conditions": [...],
  "actions": [...]
}
```

### Trigger Types (Simplified)
```json
// Mouse trigger
{"type": "mouse", "button": "left|right", "event": "down|up|hold"}

// Keyboard trigger
{"type": "key", "key": "space|a|w|s|d", "event": "down|up|hold"}

// Timer trigger
{"type": "timer", "interval_ms": 1000}

// Value change trigger
{"type": "value", "path": "player.health", "comparison": "<|>|==", "value": 30}

// Custom event trigger
{"type": "event", "name": "quest_complete"}
```

### Condition Types (Minimal)
```json
// Simple comparison
{"type": "compare", "left": "player.score", "op": ">", "right": 100}

// State check
{"type": "state", "entity": "game", "is": "playing"}

// Logical grouping
{"type": "all|any", "conditions": [...]}
```

### Action Types (Observable)
```json
// Log output
{"type": "log", "message": "Rule fired: ${rule.id}", "level": "info|warning|error"}

// Play sound
{"type": "audio", "clip": "success", "volume": 0.5}

// Set value
{"type": "set", "path": "player.score", "value": "${player.score + 10}"}

// Visual feedback
{"type": "visual", "effect": "flash|shake|highlight", "target": "screen|object"}

// Wait
{"type": "wait", "ms": 1000}

// Fire event
{"type": "event", "name": "custom_event", "data": {...}}

// Sequence
{"type": "sequence", "actions": [...]}

// Parallel
{"type": "parallel", "actions": [...]}
```

## Core Components

### 1. Event System (UniRx)
```csharp
public static class EventBus
{
    static readonly Subject<GameEvent> stream = new();
    public static IObservable<GameEvent> Stream => stream;
    public static void Fire(GameEvent evt) => stream.OnNext(evt);
}
```

### 2. Rule Repository
```csharp
public class RuleRepository
{
    Dictionary<string, List<Rule>> triggerToRules = new();
    List<Rule> allRules = new();
    
    public void LoadFromJson(string json) { /* SimpleJSON parsing */ }
    public List<Rule> GetRulesForTrigger(string triggerType) { }
}
```

### 3. Rule Evaluator (UniTask)
```csharp
public class RuleEvaluator
{
    public async UniTask EvaluateAsync(Rule rule, GameEvent evt)
    {
        if (!CheckConditions(rule.Conditions)) return;
        await ExecuteActions(rule.Actions);
    }
}
```

### 4. Action Executor
```csharp
public class ActionExecutor
{
    public async UniTask ExecuteAsync(ActionData action)
    {
        switch(action.Type)
        {
            case "log": Debug.Log(action.Message); break;
            case "audio": AudioSource.PlayClipAtPoint(...); break;
            case "wait": await UniTask.Delay(action.Ms); break;
            case "sequence": await ExecuteSequence(action.Actions); break;
            case "parallel": await UniTask.WhenAll(...); break;
        }
    }
}
```

### 5. Input Adapter
```csharp
public class InputAdapter : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            EventBus.Fire(new GameEvent("mouse", "left", "down"));
        if (Input.GetKeyDown(KeyCode.Space))
            EventBus.Fire(new GameEvent("key", "space", "down"));
    }
}
```

### 6. Value Store (Simple)
```csharp
public static class GameState
{
    static Dictionary<string, object> values = new();
    
    public static T Get<T>(string path) { }
    public static void Set(string path, object value) { }
    public static IObservable<T> Observe<T>(string path) { }
}
```

## Implementation Patterns

### Pattern 1: Async Action Chains with UniTask
- Actions return `UniTask` for composition
- Sequence: `await UniTask.WhenAll(actions.Select(ExecuteAsync))`
- Parallel: `foreach(var action in actions) await ExecuteAsync(action)`
- Cancellation via `CancellationTokenSource` for rule interruption

### Pattern 2: Observable State with UniRx
- GameState changes emit through `Subject<StateChange>`
- Rules subscribe to specific paths: `GameState.Observe("player.health")`
- Automatic cleanup via `CompositeDisposable`

### Pattern 3: Expression Evaluation (Simplified)
- Basic math: Parse and evaluate `"${player.score + 10}"`
- String interpolation: Replace `${path}` with GameState values
- No complex parsing - keep expressions simple for weekend scope

### Pattern 4: Hot Reload
- FileSystemWatcher on StreamingAssets/rules/
- Queue changes, apply on main thread
- Full rule replacement (no incremental updates)

## Example Rules

### 1. Mouse Click Counter
```json
{
  "id": "click_counter",
  "triggers": [
    {"type": "mouse", "button": "left", "event": "down"}
  ],
  "actions": [
    {"type": "set", "path": "clicks", "value": "${clicks + 1}"},
    {"type": "log", "message": "Clicks: ${clicks}"},
    {"type": "audio", "clip": "click", "volume": 0.3}
  ]
}
```

### 2. Health Warning System
```json
{
  "id": "low_health_warning",
  "triggers": [
    {"type": "value", "path": "player.health", "comparison": "<", "value": 30}
  ],
  "conditions": [
    {"type": "state", "entity": "player", "is": "alive"}
  ],
  "actions": [
    {"type": "parallel", "actions": [
      {"type": "log", "message": "WARNING: Low health!", "level": "warning"},
      {"type": "audio", "clip": "warning", "volume": 0.8},
      {"type": "visual", "effect": "flash", "target": "screen"}
    ]}
  ]
}
```

### 3. Combo Detection
```json
{
  "id": "double_click_combo",
  "triggers": [
    {"type": "mouse", "button": "left", "event": "down"}
  ],
  "conditions": [
    {"type": "compare", "left": "${time - lastClickTime}", "op": "<", "right": 0.5}
  ],
  "actions": [
    {"type": "set", "path": "combo", "value": "${combo + 1}"},
    {"type": "log", "message": "COMBO x${combo}!"},
    {"type": "visual", "effect": "shake", "target": "screen"}
  ]
}
```

### 4. Timed Events
```json
{
  "id": "periodic_spawn",
  "triggers": [
    {"type": "timer", "interval_ms": 5000}
  ],
  "conditions": [
    {"type": "compare", "left": "enemies.count", "op": "<", "right": 10}
  ],
  "actions": [
    {"type": "event", "name": "spawn_enemy"},
    {"type": "log", "message": "Spawning enemy..."},
    {"type": "set", "path": "enemies.count", "value": "${enemies.count + 1}"}
  ]
}
```

### 5. State Machine Transition
```json
{
  "id": "enter_combat",
  "triggers": [
    {"type": "event", "name": "enemy_spotted"}
  ],
  "conditions": [
    {"type": "state", "entity": "game", "is": "exploration"}
  ],
  "actions": [
    {"type": "sequence", "actions": [
      {"type": "log", "message": "Entering combat!"},
      {"type": "audio", "clip": "battle_start", "volume": 1.0},
      {"type": "set", "path": "game.state", "value": "combat"},
      {"type": "visual", "effect": "fade", "target": "screen"},
      {"type": "wait", "ms": 500},
      {"type": "event", "name": "combat_started"}
    ]}
  ]
}
```

### 6. Keyboard Pattern
```json
{
  "id": "konami_code",
  "triggers": [
    {"type": "key", "key": "a", "event": "down"}
  ],
  "conditions": [
    {"type": "compare", "left": "input.sequence", "op": "==", "right": "uuddlrlrb"}
  ],
  "actions": [
    {"type": "log", "message": "KONAMI CODE ACTIVATED!", "level": "error"},
    {"type": "parallel", "actions": [
      {"type": "audio", "clip": "powerup", "volume": 1.0},
      {"type": "visual", "effect": "highlight", "target": "screen"},
      {"type": "set", "path": "player.invincible", "value": true}
    ]}
  ]
}
```

## Implementation Timeline

### Day 1 - Morning (4 hours)
1. **Project Setup** (30 min)
   - Import UniTask, UniRx via Package Manager
   - Add SimpleJSON.cs to Plugins/
   - Create folder structure

2. **Core Systems** (2 hours)
   - EventBus with UniRx Subject
   - GameState dictionary with Get/Set
   - Basic Rule/Trigger/Action data classes
   - Simple JSON parser using SimpleJSON

3. **Input System** (30 min)
   - Mouse and keyboard adapters
   - Timer system with UniTask.Delay

4. **Testing Harness** (1 hour)
   - Debug UI with TextMeshPro
   - Visual feedback manager
   - Audio clip player

### Day 1 - Afternoon (4 hours)
1. **Rule Evaluation** (2 hours)
   - Condition checking (simple comparisons)
   - Basic expression evaluation (${} replacement)
   - Rule-to-trigger mapping

2. **Action Execution** (2 hours)
   - UniTask-based action executor
   - Sequence/Parallel patterns
   - Log, Audio, Visual, Set actions

### Day 2 - Morning (4 hours)
1. **LLM Integration Mock** (1 hour)
   - Simulate LLM responses with sample JSONs
   - Rule injection endpoint

2. **Hot Reload** (1 hour)
   - FileSystemWatcher setup
   - Runtime rule replacement

3. **Complex Examples** (2 hours)
   - Multi-trigger rules
   - Nested conditions
   - Action chains

### Day 2 - Afternoon (4 hours)
1. **Polish & Debug** (2 hours)
   - Error handling (try-catch, not validation)
   - Performance monitoring
   - Debug visualization

2. **Demo Scenarios** (2 hours)
   - Interactive playground
   - LLM prompt → Rule → Gameplay demos
   - Documentation of patterns that work

## Key Simplifications

### What We're NOT Doing
- No FSM compilation (direct interpretation)
- No manifest validation (trust the LLM)
- No type safety (everything is dynamic)
- No optimization (prototype only)
- No VR-specific code (desktop only)
- No complex expressions (basic math only)
- No persistence (in-memory only)

### What We ARE Doing
- End-to-end LLM to gameplay pipeline
- Observable feedback for all actions
- Async/await patterns with UniTask
- Hot reload for rapid iteration
- Multiple trigger/condition/action types
- Clear patterns for LLM training

## Success Metrics

1. **LLM Integration**: Can generate valid rules from natural language
2. **Response Time**: <100ms from event to action
3. **Complexity**: Support 5+ triggers, 3+ conditions, 10+ actions per rule
4. **Iteration Speed**: <5 seconds from JSON edit to in-game behavior
5. **Pattern Coverage**: Demonstrate 10+ different gameplay patterns

## Risk Mitigation

### If JSON Parsing Fails
- Pre-validate with online JSON validator
- Use try-catch, default to safe values
- Log errors, continue execution

### If Performance Is Poor
- Reduce rule count (target 50 max)
- Simplify conditions (no nested evaluation)
- Profile with Unity Profiler, not VR metrics

### If LLM Output Is Inconsistent
- Provide more examples in prompt
- Use schema in system message
- Post-process common errors

## Next Steps After Weekend

If prototype succeeds:
1. Add validation layer with better error messages
2. Implement proper FSM compilation for performance
3. Add VR-specific triggers and actions
4. Create visual rule editor
5. Build comprehensive test suite
6. Optimize for Quest hardware

This plan focuses on proving the concept works rather than building production infrastructure, allowing maximum iteration on the LLM-to-gameplay pipeline within the weekend constraint.