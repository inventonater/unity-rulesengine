# Unity Rules Engine for Quest VR - Final Implementation Plan

## Executive Summary

This implementation delivers a high-performance Event-Condition-Action (ECA) automation system for Quest VR applications, optimized for LLM-generated rules while maintaining deterministic execution under strict performance constraints. The architecture employs a standalone .NET Standard 2.1 core with Unity adapter, targeting <0.5ms rule evaluation at 72-120Hz.

**Core Pattern**: Event-Condition-Action compiled into explicit per-rule finite state machines
**Target Performance**: <0.5ms per rule evaluation, zero allocations in hot path
**Schema**: Compact JSON authoring surface with typed expression DSL
**Threading**: Single-threaded deterministic execution with cooperative cancellation

## Architecture Overview

### Core Engine (.NET Standard 2.1)
- **RuleCompiler**: JSON → validated AST → optimized execution plan
- **EventBus**: Lock-free ring buffer with indexed routing
- **EntityRegistry**: Typed attribute store with change notifications  
- **ServiceRegistry**: Validated action dispatching with thread affinity
- **ExpressionEngine**: Tiny typed DSL interpreter (IL2CPP safe)
- **Scheduler**: Timer wheel for temporal operations
- **Runtime**: Per-rule FSM executor with mode semantics

### Unity Adapter
- **Input Bridge**: Meta XR → canonical events (buttons, gestures, poses)
- **Spatial System**: Zone definitions with non-alloc collision queries
- **Service Implementations**: Haptics, audio, UI, scene management, HTTP
- **Threading Bridge**: Main thread marshaling for Unity API calls
- **Development Tools**: Hot reload, rule debugger, performance profiler

### Rule Schema (v1.0)

```json
{
  "schema_version": 1,
  "id": "unique_rule_id",
  "alias": "Human readable name",
  "mode": "single|restart|queued|parallel",
  "max": 10,
  "variables": {},
  "triggers": [/* Trigger definitions */],
  "conditions": [/* Optional condition checks */],
  "actions": [/* Action sequence */],
  "capabilities": ["http", "zones", "haptics"]
}
```

#### Trigger Types
```json
// State change detection
{
  "trigger": "state",
  "entity": "player.health",
  "from": ["ok"], 
  "to": ["low"],
  "for": "2s",
  "id": "health_drop"
}

// Numeric threshold crossing
{
  "trigger": "numeric",
  "entity": "sensor.temperature", 
  "above": 28.5,
  "for": "30s"
}

// Discrete events
{
  "trigger": "event",
  "type": "xr.button_press",
  "match": {"hand": "right", "button": "primary"}
}

// Spatial zones
{
  "trigger": "zone",
  "entity": "player.avatar",
  "zone": "danger_area", 
  "event": "enter"
}

// Temporal patterns
{
  "trigger": "time",
  "at": "18:30:00",
  "pattern": {"minutes": "*/5"}
}

// Complex sequences
{
  "trigger": "sequence",
  "steps": [
    {"event": {"type": "xr.button_press", "match": {"button": "primary"}}},
    {"event": {"type": "xr.button_press", "match": {"button": "primary"}}}
  ],
  "within": "500ms"
}
```

#### Conditions
```json
{"condition": "state", "entity": "game.mode", "is": ["playing"]}
{"condition": "numeric", "entity": "player.health", "above": 30}
{"condition": "time", "after": "08:00", "before": "20:00"}
{"condition": "expr", "expr": "health > 30 && mode == 'playing'"}
{"condition": "and|or|not", "conditions": [/* nested */]}
```

#### Actions
```json
{"action": "call", "service": "haptics.pulse", "data": {"hand": "right", "intensity": 0.8}}
{"action": "wait", "for": "1s"}
{"action": "wait", "until": {"condition": "state", "entity": "door.open", "is": true}}
{"action": "choose", "when": [{"if": [...], "do": [...]}], "else": [...]}
{"action": "parallel", "do": [/* concurrent actions */]}
{"action": "vars", "set": {"counter": "${vars.counter + 1}"}}
{"action": "stop", "reason": "Objective complete"}
```

## Core Implementation Details

### Memory Management
```csharp
public class RuleEngineMemory
{
    // Pre-allocated pools
    private readonly ObjectPool<RuleContext> contextPool;
    private readonly NativeArray<RuleData> ruleBuffer;
    private readonly NativeQueue<EngineEvent> eventQueue;
    
    // Fixed capacity configuration
    private const int MAX_RULES = 1000;
    private const int MAX_EVENTS_PER_FRAME = 500;
    private const int CONTEXT_POOL_SIZE = 20;
}

[StructLayout(LayoutKind.Sequential)]
public struct EngineEvent
{
    public int EventTypeId;    // Pre-resolved at compile time
    public int EntityId;       // Pre-resolved entity reference
    public long Timestamp;     // Monotonic milliseconds
    public int DataA, DataB;   // Packed payload
}
```

### Rule Compilation Pipeline
```csharp
public class RuleCompiler
{
    public CompiledRule Compile(RuleDefinition definition)
    {
        // 1. Validate schema and resolve references
        ValidateSchema(definition);
        var entityMap = ResolveEntities(definition);
        var serviceMap = ResolveServices(definition);
        
        // 2. Build trigger subscriptions with options
        var triggers = CompileTriggers(definition.Triggers, entityMap);
        
        // 3. Precompile conditions to fast predicates
        var conditions = CompileConditions(definition.Conditions, entityMap);
        
        // 4. Build action execution plan
        var actions = CompileActions(definition.Actions, serviceMap);
        
        // 5. Generate FSM with mode semantics
        return new CompiledRule
        {
            Id = definition.Id,
            Mode = definition.Mode,
            StateMachine = BuildStateMachine(triggers, conditions, actions),
            Subscriptions = BuildSubscriptions(triggers)
        };
    }
}
```

### Expression Engine
```csharp
public class ExpressionEngine
{
    // IL2CPP-safe expression evaluation
    public bool EvaluateBoolean(CompiledExpression expr, EvalContext context)
    {
        return expr.Evaluator(context);
    }
    
    // Compile-time expression parsing
    public CompiledExpression Compile(string expression)
    {
        var tokens = Tokenize(expression);
        var ast = Parse(tokens);
        var evaluator = GenerateEvaluator(ast);
        
        return new CompiledExpression { Evaluator = evaluator };
    }
    
    // Safe function library
    private static readonly Dictionary<string, Func<object[], object>> Functions = new()
    {
        ["state"] = args => GetEntityState((string)args[0]),
        ["attr"] = args => GetEntityAttribute((string)args[0], (string)args[1]),
        ["now"] = args => DateTime.UtcNow.ToString("O"),
        ["clamp"] = args => Math.Max((float)args[1], Math.Min((float)args[0], (float)args[2]))
    };
}
```

### Runtime Execution
```csharp
public class RuleRuntime
{
    public void Tick(float deltaTime)
    {
        // 1. Process incoming events
        ProcessEventQueue();
        
        // 2. Advance timer wheel
        timerWheel.Advance(clock.Now);
        
        // 3. Execute ready actions
        ExecutePendingActions();
        
        // 4. Update rule state machines
        UpdateRuleStates();
    }
    
    private void ProcessEventQueue()
    {
        while (eventQueue.TryDequeue(out var evt))
        {
            // Route to subscribed rules via precomputed index
            if (subscriptionIndex.TryGetValue(evt.EventTypeId, out var subscribers))
            {
                foreach (var ruleId in subscribers)
                {
                    EvaluateRule(ruleId, evt);
                }
            }
        }
    }
}
```

## Unity Adapter Implementation

### Input Bridge
```csharp
public class XRInputBridge : MonoBehaviour
{
    private RuleEngine ruleEngine;
    
    void Update()
    {
        // Meta XR Controller Input
        CheckControllerInput(OVRInput.Controller.RTouch, "right");
        CheckControllerInput(OVRInput.Controller.LTouch, "left");
        
        // Hand Tracking
        if (OVRPlugin.GetHandState(OVRPlugin.Step.Render, OVRPlugin.Hand.HandRight, out var handState))
        {
            DetectGestures(handState, "right");
        }
    }
    
    private void CheckControllerInput(OVRInput.Controller controller, string hand)
    {
        // Primary button state changes
        bool primaryPressed = OVRInput.GetDown(OVRInput.Button.One, controller);
        if (primaryPressed)
        {
            ruleEngine.EmitEvent("xr.button_press", new Dictionary<string, object>
            {
                ["hand"] = hand,
                ["button"] = "primary",
                ["timestamp"] = Time.timeAsDouble
            });
        }
        
        // Trigger pressure
        float triggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
        ruleEngine.UpdateEntity($"xr.controller.{hand}.trigger", triggerValue);
    }
}
```

### Spatial System
```csharp
public class SpatialZoneManager : MonoBehaviour
{
    private readonly Dictionary<string, ZoneDefinition> zones = new();
    private readonly Dictionary<int, HashSet<string>> entityZones = new();
    
    public struct ZoneDefinition
    {
        public string Id;
        public Collider Collider;
        public HashSet<int> CurrentOccupants;
    }
    
    void FixedUpdate()
    {
        // Non-allocating overlap queries
        foreach (var (zoneId, zone) in zones)
        {
            var previousOccupants = new HashSet<int>(zone.CurrentOccupants);
            zone.CurrentOccupants.Clear();
            
            // Query current occupants
            int hitCount = Physics.OverlapBoxNonAlloc(
                zone.Collider.bounds.center,
                zone.Collider.bounds.extents,
                overlapBuffer,
                zone.Collider.transform.rotation
            );
            
            for (int i = 0; i < hitCount; i++)
            {
                if (trackedEntities.TryGetValue(overlapBuffer[i], out int entityId))
                {
                    zone.CurrentOccupants.Add(entityId);
                    
                    // Emit enter event if new
                    if (!previousOccupants.Contains(entityId))
                    {
                        ruleEngine.EmitEvent("zone.enter", new Dictionary<string, object>
                        {
                            ["entity"] = entityId,
                            ["zone"] = zoneId
                        });
                    }
                }
            }
            
            // Emit leave events
            foreach (int entityId in previousOccupants)
            {
                if (!zone.CurrentOccupants.Contains(entityId))
                {
                    ruleEngine.EmitEvent("zone.leave", new Dictionary<string, object>
                    {
                        ["entity"] = entityId,
                        ["zone"] = zoneId
                    });
                }
            }
        }
    }
}
```

### Service Registry
```csharp
public class UnityServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<string, ServiceDefinition> services = new();
    
    public void RegisterService<T>(string name, Func<T, UniTask> handler, ServiceSchema schema)
    {
        services[name] = new ServiceDefinition
        {
            Name = name,
            Handler = async (data) => await handler((T)data),
            Schema = schema,
            ThreadAffinity = ThreadAffinity.MainThread
        };
    }
    
    public async UniTask InvokeService(string serviceName, object data)
    {
        if (!services.TryGetValue(serviceName, out var service))
            throw new InvalidOperationException($"Service not found: {serviceName}");
            
        // Validate arguments against schema
        service.Schema.Validate(data);
        
        // Ensure main thread execution
        if (service.ThreadAffinity == ThreadAffinity.MainThread)
        {
            await UniTask.SwitchToMainThread();
        }
        
        await service.Handler(data);
    }
}

// Example service implementations
public class HapticsService
{
    public async UniTask Pulse(HapticPulseData data)
    {
        var controller = data.Hand == "right" ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
        
        OVRInput.SetControllerVibration(
            data.Intensity, 
            data.Intensity, 
            controller
        );
        
        await UniTask.Delay(data.DurationMs);
        OVRInput.SetControllerVibration(0, 0, controller);
    }
}
```

## Performance Optimizations

### Timer Wheel Implementation
```csharp
public class TimerWheel
{
    private readonly TimerSlot[] slots;
    private readonly int slotCount;
    private readonly long tickMs;
    private long currentTick;
    
    public TimerWheel(int slots = 512, long tickMs = 10)
    {
        this.slots = new TimerSlot[slots];
        this.slotCount = slots;
        this.tickMs = tickMs;
    }
    
    public TimerHandle Schedule(long delayMs, Action callback)
    {
        long targetTick = currentTick + (delayMs / tickMs);
        int slotIndex = (int)(targetTick % slotCount);
        
        var timer = new Timer { Callback = callback, TargetTick = targetTick };
        slots[slotIndex].Timers.Add(timer);
        
        return new TimerHandle(timer);
    }
    
    public void Advance(long nowMs)
    {
        long newTick = nowMs / tickMs;
        
        while (currentTick < newTick)
        {
            currentTick++;
            int slotIndex = (int)(currentTick % slotCount);
            
            var slot = slots[slotIndex];
            for (int i = slot.Timers.Count - 1; i >= 0; i--)
            {
                var timer = slot.Timers[i];
                if (timer.TargetTick <= currentTick)
                {
                    timer.Callback();
                    slot.Timers.RemoveAt(i);
                }
            }
        }
    }
}
```

### Zero-Allocation Event Processing
```csharp
public class EventBus
{
    private readonly RingBuffer<EngineEvent> eventBuffer;
    private readonly Dictionary<int, List<int>> subscriptions;
    
    // Struct-based events to avoid allocations
    public void Emit(int eventTypeId, int entityId, int dataA = 0, int dataB = 0)
    {
        var evt = new EngineEvent
        {
            EventTypeId = eventTypeId,
            EntityId = entityId,
            Timestamp = clock.NowMs,
            DataA = dataA,
            DataB = dataB
        };
        
        if (!eventBuffer.TryEnqueue(evt))
        {
            // Handle overflow - either drop or warn
            diagnostics.RecordDroppedEvent(eventTypeId);
        }
    }
    
    public void ProcessEvents(Action<EngineEvent, ReadOnlySpan<int>> handler)
    {
        while (eventBuffer.TryDequeue(out var evt))
        {
            if (subscriptions.TryGetValue(evt.EventTypeId, out var subscribers))
            {
                handler(evt, CollectionsMarshal.AsSpan(subscribers));
            }
        }
    }
}
```

## Curated Examples

### 1. Basic Haptic Feedback
```json
{
  "schema_version": 1,
  "id": "basic_haptic_feedback",
  "alias": "Button press haptics",
  "mode": "single",
  "triggers": [
    {
      "trigger": "event",
      "type": "xr.button_press",
      "match": {"hand": "right", "button": "primary"}
    }
  ],
  "actions": [
    {
      "action": "call",
      "service": "haptics.pulse",
      "data": {
        "hand": "right",
        "intensity": 0.6,
        "duration_ms": 50
      }
    }
  ],
  "capabilities": ["haptics"]
}
```

### 2. Zone-Based Lighting Control
```json
{
  "schema_version": 1,
  "id": "cave_lighting_system",
  "alias": "Dynamic cave lighting",
  "mode": "restart",
  "triggers": [
    {
      "trigger": "zone",
      "entity": "player.avatar",
      "zone": "dark_cave",
      "event": "enter",
      "id": "cave_enter"
    },
    {
      "trigger": "zone", 
      "entity": "player.avatar",
      "zone": "dark_cave",
      "event": "leave",
      "id": "cave_exit"
    }
  ],
  "actions": [
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "trigger", "id": "cave_enter"}],
          "do": [
            {
              "action": "parallel",
              "do": [
                {
                  "action": "call",
                  "service": "lighting.set_preset",
                  "data": {"preset": "torch_lit", "transition_ms": 2000}
                },
                {
                  "action": "call",
                  "service": "audio.play_spatial",
                  "data": {"sound": "torch_ignite", "position": "player", "volume": 0.8}
                },
                {
                  "action": "call",
                  "service": "haptics.pulse",
                  "data": {"hand": "both", "intensity": 0.3, "duration_ms": 100}
                }
              ]
            }
          ]
        },
        {
          "if": [{"condition": "trigger", "id": "cave_exit"}],
          "do": [
            {
              "action": "call",
              "service": "lighting.set_preset", 
              "data": {"preset": "daylight", "transition_ms": 1500}
            }
          ]
        }
      ]
    }
  ],
  "capabilities": ["zones", "haptics"]
}
```

### 3. Health Monitoring with Escalation
```json
{
  "schema_version": 1,
  "id": "health_monitor",
  "alias": "Low health warning system",
  "mode": "single",
  "variables": {
    "warning_threshold": 30,
    "critical_threshold": 15,
    "alert_interval_ms": 3000
  },
  "triggers": [
    {
      "trigger": "numeric",
      "entity": "player.health",
      "below": 30,
      "for": "1s",
      "id": "health_low"
    }
  ],
  "conditions": [
    {"condition": "state", "entity": "player.status", "is": "alive"},
    {"condition": "state", "entity": "game.mode", "is": ["playing", "combat"]}
  ],
  "actions": [
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "numeric", "entity": "player.health", "below": 15}],
          "do": [
            {
              "action": "parallel",
              "do": [
                {
                  "action": "call",
                  "service": "ui.show_warning",
                  "data": {
                    "message": "CRITICAL: Health at ${state('player.health')}%",
                    "color": "#FF0000",
                    "duration_ms": 2000,
                    "pulse": true
                  }
                },
                {
                  "action": "call",
                  "service": "haptics.pattern",
                  "data": {"hand": "both", "pattern": "urgent_pulse"}
                },
                {
                  "action": "call",
                  "service": "audio.play",
                  "data": {"sound": "heartbeat_critical", "volume": 0.9}
                }
              ]
            }
          ]
        }
      ],
      "else": [
        {
          "action": "call",
          "service": "ui.show_notification",
          "data": {
            "message": "Health Low: ${state('player.health')}%",
            "color": "#FFA500", 
            "duration_ms": 1500
          }
        }
      ]
    },
    {"action": "wait", "for": "${vars.alert_interval_ms}ms"},
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "numeric", "entity": "player.health", "above": 29}],
          "do": [{"action": "stop", "reason": "Health recovered"}]
        }
      ],
      "else": [
        {
          "action": "call",
          "service": "rule.restart",
          "data": {"rule_id": "health_monitor"}
        }
      ]
    }
  ]
}
```

### 4. Double-Tap Gesture Recognition
```json
{
  "schema_version": 1,
  "id": "double_tap_gesture",
  "alias": "Right controller double-tap",
  "mode": "single", 
  "triggers": [
    {
      "trigger": "sequence",
      "steps": [
        {
          "event": {
            "type": "xr.button_press",
            "match": {"hand": "right", "button": "primary"}
          }
        },
        {
          "event": {
            "type": "xr.button_press", 
            "match": {"hand": "right", "button": "primary"}
          }
        }
      ],
      "within": "400ms",
      "id": "double_tap"
    }
  ],
  "conditions": [
    {"condition": "state", "entity": "ui.menu_open", "is": false}
  ],
  "actions": [
    {
      "action": "parallel",
      "do": [
        {
          "action": "call",
          "service": "ui.toggle_quick_menu",
          "data": {"position": "controller_right"}
        },
        {
          "action": "call",
          "service": "haptics.pattern",
          "data": {"hand": "right", "pattern": "double_click_confirm"}
        },
        {
          "action": "call",
          "service": "audio.play",
          "data": {"sound": "ui_menu_open", "volume": 0.7}
        }
      ]
    }
  ],
  "capabilities": ["haptics"]
}
```

### 5. Quest Progression with Timeout
```json
{
  "schema_version": 1,
  "id": "artifact_quest_progression", 
  "alias": "Ancient artifact quest flow",
  "mode": "parallel",
  "max": 3,
  "variables": {
    "portal_timeout_ms": 300000,
    "fragment_count": 0
  },
  "triggers": [
    {
      "trigger": "event",
      "type": "quest.objective_complete",
      "match": {"quest_id": "ancient_artifact"}
    }
  ],
  "actions": [
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "expr", "expr": "trigger.data.objective == 'collect_fragments'"}],
          "do": [
            {
              "action": "call",
              "service": "ui.show_notification",
              "data": {
                "title": "Fragments Collected!",
                "message": "All three ancient fragments obtained",
                "duration_ms": 4000,
                "type": "success"
              }
            },
            {
              "action": "call",
              "service": "object.spawn",
              "data": {
                "prefab": "ancient_portal",
                "position": "${state('location.temple_entrance')}",
                "effect": "mystical_appear"
              }
            },
            {
              "action": "wait",
              "until": {
                "condition": "zone",
                "entity": "player.avatar", 
                "zone": "portal_area",
                "event": "enter"
              },
              "timeout": "${vars.portal_timeout_ms}ms"
            },
            {
              "action": "choose",
              "when": [
                {
                  "if": [{"condition": "zone", "entity": "player.avatar", "zone": "portal_area", "occupied": true}],
                  "do": [
                    {
                      "action": "call",
                      "service": "scene.transition",
                      "data": {
                        "target_scene": "ancient_temple_interior",
                        "transition_type": "portal_warp",
                        "duration_ms": 2000
                      }
                    }
                  ]
                }
              ],
              "else": [
                {
                  "action": "call",
                  "service": "ui.show_hint",
                  "data": {
                    "message": "The portal shimmers and fades away...",
                    "direction": "temple_entrance"
                  }
                },
                {
                  "action": "call",
                  "service": "object.destroy",
                  "data": {"object": "ancient_portal", "effect": "fade_away"}
                }
              ]
            }
          ]
        },
        {
          "if": [{"condition": "expr", "expr": "trigger.data.objective == 'solve_temple_puzzle'"}],
          "do": [
            {
              "action": "parallel",
              "do": [
                {
                  "action": "call",
                  "service": "quest.complete",
                  "data": {
                    "quest_id": "ancient_artifact",
                    "rewards": {"xp": 5000, "gold": 1000, "items": ["legendary_staff"]}
                  }
                },
                {
                  "action": "call",
                  "service": "achievement.unlock",
                  "data": {"achievement": "artifact_master"}
                },
                {
                  "action": "call",
                  "service": "cutscene.play",
                  "data": {"cutscene": "artifact_revelation"}
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### 6. Performance Auto-Optimizer
```json
{
  "schema_version": 1,
  "id": "performance_optimizer",
  "alias": "Dynamic quality adjustment",
  "mode": "single",
  "variables": {
    "target_fps": 72,
    "min_fps": 60,
    "optimization_steps": 0,
    "max_steps": 3
  },
  "triggers": [
    {
      "trigger": "numeric",
      "entity": "performance.average_fps",
      "below": 60,
      "for": "5s"
    }
  ],
  "conditions": [
    {"condition": "state", "entity": "settings.auto_optimize", "is": true},
    {"condition": "expr", "expr": "vars.optimization_steps < vars.max_steps"}
  ],
  "actions": [
    {
      "action": "call",
      "service": "log.warning",
      "data": {
        "message": "Performance below target (${state('performance.average_fps')} fps), optimizing..."
      }
    },
    {
      "action": "vars",
      "set": {"optimization_steps": "${vars.optimization_steps + 1}"}
    },
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "expr", "expr": "vars.optimization_steps == 1"}],
          "do": [
            {
              "action": "call",
              "service": "graphics.reduce_effects",
              "data": {"level": "particle_reduction"}
            }
          ]
        },
        {
          "if": [{"condition": "expr", "expr": "vars.optimization_steps == 2"}],
          "do": [
            {
              "action": "call", 
              "service": "graphics.reduce_quality",
              "data": {"level": "medium"}
            }
          ]
        },
        {
          "if": [{"condition": "expr", "expr": "vars.optimization_steps >= 3"}],
          "do": [
            {
              "action": "call",
              "service": "graphics.reduce_quality", 
              "data": {"level": "low"}
            }
          ]
        }
      ]
    },
    {"action": "wait", "for": "3s"},
    {
      "action": "call",
      "service": "log.info",
      "data": {
        "message": "Optimization step ${vars.optimization_steps} complete. FPS: ${state('performance.average_fps')}"
      }
    }
  ]
}
```

### 7. Contextual Interaction System
```json
{
  "schema_version": 1,
  "id": "smart_interaction",
  "alias": "Context-aware object interaction",
  "mode": "single",
  "triggers": [
    {
      "trigger": "event",
      "type": "xr.grip_squeeze",
      "match": {"hand": "right", "pressure": 0.8}
    }
  ],
  "conditions": [
    {"condition": "numeric", "entity": "interaction.nearest_distance", "below": 1.5}
  ],
  "actions": [
    {
      "action": "choose",
      "when": [
        {
          "if": [{"condition": "state", "entity": "interaction.nearest_object_type", "is": "weapon"}],
          "do": [
            {
              "action": "parallel",
              "do": [
                {
                  "action": "call",
                  "service": "object.attach_to_hand",
                  "data": {
                    "object": "${state('interaction.nearest_object')}",
                    "hand": "right",
                    "attach_point": "grip"
                  }
                },
                {
                  "action": "call",
                  "service": "haptics.pulse",
                  "data": {"hand": "right", "intensity": 0.8, "duration_ms": 100}
                },
                {
                  "action": "call",
                  "service": "audio.play_spatial",
                  "data": {
                    "sound": "weapon_equip",
                    "position": "${state('interaction.nearest_object_position')}"
                  }
                }
              ]
            }
          ]
        },
        {
          "if": [{"condition": "state", "entity": "interaction.nearest_object_type", "is": "lever"}],
          "do": [
            {
              "action": "call",
              "service": "object.activate",
              "data": {"object": "${state('interaction.nearest_object')}"}
            },
            {
              "action": "call",
              "service": "haptics.pattern",
              "data": {"hand": "right", "pattern": "mechanism_click"}
            }
          ]
        },
        {
          "if": [{"condition": "state", "entity": "interaction.nearest_object_type", "is": "door"}],
          "do": [
            {
              "action": "choose",
              "when": [
                {
                  "if": [{"condition": "state", "entity": "interaction.nearest_object.locked", "is": true}],
                  "do": [
                    {
                      "action": "call",
                      "service": "audio.play",
                      "data": {"sound": "door_locked", "volume": 0.8}
                    },
                    {
                      "action": "call",
                      "service": "ui.show_hint",
                      "data": {"message": "This door is locked"}
                    }
                  ]
                }
              ],
              "else": [
                {
                  "action": "call",
                  "service": "object.animate",
                  "data": {
                    "object": "${state('interaction.nearest_object')}",
                    "animation": "door_open"
                  }
                }
              ]
            }
          ]
        }
      ],
      "else": [
        {
          "action": "call",
          "service": "haptics.pulse",
          "data": {"hand": "right", "intensity": 0.2, "duration_ms": 30}
        }
      ]
    }
  ],
  "capabilities": ["haptics"]
}
```

## Development Tooling

### Rule Validator
```csharp
public class RuleValidator
{
    public ValidationResult Validate(RuleDefinition rule)
    {
        var result = new ValidationResult();
        
        // Schema validation
        ValidateSchema(rule, result);
        
        // Entity reference validation
        ValidateEntityReferences(rule, result);
        
        // Service capability validation  
        ValidateServiceCapabilities(rule, result);
        
        // Performance estimation
        EstimatePerformance(rule, result);
        
        // Anti-pattern detection
        DetectAntiPatterns(rule, result);
        
        return result;
    }
    
    private void DetectAntiPatterns(RuleDefinition rule, ValidationResult result)
    {
        // Warn about missing cooldowns on high-frequency triggers
        foreach (var trigger in rule.Triggers)
        {
            if (trigger.Type == "event" && !trigger.Options.CooldownMs.HasValue)
            {
                result.AddWarning("Consider adding cooldown to prevent trigger spam");
            }
        }
        
        // Warn about unbounded parallel execution
        if (rule.Mode == "parallel" && rule.Max > 10)
        {
            result.AddWarning("High parallel limit may impact performance");
        }
    }
}
```

### Hot Reload System
```csharp
public class RuleHotReload
{
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    
    public async UniTask EnableHotReload(string rulesPath)
    {
        var watcher = new FileSystemWatcher(rulesPath)
        {
            Filter = "*.json",
            EnableRaisingEvents = true
        };
        
        watcher.Changed += async (sender, e) =>
        {
            await UniTask.Delay(100); // Debounce
            await ReloadRule(e.FullPath);
        };
    }
    
    private async UniTask ReloadRule(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var rule = JsonSerializer.Deserialize<RuleDefinition>(content);
            
            var validation = validator.Validate(rule);
            if (validation.IsValid)
            {
                await ruleEngine.ReplaceRule(rule);
                Debug.Log($"Hot reloaded rule: {rule.Id}");
            }
            else
            {
                Debug.LogError($"Hot reload failed: {validation.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Hot reload error: {ex.Message}");
        }
    }
    
    #endif
}
```

## Performance Benchmarks & Success Criteria

### Target Metrics
| Metric | Target | Validation Method |
|--------|--------|------------------|
| Rule Evaluation Time | <0.5ms p95 | Device profiling |
| Memory Allocation | 0 bytes/frame steady state | Unity Profiler |
| Concurrent Rules | 100+ without degradation | Stress testing |
| Hot Reload Time | <200ms | Development tooling |
| LLM Generation Success | >90% valid rules | Schema compliance |

### Device Compatibility
- **Quest 2**: 72Hz stable with 75 rules
- **Quest 3**: 90Hz stable with 100 rules  
- **Quest Pro**: 120Hz stable with 80 rules

### Memory Budget
- **Core Engine**: 16MB maximum
- **Unity Adapter**: 8MB maximum
- **Rule Data**: 1KB per rule average
- **Runtime Pools**: 4MB pre-allocated

## Risk Mitigation

### Technical Risks
1. **IL2CPP Compatibility**: Visitor pattern + extensive testing
2. **Performance Degradation**: Timer wheel + zero-alloc design
3. **Memory Fragmentation**: Pre-allocated pools + fixed buffers
4. **Quest Thermal Limits**: Built-in performance monitoring

### Development Risks  
1. **Schema Evolution**: Versioned migration system
2. **Complex Rule Debugging**: Comprehensive tracing + visualization
3. **LLM Integration Failures**: Extensive validation + error recovery

## Implementation Timeline

### Phase 1: Core Foundation (Weeks 1-3)
- [ ] Core engine architecture
- [ ] JSON schema + validation  
- [ ] Expression engine
- [ ] Basic trigger/condition/action support
- [ ] Unit test framework

### Phase 2: Unity Integration (Weeks 4-6)
- [ ] Unity adapter implementation
- [ ] XR input bridge
- [ ] Spatial zone system
- [ ] Service registry + core services
- [ ] Editor tooling

### Phase 3: Advanced Features (Weeks 7-8)
- [ ] Timer wheel optimization
- [ ] Hot reload system
- [ ] Performance profiling
- [ ] Device testing + optimization

### Phase 4: Polish & Documentation (Weeks 9-10)  
- [ ] Comprehensive examples library
- [ ] Developer documentation
- [ ] Performance benchmarking
- [ ] Release preparation

This implementation plan provides a robust foundation for a production-ready rules engine that meets Quest VR's demanding performance requirements while maintaining the flexibility needed for LLM-generated automation rules.