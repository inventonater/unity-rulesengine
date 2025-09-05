Looking at these comprehensive proposals for a Unity Rules Engine for Quest VR, I can see they share strong core concepts while differing in implementation details. Let me synthesize the best aspects into a unified proposal.

# Unity Rules Engine for Quest VR - Final Implementation Plan

## Executive Summary

A deterministic, high-performance Event-Condition-Action (ECA) rules engine optimized for Quest VR (72-120Hz), supporting both human and LLM authoring. The architecture employs a **three-tier execution model** with zero runtime allocations, compiling rules to explicit per-rule finite state machines for predictable performance and IL2CPP safety.

**Core Innovation**: Hybrid architecture combining Burst-compiled hot paths for spatial/per-frame logic with a clean managed core for business logic, unified under an LLM-optimized schema that enforces explicit types and units.

## Architecture Overview

### Three-Tier Execution Model

**Tier 1: Performance-Critical (Burst-Compiled)**
- Target: <100μs per evaluation
- Spatial queries, proximity checks, high-frequency numeric evaluations  
- Native collections, SIMD operations, zero allocations
- Grid-based spatial acceleration for zone queries

**Tier 2: Standard Logic (Managed Core)**
- Target: 100-500μs per evaluation
- Rule FSM execution, expression evaluation, action dispatch
- .NET Standard 2.1, IL2CPP-safe patterns
- Pre-allocated object pools, struct-based events

**Tier 3: Reactive Patterns (Bounded Streams)**
- Target: 1-10ms acceptable
- Temporal patterns, gesture sequences, complex waits
- Custom zero-alloc reactive operators (not R3 to avoid allocations)
- Kept entirely off hot paths

### Core Components

```csharp
public struct EngineConfig
{
    public int MaxRules;              // Default: 256
    public int MaxEventsPerFrame;     // Default: 1000  
    public int MaxConcurrentInstances; // Default: 32
    public TimeSource TimeMode;       // Game vs Wall clock
}

[StructLayout(LayoutKind.Sequential)]
public struct EngineEvent
{
    public int EventTypeId;      // Pre-resolved at compile
    public int EntityId;         // Entity reference
    public float4 Data;          // Packed payload (Unity.Mathematics)
    public long TimestampMs;     // Monotonic time
}
```

## Rule Schema v2.0 (LLM-Optimized)

### Design Principles
- **Explicit Type Discrimination**: Every object has a "type" field
- **Consistent Arrays**: All multi-value fields are always arrays
- **Inline Units**: Field names include units and ranges
- **Enum Strings**: Replace booleans with descriptive enums
- **Flat When Possible**: Minimize nesting depth

### Schema Structure

```json
{
  "schema_version": "2.0.0",
  "id": "rule_unique_id",
  "alias": "Human readable name",
  "mode": "single|restart|queue|parallel",
  "max_instances_1_32": 1,
  "variables": {},
  "triggers": [],
  "conditions": [],
  "actions": [],
  "hints": {
    "tier": "critical|standard|reactive",
    "estimated_ms": 0.1
  }
}
```

### Trigger Types

```json
// State change with duration guard
{
  "type": "state",
  "entity": "player.health",
  "from": ["normal"],
  "to": ["low", "critical"],
  "for_ms_min_0": 1000,
  "options": {
    "debounce_ms": 100,
    "distinct": "deep"
  }
}

// Spatial zone events
{
  "type": "zone",
  "entity": "player.avatar",
  "zone": "dark_cave",
  "event": "enter|leave",
  "options": {
    "cooldown_ms": 5000
  }
}

// Pattern matching for gestures
{
  "type": "pattern",
  "window_ms_max_5000": 500,
  "sequence": [
    {"event": "button.a", "state": "pressed"},
    {"event": "button.a", "state": "pressed"}
  ]
}
```

### Conditions

```json
// All conditions use consistent structure
{"type": "state", "entity": "game.mode", "is": ["playing"]}
{"type": "numeric", "entity": "sensor.fps", "above": 30, "below": 120}
{"type": "time", "after": "08:00", "before": "20:00", "weekday": ["mon-fri"]}
{"type": "expr", "expr": "state('player.health') < 30 && vars.combat == true"}
```

### Actions

```json
// Service calls with typed data
{
  "type": "call",
  "service": "haptics.pulse",
  "data": {
    "hand": "left|right|both",
    "amplitude_0_to_1": 0.5,
    "duration_ms_10_5000": 50
  }
}

// Control flow with explicit types
{"type": "wait", "for_ms": 1000}
{"type": "wait", "until": {...}, "timeout_ms": 5000, "on_timeout": "continue|stop"}
{"type": "repeat", "count_1_100": 3, "actions": [...]}
{"type": "parallel", "actions": [...]}
```

## Implementation Details

### Memory Management

```csharp
public class MemorySystem
{
    // All allocations at startup
    private readonly struct PoolConfig
    {
        public const int RuleContexts = 32;
        public const int EventQueue = 4096;
        public const int TimerSlots = 512;
    }
    
    private readonly NativeArray<RuleData> ruleBuffer;
    private readonly RingBuffer<EngineEvent> eventQueue;
    private readonly ObjectPool<RuleContext> contextPool;
    
    // Zero allocations after warmup
    public void Initialize()
    {
        ruleBuffer = new NativeArray<RuleData>(256, Allocator.Persistent);
        eventQueue = new RingBuffer<EngineEvent>(PoolConfig.EventQueue);
        contextPool = new ObjectPool<RuleContext>(PoolConfig.RuleContexts);
    }
}
```

### Compilation Pipeline

```csharp
public class RuleCompiler
{
    public CompiledRulePack Compile(RuleDocument[] documents)
    {
        // Phase 1: Schema validation with LLM-friendly errors
        var validation = ValidateWithCorrections(documents);
        if (!validation.Success)
            return validation.WithSuggestions();
            
        // Phase 2: String interning and ID resolution
        var symbols = new SymbolTable();
        ResolveAllReferences(documents, symbols);
        
        // Phase 3: Expression compilation to bytecode
        var expressions = CompileExpressions(documents, symbols);
        
        // Phase 4: FSM generation per rule
        var stateMachines = GenerateFSMs(documents, expressions);
        
        // Phase 5: Binary packaging (.uar format)
        return PackageRules(stateMachines, symbols);
    }
}
```

### Timer Wheel Implementation

```csharp
[BurstCompile]
public struct TimerWheel : IDisposable
{
    private const int WHEEL_SIZE = 512;
    private const int TICK_MS = 16; // ~60Hz
    
    private NativeArray<TimerBucket> buckets;
    private NativeList<TimerEntry> overflow;
    private long currentTick;
    
    public void Schedule(long delayMs, int ruleId, int actionId)
    {
        long targetTick = currentTick + (delayMs / TICK_MS);
        int bucketIndex = (int)(targetTick % WHEEL_SIZE);
        
        if (targetTick < currentTick + WHEEL_SIZE)
        {
            buckets[bucketIndex].Add(new TimerEntry 
            { 
                DueTick = targetTick,
                RuleId = ruleId,
                ActionId = actionId
            });
        }
        else
        {
            overflow.Add(new TimerEntry {...});
        }
    }
    
    public void Advance(long nowMs)
    {
        long newTick = nowMs / TICK_MS;
        while (currentTick < newTick)
        {
            ProcessBucket(currentTick % WHEEL_SIZE);
            currentTick++;
            MigrateOverflow();
        }
    }
}
```

### Spatial Acceleration (Burst)

```csharp
[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct SpatialGrid : IJobParallelFor
{
    private const float CELL_SIZE = 2.0f;
    
    [ReadOnly] public NativeArray<float3> EntityPositions;
    [ReadOnly] public NativeMultiHashMap<int, ZoneBounds> GridToZones;
    [WriteOnly] public NativeQueue<ZoneEvent>.ParallelWriter Events;
    
    public void Execute(int entityIndex)
    {
        var pos = EntityPositions[entityIndex];
        int gridKey = GetGridKey(pos);
        
        // Check only nearby grid cells
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int checkKey = gridKey + (dx << 16) + dy;
            if (GridToZones.TryGetFirstValue(checkKey, out var zone, out var iter))
            {
                do {
                    if (IsInZone(pos, zone))
                    {
                        Events.Enqueue(new ZoneEvent 
                        { 
                            EntityId = entityIndex,
                            ZoneId = zone.Id,
                            EventType = DetermineEventType(entityIndex, zone.Id)
                        });
                    }
                } while (GridToZones.TryGetNextValue(out zone, ref iter));
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetGridKey(float3 pos)
    {
        int x = (int)(pos.x / CELL_SIZE);
        int z = (int)(pos.z / CELL_SIZE);
        return (x << 16) | (z & 0xFFFF);
    }
}
```

## Unity Adapter

### Service Registry

```csharp
public class ServiceRegistry
{
    private readonly Dictionary<string, ServiceDefinition> services = new();
    
    public void Initialize()
    {
        // Haptics with schema validation
        Register<HapticData>("haptics.pulse", async (data) =>
        {
            await UniTask.SwitchToMainThread();
            var controller = data.Hand switch
            {
                "left" => OVRInput.Controller.LTouch,
                "right" => OVRInput.Controller.RTouch,
                "both" => OVRInput.Controller.Touch,
                _ => OVRInput.Controller.RTouch
            };
            
            OVRInput.SetControllerVibration(
                data.Amplitude,
                data.Amplitude,
                controller
            );
            
            await UniTask.Delay(data.DurationMs);
            OVRInput.SetControllerVibration(0, 0, controller);
        },
        new ServiceSchema
        {
            Parameters = new[]
            {
                new Param("hand", ParamType.Enum, new[] {"left", "right", "both"}),
                new Param("amplitude_0_to_1", ParamType.Float, min: 0, max: 1),
                new Param("duration_ms_10_5000", ParamType.Int, min: 10, max: 5000)
            },
            ThreadAffinity = ThreadAffinity.MainThread
        });
        
        // Additional services...
    }
}
```

## Example Rules

### 1. Basic Button Haptics
```json
{
  "schema_version": "2.0.0",
  "id": "button_haptic",
  "mode": "single",
  "triggers": [{
    "type": "state",
    "entity": "xr.button.a",
    "to": ["pressed"],
    "options": {"cooldown_ms": 50}
  }],
  "actions": [{
    "type": "call",
    "service": "haptics.pulse",
    "data": {
      "hand": "right",
      "amplitude_0_to_1": 0.5,
      "duration_ms_10_5000": 30
    }
  }]
}
```

### 2. Zone-Based Dynamic Lighting
```json
{
  "schema_version": "2.0.0",
  "id": "cave_lighting",
  "mode": "restart",
  "triggers": [
    {"type": "zone", "entity": "player", "zone": "dark_cave", "event": "enter", "id": "enter"},
    {"type": "zone", "entity": "player", "zone": "dark_cave", "event": "leave", "id": "leave"}
  ],
  "actions": [{
    "type": "choose",
    "branches": [
      {
        "when": [{"type": "trigger", "id": ["enter"]}],
        "do": [{
          "type": "parallel",
          "actions": [
            {"type": "call", "service": "light.enable", "data": {"entity": "torch", "brightness_0_255": 255}},
            {"type": "call", "service": "audio.play", "data": {"sound": "torch_ignite", "volume_0_to_1": 0.7}},
            {"type": "call", "service": "camera.exposure", "data": {"value": 1.5, "duration_ms": 2000}}
          ]
        }]
      }
    ]
  }],
  "hints": {"tier": "standard"}
}
```

### 3. Health Warning System
```json
{
  "schema_version": "2.0.0",
  "id": "health_monitor",
  "mode": "single",
  "variables": {"warning_threshold": 30},
  "triggers": [{
    "type": "numeric",
    "entity": "player.health",
    "below": 30,
    "for_ms_min_0": 1000
  }],
  "conditions": [
    {"type": "state", "entity": "player.status", "is": ["alive"]}
  ],
  "actions": [{
    "type": "repeat",
    "until": [{"type": "numeric", "entity": "player.health", "above": 30}],
    "actions": [
      {
        "type": "parallel",
        "actions": [
          {"type": "call", "service": "ui.warning", "data": {"message": "Health Low: ${state('player.health')}%"}},
          {"type": "call", "service": "haptics.pattern", "data": {"hand": "both", "pattern": "heartbeat"}},
          {"type": "call", "service": "vfx.screen", "data": {"effect": "blood_vignette", "intensity_0_to_1": 0.7}}
        ]
      },
      {"type": "wait", "for_ms": 3000}
    ]
  }]
}
```

### 4. Gesture Recognition (Double-Tap)
```json
{
  "schema_version": "2.0.0",
  "id": "double_tap_menu",
  "mode": "single",
  "triggers": [{
    "type": "pattern",
    "window_ms_max_5000": 400,
    "sequence": [
      {"event": "xr.button.primary", "state": "pressed"},
      {"event": "xr.button.primary", "state": "pressed"}
    ]
  }],
  "actions": [
    {"type": "call", "service": "ui.toggle_menu", "data": {"position": "controller"}},
    {"type": "call", "service": "haptics.pattern", "data": {"hand": "right", "pattern": "double_click"}}
  ],
  "hints": {"tier": "critical", "estimated_ms": 0.05}
}
```

### 5. Performance Auto-Optimizer
```json
{
  "schema_version": "2.0.0",
  "id": "perf_optimizer",
  "mode": "single",
  "triggers": [{
    "type": "numeric",
    "entity": "sensor.fps",
    "below": 60,
    "for_ms_min_0": 5000
  }],
  "conditions": [
    {"type": "state", "entity": "settings.auto_optimize", "is": ["enabled"]}
  ],
  "actions": [{
    "type": "repeat",
    "count_1_100": 3,
    "until": [{"type": "numeric", "entity": "sensor.fps", "above": 65}],
    "actions": [
      {"type": "call", "service": "graphics.reduce", "data": {"step": 1}},
      {"type": "wait", "for_ms": 2000},
      {"type": "call", "service": "log.info", "data": {"message": "FPS: ${state('sensor.fps')}"}}
    ]
  }]
}
```

### 6. Quest Progression with Timeout
```json
{
  "schema_version": "2.0.0",
  "id": "quest_portal",
  "mode": "parallel",
  "max_instances_1_32": 3,
  "triggers": [{
    "type": "event",
    "name": "quest.objective_complete",
    "filter": {"quest_id": "ancient_artifact"}
  }],
  "actions": [
    {"type": "call", "service": "object.spawn", "data": {"prefab": "portal", "position": "${state('marker.temple')}"}},
    {
      "type": "wait",
      "until": {"type": "zone", "entity": "player", "zone": "portal", "event": "enter"},
      "timeout_ms": 300000,
      "on_timeout": "continue"
    },
    {
      "type": "choose",
      "branches": [{
        "when": [{"type": "zone", "entity": "player", "zone": "portal", "occupied": true}],
        "do": [{"type": "call", "service": "scene.load", "data": {"scene": "temple_interior"}}]
      }],
      "default": [
        {"type": "call", "service": "ui.hint", "data": {"message": "Portal expired"}},
        {"type": "call", "service": "object.destroy", "data": {"object": "portal"}}
      ]
    }
  ]
}
```

## Performance Targets & Validation

| Metric | Target | Validation Method |
|--------|--------|------------------|
| Simple rule evaluation | <0.1ms | Burst profiler |
| Complex rule evaluation | <0.5ms | Unity Profiler |
| 100 concurrent rules | <2ms/frame | Device testing |
| Memory per rule | <4KB | Memory snapshot |
| Steady-state allocations | 0 bytes/frame | Profiler verification |
| Hot reload time | <200ms | Editor tooling |
| LLM generation success | >90% valid | Schema compliance |

## Implementation Timeline

### Phase 1: Core Foundation (Weeks 1-2)
- Rule compiler with FSM generation
- Event bus and timer wheel
- Expression VM
- Memory pool system
- Unit test framework

### Phase 2: Unity Integration (Weeks 3-4)
- Unity adapter and service registry
- Meta XR input bridge
- Spatial zone system with Burst
- Basic services (haptics, audio, UI)
- Editor window with hot reload

### Phase 3: Performance Optimization (Week 5)
- Burst compilation for critical paths
- Native collections integration
- Spatial grid acceleration
- Profile and optimize allocations
- Device benchmarking

### Phase 4: Polish & Documentation (Week 6)
- LLM authoring guidelines
- Comprehensive example library
- Performance profiling tools
- Migration utilities
- Release preparation

## Key Innovations

1. **LLM-Optimized Schema**: Explicit types, inline units, consistent arrays make it nearly impossible for LLMs to generate invalid rules

2. **Three-Tier Architecture**: Separates performance-critical spatial logic from business logic while keeping reactive patterns entirely off hot paths

3. **Timer Wheel + FSM**: O(1) timer operations combined with explicit state machines provide deterministic, debuggable execution

4. **Zero Runtime Allocations**: All memory pre-allocated at startup with struct-based events and object pooling

5. **Burst Spatial Acceleration**: Grid-based zone queries run in parallel jobs for massive scale

This architecture delivers Quest-ready performance while maintaining the flexibility needed for complex game logic and LLM-generated content. The unified schema and tiered execution model ensure both human designers and AI can author reliable, performant rules.