# Unity Rules Engine for Quest VR - Implementation Plan v3.1

## Executive Summary

A deterministic Event-Condition-Action (ECA) rules engine optimized for Quest VR (72-120Hz) and LLM authoring reliability. The system uses standard trigger/condition/action patterns, validates against JSON manifests, and compiles rules to in-memory FSMs without intermediate binary formats.

**Core Design Principles**:
- One way to express each concept (no aliases)
- Explicit type discrimination everywhere
- Arrays for all potentially multiple values
- Units embedded in field names
- Zero runtime allocations

## Architecture Overview

### System Components

```
┌─────────────────────────────────────────────────────────┐
│                    JSON Rules                            │
│              (Schema v3.1 + Manifests)                   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                Validation & Parsing                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────────┐ │
│  │ JSON     │→ │ Manifest │→ │ AST Construction     │ │
│  │ Schema   │  │ Validation│  │ (Type Resolution)    │ │
│  └──────────┘  └──────────┘  └──────────────────────┘ │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│              Runtime Compilation                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────────┐ │
│  │   FSM    │  │Expression│  │  Routing Tables      │ │
│  │ Generator│  │ Compiler │  │  (Event→Rule Map)    │ │
│  └──────────┘  └──────────┘  └──────────────────────┘ │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                 Runtime Engine                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐ │
│  │Event Bus │  │FSM Engine│  │Timer Wheel│  │Service │ │
│  │(Ring Buf)│  │(Per Rule)│  │(O(1) ops) │  │Registry│ │
│  └──────────┘  └──────────┘  └──────────┘  └────────┘ │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                Unity Adapter Layer                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐ │
│  │XR Input  │  │  Spatial │  │ Services │  │ Editor │ │
│  │  Bridge  │  │   Zones  │  │ (Haptics)│  │  Tools │ │
│  └──────────┘  └──────────┘  └──────────┘  └────────┘ │
└──────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Rule Loading**: JSON → Validation → AST → FSM (in memory)
2. **Event Processing**: Input → Event Bus → FSM Evaluation → Actions
3. **Hot Reload**: File Change → Parse Changed Rules → Replace FSM

## Schema Definition v3.1

### Core Rule Structure

```json
{
  "schema_version": "3.1.0",
  "id": "unique_identifier",
  "description": "Optional human-readable description",
  "mode": "single|restart|queue|parallel",
  "max_instances_1_to_10": 1,
  "variables": {},
  "triggers": [],
  "conditions": [],
  "actions": []
}
```

### Trigger Types

All triggers must have a `type` field and optional `id` for referencing:

```json
// State change trigger
{
  "type": "state_change",
  "id": "optional_id",
  "entity": ["player.health"],
  "from": ["normal", "high"],
  "to": ["low", "critical"],
  "for_ms_0_to_60000": 1000
}

// Numeric threshold trigger
{
  "type": "numeric_threshold",
  "entity": ["player.health"],
  "below": 30,
  "above": 10,
  "for_ms_0_to_60000": 1000
}

// Zone event trigger
{
  "type": "zone_event",
  "entity": ["player.avatar"],
  "zone": "dark_cave",
  "event": "enter|leave"
}

// Time schedule trigger
{
  "type": "time_schedule",
  "at": "HH:MM:SS",
  "every_seconds": 60,
  "every_minutes": 5,
  "every_hours": 1
}

// Event trigger
{
  "type": "event",
  "name": "quest.complete",
  "filter": {"quest_id": "main_01"}
}

// Pattern trigger
{
  "type": "pattern",
  "within_ms_10_to_5000": 500,
  "sequence": [
    {"event": "button.press", "data": {"button": "a"}},
    {"event": "button.press", "data": {"button": "a"}}
  ]
}
```

### Condition Types

```json
// State condition
{
  "type": "state_equals",
  "entity": ["game.mode"],
  "equals": ["playing", "paused"]
}

// Numeric comparison
{
  "type": "numeric_compare",
  "entity": ["sensor.fps"],
  "above": 30,
  "below": 120
}

// Time window
{
  "type": "time_window",
  "after": "08:00",
  "before": "20:00",
  "weekdays": ["mon", "tue", "wed", "thu", "fri"]
}

// Logical operators
{
  "type": "all_of|any_of|none_of",
  "conditions": [...]
}

// Expression (limited to simple comparisons)
{
  "type": "expression",
  "expr": "player.health < 30 && game.mode == 'combat'"
}

// Check which trigger fired
{
  "type": "trigger_fired",
  "trigger_id": ["enter_zone"]
}
```

### Action Types

```json
// Service call
{
  "type": "service_call",
  "service": "haptics.pulse",
  "data": {
    "hand": "right",
    "amplitude_0_to_1": 0.5,
    "duration_ms_10_to_5000": 50
  }
}

// Wait for duration
{
  "type": "wait_duration",
  "duration_ms_0_to_60000": 1000
}

// Wait for condition
{
  "type": "wait_condition",
  "condition": {...},
  "timeout_ms_0_to_600000": 30000,
  "on_timeout": "continue|stop"
}

// Conditional branching
{
  "type": "branch",
  "if": {...condition...},
  "then": [...actions...],
  "else": [...actions...]
}

// Parallel execution
{
  "type": "parallel",
  "actions": [...]
}

// Repeat N times
{
  "type": "repeat_count",
  "count_1_to_100": 3,
  "actions": [...]
}

// Repeat until condition
{
  "type": "repeat_until",
  "condition": {...},
  "max_iterations_1_to_1000": 100,
  "actions": [...]
}

// Set variable
{
  "type": "set_variable",
  "name": "counter",
  "value": "${counter + 1}"
}

// Stop execution
{
  "type": "stop",
  "reason": "completed"
}
```

## Manifest System

### Entity Manifest (JSON)

```json
{
  "version": "1.0.0",
  "entities": {
    "player": {
      "health": {
        "type": "numeric",
        "min": 0,
        "max": 100,
        "unit": "percent",
        "states": {
          "critical": [0, 20],
          "low": [20, 40],
          "normal": [40, 80],
          "high": [80, 100]
        }
      },
      "status": {
        "type": "enum",
        "values": ["alive", "dead", "stunned", "invisible"]
      }
    },
    "xr": {
      "controller": {
        "template": "{hand}.{control}",
        "variables": {
          "hand": ["left", "right"],
          "control": ["button_primary", "button_secondary", "trigger", "thumbstick"]
        }
      }
    },
    "sensor": {
      "fps": {
        "type": "numeric",
        "min": 0,
        "max": 120,
        "unit": "frames_per_second"
      }
    }
  },
  "zones": {
    "dark_cave": {
      "shape": "box",
      "center": [0, 0, 0],
      "size": [10, 5, 10]
    },
    "portal_area": {
      "shape": "sphere",
      "center": [100, 0, 50],
      "radius": 2
    }
  }
}
```

### Service Manifest (JSON)

```json
{
  "version": "1.0.0",
  "services": {
    "haptics.pulse": {
      "thread_affinity": "main",
      "parameters": {
        "hand": {
          "type": "enum",
          "values": ["left", "right", "both"],
          "required": true
        },
        "amplitude_0_to_1": {
          "type": "float",
          "min": 0,
          "max": 1,
          "required": true
        },
        "duration_ms_10_to_5000": {
          "type": "integer",
          "min": 10,
          "max": 5000,
          "required": true
        }
      }
    },
    "lights.set": {
      "thread_affinity": "main",
      "parameters": {
        "entity": {
          "type": "string[]",
          "required": true
        },
        "brightness_0_to_255": {
          "type": "integer",
          "min": 0,
          "max": 255,
          "required": true
        }
      }
    }
  }
}
```

## Compilation Pipeline

### Phase 1: Validation
1. **JSON Schema Validation**: Structure and required fields
2. **Manifest Reference Validation**: All entities/services exist
3. **Type Checking**: Values match declared types
4. **Range Validation**: Numbers within min/max
5. **Enum Validation**: Values in allowed list

### Phase 2: AST Construction
The AST (Abstract Syntax Tree) transforms JSON into typed objects:

**Input JSON:**
```json
{
  "triggers": [{
    "type": "state_change",
    "entity": ["player.health"],
    "to": ["low"]
  }]
}
```

**AST Output:**
```
RuleAST {
  RuleId: "rule_1",
  Triggers: [
    StateChangeTrigger {
      EntityId: 42,        // Resolved from manifest
      ToStates: [16],      // "low" → state ID 16
      Debounce: 0
    }
  ]
}
```

### Phase 3: Runtime Compilation
1. **FSM Generation**: Create state machine from AST
2. **Expression Compilation**: Parse expressions to evaluators
3. **Routing Table**: Build event → rule mappings
4. **Optimization**: Reorder conditions, merge duplicates

## Runtime Components

### Event Bus
- Ring buffer (4096 slots)
- Struct-based events (no heap allocation)
- Pre-computed routing tables
- Overflow policy: drop oldest + warning

### FSM Engine
- One FSM per rule
- Stack-based state tracking
- Pre-allocated context pool (32 contexts)
- Deterministic execution order

### Timer Wheel
- 512 slots at 16ms resolution
- O(1) insertion and removal
- Handles near (< 8s) and far timers
- Monotonic clock source

### Service Registry
- Thread affinity enforcement
- Parameter validation
- Retry policies
- Error handling

## Memory Management

All allocations at startup:
```csharp
public class MemoryPools
{
    // Fixed sizes
    const int MAX_RULES = 256;
    const int MAX_EVENTS = 4096;
    const int MAX_CONTEXTS = 32;
    const int MAX_TIMERS = 1024;
    
    // Pre-allocated pools
    RuleContext[] contextPool;
    EngineEvent[] eventBuffer;
    TimerEntry[] timerPool;
    FSMInstance[] instancePool;
}
```

## Unity Integration

### Dependencies
- Unity 2022.3 LTS
- Unity.Mathematics (SIMD)
- Unity.Collections (Native arrays)
- Unity.Burst (optional)
- Meta XR SDK

### Threading Model
- Engine runs on main thread
- Service calls marshaled to main thread
- Async I/O on thread pool
- Burst jobs for spatial queries (optional)

## Error Handling

### Validation Errors
```
ERROR: Unknown entity "player.helth" at triggers[0].entity
  Did you mean: "player.health"? (edit distance: 1)
  Available entities:
    - player.health (numeric: 0-100%)
    - player.mana (numeric: 0-100%)
    - player.status (enum: alive|dead|stunned)
```

### Type Coercion
Automatic fixes for LLM errors:
- `"100"` → `100` (string to number)
- `"value"` → `["value"]` (single to array)
- `"2s"` → `2000` (duration parsing)
- `true` → `"enabled"` (boolean to enum)

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Rule evaluation | <0.5ms p95 | Unity Profiler |
| Event throughput | 1000/frame | Synthetic benchmark |
| Memory per rule | <4KB | Memory snapshot |
| Startup allocation | <10MB | Profiler |
| Steady-state allocation | 0 bytes/frame | Frame debugger |
| Hot reload | <200ms | Editor timing |
| LLM generation success | >90% first try | Validation tests |

## Implementation Timeline

### Week 1-2: Foundation
- JSON schema definition
- Manifest system
- Validation pipeline
- Unit test framework

### Week 3: Parsing & Compilation
- AST builder
- Expression parser
- FSM generator
- Routing tables

### Week 4-5: Runtime Engine
- Event bus
- FSM executor
- Timer wheel
- Memory pools

### Week 6-7: Unity Integration
- Service registry
- XR input bridge
- Spatial zones
- Editor tools

### Week 8: Polish & Testing
- Performance optimization
- Documentation
- Example library
- LLM testing

## Example Rules

### 1. Simple Button Haptics
```json
{
  "schema_version": "3.1.0",
  "id": "button_haptic_simple",
  "mode": "single",
  "triggers": [{
    "type": "state_change",
    "entity": ["xr.controller.right.button_primary"],
    "to": ["pressed"]
  }],
  "actions": [{
    "type": "service_call",
    "service": "haptics.pulse",
    "data": {
      "hand": "right",
      "amplitude_0_to_1": 0.5,
      "duration_ms_10_to_5000": 50
    }
  }]
}
```

### 2. Zone-Based Lighting
```json
{
  "schema_version": "3.1.0",
  "id": "cave_lighting_system",
  "mode": "restart",
  "triggers": [
    {
      "type": "zone_event",
      "id": "enter_cave",
      "entity": ["player.avatar"],
      "zone": "dark_cave",
      "event": "enter"
    },
    {
      "type": "zone_event",
      "id": "leave_cave",
      "entity": ["player.avatar"],
      "zone": "dark_cave",
      "event": "leave"
    }
  ],
  "actions": [{
    "type": "branch",
    "if": {
      "type": "trigger_fired",
      "trigger_id": ["enter_cave"]
    },
    "then": [{
      "type": "parallel",
      "actions": [
        {
          "type": "service_call",
          "service": "lights.set",
          "data": {"entity": ["torch"], "brightness_0_to_255": 255}
        },
        {
          "type": "service_call",
          "service": "audio.play",
          "data": {"sound": "torch_ignite", "volume_0_to_1": 0.8}
        },
        {
          "type": "service_call",
          "service": "camera.set_exposure",
          "data": {"exposure_0_to_10": 1.5}
        }
      ]
    }],
    "else": [{
      "type": "service_call",
      "service": "lights.set",
      "data": {"entity": ["torch"], "brightness_0_to_255": 0}
    }]
  }]
}
```

### 3. Health Warning Loop
```json
{
  "schema_version": "3.1.0",
  "id": "low_health_warning",
  "mode": "single",
  "triggers": [{
    "type": "numeric_threshold",
    "entity": ["player.health"],
    "below": 30,
    "for_ms_0_to_60000": 1000
  }],
  "conditions": [{
    "type": "state_equals",
    "entity": ["player.status"],
    "equals": ["alive"]
  }],
  "actions": [{
    "type": "repeat_until",
    "condition": {
      "type": "numeric_compare",
      "entity": ["player.health"],
      "above": 30
    },
    "max_iterations_1_to_1000": 100,
    "actions": [
      {
        "type": "parallel",
        "actions": [
          {
            "type": "service_call",
            "service": "ui.show_warning",
            "data": {"text": "Health Critical: ${player.health}%", "color": "#FF0000"}
          },
          {
            "type": "service_call",
            "service": "haptics.pattern",
            "data": {"hand": "both", "pattern": "heartbeat"}
          },
          {
            "type": "service_call",
            "service": "vfx.screen_effect",
            "data": {"effect": "blood_vignette", "intensity_0_to_1": 0.7}
          }
        ]
      },
      {
        "type": "wait_duration",
        "duration_ms_0_to_60000": 3000
      }
    ]
  }]
}
```

### 4. Gesture Pattern (Double Tap)
```json
{
  "schema_version": "3.1.0",
  "id": "double_tap_menu",
  "mode": "single",
  "triggers": [{
    "type": "pattern",
    "within_ms_10_to_5000": 400,
    "sequence": [
      {"event": "xr.button.press", "data": {"button": "primary", "hand": "right"}},
      {"event": "xr.button.press", "data": {"button": "primary", "hand": "right"}}
    ]
  }],
  "actions": [
    {
      "type": "service_call",
      "service": "ui.toggle_menu",
      "data": {"menu": "quick_actions"}
    },
    {
      "type": "service_call",
      "service": "haptics.pulse",
      "data": {"hand": "right", "amplitude_0_to_1": 0.3, "duration_ms_10_to_5000": 30}
    }
  ]
}
```

### 5. Periodic Telemetry
```json
{
  "schema_version": "3.1.0",
  "id": "telemetry_reporter",
  "mode": "single",
  "triggers": [{
    "type": "time_schedule",
    "every_minutes": 5
  }],
  "conditions": [{
    "type": "numeric_compare",
    "entity": ["sensor.active_players"],
    "above": 0
  }],
  "actions": [{
    "type": "service_call",
    "service": "http.post",
    "data": {
      "url": "https://telemetry.example.com/metrics",
      "body": {
        "timestamp": "${system.time}",
        "fps": "${sensor.fps}",
        "players": "${sensor.active_players}",
        "memory_mb": "${sensor.memory_usage}"
      },
      "timeout_ms": 10000
    }
  }]
}
```

### 6. Quest Portal with Timeout
```json
{
  "schema_version": "3.1.0",
  "id": "quest_portal_handler",
  "mode": "parallel",
  "max_instances_1_to_10": 3,
  "variables": {
    "portal_id": ""
  },
  "triggers": [{
    "type": "event",
    "name": "quest.objective.complete",
    "filter": {"quest": "ancient_artifact", "objective": "collect_fragments"}
  }],
  "actions": [
    {
      "type": "set_variable",
      "name": "portal_id",
      "value": "${system.generate_id()}"
    },
    {
      "type": "service_call",
      "service": "objects.spawn",
      "data": {
        "prefab": "magic_portal",
        "id": "${portal_id}",
        "position": "${marker.temple_entrance}"
      }
    },
    {
      "type": "wait_condition",
      "condition": {
        "type": "zone_event",
        "entity": ["player.avatar"],
        "zone": "portal_area",
        "event": "enter"
      },
      "timeout_ms_0_to_600000": 300000,
      "on_timeout": "continue"
    },
    {
      "type": "branch",
      "if": {
        "type": "zone_event",
        "entity": ["player.avatar"],
        "zone": "portal_area",
        "event": "enter"
      },
      "then": [{
        "type": "service_call",
        "service": "scene.transition",
        "data": {"to": "temple_interior", "effect": "fade"}
      }],
      "else": [
        {
          "type": "service_call",
          "service": "ui.show_message",
          "data": {"text": "The portal fades away...", "duration_ms": 3000}
        },
        {
          "type": "service_call",
          "service": "objects.destroy",
          "data": {"id": "${portal_id}"}
        }
      ]
    }
  ]
}
```

### 7. Performance Auto-Optimizer
```json
{
  "schema_version": "3.1.0",
  "id": "fps_optimizer",
  "mode": "single",
  "variables": {
    "quality_level": 3,
    "min_fps": 60
  },
  "triggers": [{
    "type": "numeric_threshold",
    "entity": ["sensor.fps"],
    "below": 60,
    "for_ms_0_to_60000": 5000
  }],
  "conditions": [
    {
      "type": "state_equals",
      "entity": ["settings.auto_optimize"],
      "equals": ["enabled"]
    },
    {
      "type": "numeric_compare",
      "entity": ["quality_level"],
      "above": 0
    }
  ],
  "actions": [{
    "type": "repeat_until",
    "condition": {
      "type": "any_of",
      "conditions": [
        {
          "type": "numeric_compare",
          "entity": ["sensor.fps"],
          "above": 65
        },
        {
          "type": "expression",
          "expr": "quality_level <= 0"
        }
      ]
    },
    "max_iterations_1_to_1000": 5,
    "actions": [
      {
        "type": "set_variable",
        "name": "quality_level",
        "value": "${quality_level - 1}"
      },
      {
        "type": "service_call",
        "service": "graphics.set_quality",
        "data": {"level": "${quality_level}"}
      },
      {
        "type": "wait_duration",
        "duration_ms_0_to_60000": 2000
      }
    ]
  }]
}
```

### 8. Combat Combo System
```json
{
  "schema_version": "3.1.0",
  "id": "combat_combo",
  "mode": "restart",
  "variables": {
    "combo_count": 0
  },
  "triggers": [{
    "type": "pattern",
    "within_ms_10_to_5000": 800,
    "sequence": [
      {"event": "combat.attack", "data": {"type": "light"}},
      {"event": "combat.attack", "data": {"type": "light"}},
      {"event": "combat.attack", "data": {"type": "heavy"}}
    ]
  }],
  "conditions": [{
    "type": "numeric_compare",
    "entity": ["player.stamina"],
    "above": 20
  }],
  "actions": [
    {
      "type": "set_variable",
      "name": "combo_count",
      "value": "${combo_count + 1}"
    },
    {
      "type": "parallel",
      "actions": [
        {
          "type": "service_call",
          "service": "combat.deal_damage",
          "data": {
            "amount": "${50 * (1 + combo_count * 0.5)}",
            "type": "combo_finisher"
          }
        },
        {
          "type": "service_call",
          "service": "vfx.spawn",
          "data": {"effect": "combo_explosion", "scale": "${1 + combo_count * 0.2}"}
        },
        {
          "type": "service_call",
          "service": "haptics.pattern",
          "data": {"hand": "both", "pattern": "combo_success"}
        }
      ]
    }
  ]
}
```

## Key Design Decisions

1. **Standard ECA Pattern**: Using trigger/condition/action for LLM familiarity
2. **No Binary Format**: Direct JSON to runtime for simplicity
3. **JSON Manifests**: Avoiding YAML reduces parser dependencies
4. **AST Intermediate**: Type safety without binary compilation
5. **Explicit Everything**: No implicit defaults or behaviors
6. **Arrays Always**: Consistency over convenience
7. **Units in Names**: Self-documenting fields

This implementation provides a robust, LLM-friendly rules engine that achieves deterministic performance on Quest VR while maintaining simplicity and debuggability.