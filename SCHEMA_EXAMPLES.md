// ================================================================
// XR/UNITY AUTOMATION SCHEMA SPECIFICATION
// Based on Home Assistant format for optimal LLM generation
// ================================================================

// AUTOMATION STRUCTURE
{
  "id": "string",                    // Required: Unique identifier
  "alias": "string",                  // Optional: Human-readable name
  "description": "string",            // Optional: Detailed description
  "mode": "single|restart|queued|parallel", // Optional: Execution mode (default: single)
  "max": "integer",                   // Optional: Max parallel/queued runs (default: 10)
  "variables": {                      // Optional: Global variables for the automation
    "key": "value"                    // Can be string, number, boolean, or object
  },
  "triggers": [],                     // Required: Array of trigger objects
  "conditions": [],                   // Optional: Array of condition objects
  "actions": []                       // Required: Array of action objects
}

// ================================================================
// TRIGGER TYPES
// ================================================================

// STATE TRIGGER - Entity state changes
{
  "trigger": "state",
  "entity_id": "string|array",       // Entity or list of entities to monitor
  "from": "string|array",             // Optional: Previous state(s)
  "to": "string|array",               // Optional: New state(s)
  "for": "HH:MM:SS",                 // Optional: Duration before triggering
  "attribute": "string",              // Optional: Monitor attribute instead of state
  "id": "string"                      // Optional: Trigger identifier
}

// EVENT TRIGGER - System or custom events
{
  "trigger": "event",
  "event_type": "string",             // Event name (e.g., "xr.button", "app.started")
  "event_data": {                     // Optional: Required event data to match
    "key": "value"
  },
  "id": "string"
}

// TIME TRIGGER - Time-based
{
  "trigger": "time",
  "at": "HH:MM:SS"                    // Specific time of day
}

// TIME PATTERN TRIGGER - Recurring time
{
  "trigger": "time_pattern",
  "hours": "/integer|integer|*",     // Optional: Hour pattern
  "minutes": "/integer|integer|*",   // Optional: Minute pattern
  "seconds": "/integer|integer|*"    // Optional: Second pattern
}

// NUMERIC STATE TRIGGER - Numeric thresholds
{
  "trigger": "numeric_state",
  "entity_id": "string|array",
  "above": "number",                  // Optional: Triggers when above
  "below": "number",                  // Optional: Triggers when below
  "for": "HH:MM:SS",                 // Optional: Duration requirement
  "id": "string"
}

// TEMPLATE TRIGGER - Custom logic
{
  "trigger": "template",
  "value_template": "string",         // Jinja2 template that returns true/false
  "for": "HH:MM:SS",                 // Optional: Duration requirement
  "id": "string"
}

// ZONE TRIGGER - XR/Unity spatial zones
{
  "trigger": "zone",
  "entity_id": "string|array",        // Entity to track (e.g., "player", "xr.headset")
  "zone": "string",                   // Zone identifier
  "event": "enter|leave",             // Trigger on enter or leave
  "id": "string"
}

// ================================================================
// CONDITION TYPES
// ================================================================

// STATE CONDITION
{
  "condition": "state",
  "entity_id": "string",
  "state": "string|array",            // State(s) that must match
  "attribute": "string",              // Optional: Check attribute instead
  "for": "HH:MM:SS"                  // Optional: Duration requirement
}

// NUMERIC STATE CONDITION
{
  "condition": "numeric_state",
  "entity_id": "string",
  "above": "number",
  "below": "number"
}

// TEMPLATE CONDITION
{
  "condition": "template",
  "value_template": "string"          // Jinja2 template returning true/false
}

// TIME CONDITION
{
  "condition": "time",
  "after": "HH:MM:SS",               // Optional: After this time
  "before": "HH:MM:SS",              // Optional: Before this time
  "weekday": ["mon", "tue", "wed", "thu", "fri", "sat", "sun"]
}

// LOGICAL CONDITIONS
{
  "condition": "and|or",
  "conditions": []                    // Array of condition objects
}

{
  "condition": "not",
  "conditions": []                    // Array to negate
}

// TRIGGER CONDITION
{
  "condition": "trigger",
  "id": "string|array"                // Match specific trigger IDs
}

// ================================================================
// ACTION TYPES
// ================================================================

// SERVICE CALL - Call any service
{
  "action": "domain.service",         // E.g., "haptics.pulse", "log.info"
  "data": {},                         // Service-specific data
  "target": {                         // Optional: Target entities
    "entity_id": "string|array"
  }
}

// DELAY
{
  "action": "delay",
  "delay": "HH:MM:SS|integer"         // Duration or seconds
}

// WAIT FOR TRIGGER
{
  "action": "wait_for_trigger",
  "triggers": [],                     // Array of trigger objects
  "timeout": "HH:MM:SS",              // Optional: Maximum wait time
  "continue_on_timeout": "boolean"    // Optional: Continue if timeout (default: true)
}

// WAIT TEMPLATE
{
  "action": "wait_template",
  "value_template": "string",
  "timeout": "HH:MM:SS",
  "continue_on_timeout": "boolean"
}

// EVENT
{
  "action": "event.fire",
  "data": {
    "event_type": "string",
    "event_data": {}
  }
}

// VARIABLES
{
  "action": "variables",
  "variables": {
    "key": "value"
  }
}

// CHOOSE (if-then-else)
{
  "action": "choose",
  "choices": [
    {
      "conditions": [],                // Array of condition objects
      "sequence": []                   // Array of action objects
    }
  ],
  "default": []                        // Optional: Default actions
}

// REPEAT
{
  "action": "repeat",
  "count": "integer",                 // Optional: Fixed count
  "while": [],                       // Optional: Conditions to continue
  "until": [],                       // Optional: Conditions to stop
  "sequence": []                     // Actions to repeat
}

// PARALLEL
{
  "action": "parallel",
  "parallel": []                       // Array of action objects to run in parallel
}

// IF-THEN-ELSE
{
  "action": "if",
  "if": [],                           // Array of condition objects
  "then": [],                         // Array of action objects
  "else": []                          // Optional: Array of action objects
}

// STOP
{
  "action": "stop",
  "stop": "string",                    // Stop reason
  "error": "boolean"                  // Optional: Mark as error (default: false)
}

// ================================================================
// XR/UNITY SPECIFIC SERVICES
// ================================================================

// Haptics
// "haptics.pulse" - Haptic feedback
// "haptics.pattern" - Pattern-based haptics

// Logging
// "log.debug", "log.info", "log.warning", "log.error"

// HTTP
// "http.get", "http.post", "http.put", "http.delete"

// XR Input
// "xr.recenter" - Recenter XR space
// "xr.teleport" - Teleport player

// Game Systems
// "scene.load" - Load Unity scene
// "object.spawn" - Spawn game object
// "ui.show_notification" - Show UI notification
// "audio.play" - Play sound effect
// "player.set_state" - Change player state

// ================================================================
// TEMPLATING
// ================================================================

// Available variables in templates:
// {{ now() }}                         - Current timestamp
// {{ trigger }}                       - Trigger data object
// {{ trigger.entity_id }}             - Entity that triggered
// {{ trigger.from_state }}            - Previous state
// {{ trigger.to_state }}              - New state
// {{ trigger.event }}                 - Event data
// {{ trigger.payload }}               - Event payload
// {{ states('entity.id') }}           - Get entity state
// {{ state_attr('entity.id', 'attr') }} - Get entity attribute
// {{ variables.var_name }}            - Access defined variables

// ================================================================
// CURATED EXAMPLES
// ================================================================

// ----------------------------------------------------------------
// EXAMPLE 1: Minimal - Log on App Start
// Demonstrates: Basic event trigger and logging action
// ----------------------------------------------------------------
{
  "id": "app_startup",
  "alias": "Log application startup",
  "triggers": [
    {
      "trigger": "event",
      "event_type": "app.started"
    }
  ],
  "actions": [
    {
      "action": "log.info",
      "data": {
        "message": "Application started at {{ now() }}"
      }
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 2: XR Button with State Tracking
// Demonstrates: State trigger, entity monitoring, haptic feedback
// ----------------------------------------------------------------
{
  "id": "xr_button_handler",
  "alias": "Right controller primary button",
  "mode": "single",
  "triggers": [
    {
      "trigger": "state",
      "entity_id": "xr.controller.right.button_primary",
      "to": "pressed",
      "id": "button_press"
    },
    {
      "trigger": "state",
      "entity_id": "xr.controller.right.button_primary",
      "to": "released",
      "for": "00:00:02",
      "id": "long_release"
    }
  ],
  "conditions": [
    {
      "condition": "state",
      "entity_id": "game.mode",
      "state": ["playing", "paused"]
    }
  ],
  "actions": [
    {
      "action": "choose",
      "choices": [
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "button_press"
            }
          ],
          "sequence": [
            {
              "action": "haptics.pulse",
              "data": {
                "hand": "right",
                "amplitude": 0.5,
                "duration_ms": 50
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "long_release"
            }
          ],
          "sequence": [
            {
              "action": "haptics.pattern",
              "data": {
                "hand": "right",
                "pattern": "double_click"
              }
            }
          ]
        }
      ]
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 3: Temperature Monitoring with HTTP Webhook
// Demonstrates: Numeric state, template conditions, variables, HTTP
// ----------------------------------------------------------------
{
  "id": "temp_monitor",
  "alias": "Temperature threshold alerting",
  "mode": "queued",
  "max": 3,
  "variables": {
    "alert_endpoint": "https://api.example.com/alerts",
    "threshold_celsius": 28.5
  },
  "triggers": [
    {
      "trigger": "numeric_state",
      "entity_id": "sensor.room_temperature",
      "above": 28.5,
      "for": "00:00:30"
    }
  ],
  "conditions": [
    {
      "condition": "template",
      "value_template": "{{ now().hour >= 8 and now().hour <= 20 }}"
    }
  ],
  "actions": [
    {
      "action": "http.post",
      "data": {
        "url": "{{ variables.alert_endpoint }}",
        "headers": {
          "Content-Type": "application/json",
          "X-Source": "unity-automation"
        },
        "json": {
          "sensor": "{{ trigger.entity_id }}",
          "temperature": "{{ states('sensor.room_temperature') }}",
          "threshold": "{{ variables.threshold_celsius }}",
          "timestamp": "{{ now().isoformat() }}"
        },
        "timeout": 5
      }
    },
    {
      "action": "ui.show_notification",
      "data": {
        "title": "Temperature Alert",
        "message": "Room temperature is {{ states('sensor.room_temperature') }}°C",
        "type": "warning",
        "duration": 5000
      }
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 4: Player Zone Detection with Adaptive Lighting
// Demonstrates: Zone triggers, parallel actions, state conditions
// ----------------------------------------------------------------
{
  "id": "zone_lighting",
  "alias": "Adaptive zone-based lighting",
  "mode": "restart",
  "triggers": [
    {
      "trigger": "zone",
      "entity_id": "player.avatar",
      "zone": "dark_cave",
      "event": "enter",
      "id": "enter_cave"
    },
    {
      "trigger": "zone",
      "entity_id": "player.avatar",
      "zone": "dark_cave",
      "event": "leave",
      "id": "leave_cave"
    }
  ],
  "actions": [
    {
      "action": "choose",
      "choices": [
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "enter_cave"
            }
          ],
          "sequence": [
            {
              "action": "parallel",
              "parallel": [
                {
                  "action": "light.turn_on",
                  "target": {
                    "entity_id": "light.player_torch"
                  },
                  "data": {
                    "brightness": 255,
                    "transition": 2
                  }
                },
                {
                  "action": "audio.play",
                  "data": {
                    "sound": "torch_ignite",
                    "volume": 0.7
                  }
                },
                {
                  "action": "camera.set_exposure",
                  "data": {
                    "exposure": 1.5,
                    "adaptation_time": 3
                  }
                }
              ]
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "leave_cave"
            }
          ],
          "sequence": [
            {
              "action": "light.turn_off",
              "target": {
                "entity_id": "light.player_torch"
              },
              "data": {
                "transition": 1
              }
            },
            {
              "action": "camera.set_exposure",
              "data": {
                "exposure": 1.0,
                "adaptation_time": 2
              }
            }
          ]
        }
      ]
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 5: Combat System with Health Monitoring
// Demonstrates: Multiple conditions, repeat loops, variables
// ----------------------------------------------------------------
{
  "id": "combat_health",
  "alias": "Low health warning system",
  "mode": "single",
  "triggers": [
    {
      "trigger": "numeric_state",
      "entity_id": "player.health",
      "below": 30,
      "id": "health_critical"
    },
    {
      "trigger": "numeric_state",
      "entity_id": "player.health",
      "below": 50,
      "above": 29,
      "id": "health_low"
    }
  ],
  "conditions": [
    {
      "condition": "state",
      "entity_id": "player.status",
      "state": "alive"
    },
    {
      "condition": "not",
      "conditions": [
        {
          "condition": "state",
          "entity_id": "game.mode",
          "state": "cutscene"
        }
      ]
    }
  ],
  "actions": [
    {
      "action": "choose",
      "choices": [
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "health_critical"
            }
          ],
          "sequence": [
            {
              "action": "repeat",
              "while": [
                {
                  "condition": "numeric_state",
                  "entity_id": "player.health",
                  "below": 30
                }
              ],
              "sequence": [
                {
                  "action": "parallel",
                  "parallel": [
                    {
                      "action": "haptics.pulse",
                      "data": {
                        "hand": "both",
                        "amplitude": 0.8,
                        "duration_ms": 200
                      }
                    },
                    {
                      "action": "vfx.screen_effect",
                      "data": {
                        "effect": "blood_vignette",
                        "intensity": 0.7
                      }
                    },
                    {
                      "action": "audio.play",
                      "data": {
                        "sound": "heartbeat_fast",
                        "volume": 0.9
                      }
                    }
                  ]
                },
                {
                  "action": "delay",
                  "delay": "00:00:01"
                }
              ]
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "trigger",
              "id": "health_low"
            }
          ],
          "sequence": [
            {
              "action": "ui.show_notification",
              "data": {
                "message": "Health Low: {{ states('player.health') }}%",
                "type": "warning",
                "duration": 3000
              }
            },
            {
              "action": "haptics.pulse",
              "data": {
                "hand": "both",
                "amplitude": 0.4,
                "duration_ms": 100
              }
            }
          ]
        }
      ]
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 6: Periodic Telemetry with Conditional Reporting
// Demonstrates: Time patterns, templates, HTTP, stop action
// ----------------------------------------------------------------
{
  "id": "telemetry",
  "alias": "Game telemetry reporting",
  "mode": "single",
  "triggers": [
    {
      "trigger": "time_pattern",
      "minutes": "/5"
    }
  ],
  "actions": [
    {
      "action": "if",
      "if": [
        {
          "condition": "template",
          "value_template": "{{ states('sensor.active_players') | int == 0 }}"
        }
      ],
      "then": [
        {
          "action": "log.debug",
          "data": {
            "message": "No active players, skipping telemetry"
          }
        },
        {
          "action": "stop",
          "stop": "No active players"
        }
      ]
    },
    {
      "action": "variables",
      "variables": {
        "telemetry_data": {
          "timestamp": "{{ now().isoformat() }}",
          "players": "{{ states('sensor.active_players') }}",
          "fps": "{{ states('sensor.average_fps') }}",
          "memory_mb": "{{ states('sensor.memory_usage') }}",
          "scene": "{{ states('game.current_scene') }}"
        }
      }
    },
    {
      "action": "http.post",
      "data": {
        "url": "https://telemetry.example.com/ingest",
        "json": "{{ variables.telemetry_data }}",
        "timeout": 10
      }
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 7: Complex Quest Chain with Branching Logic
// Demonstrates: Event data matching, nested choose, wait_for_trigger
// ----------------------------------------------------------------
{
  "id": "quest_handler",
  "alias": "Dynamic quest progression",
  "mode": "parallel",
  "max": 5,
  "triggers": [
    {
      "trigger": "event",
      "event_type": "quest.objective_complete",
      "event_data": {
        "quest_id": "ancient_artifact"
      }
    }
  ],
  "actions": [
    {
      "action": "choose",
      "choices": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.event.data.objective_id == 'collect_fragments' }}"
            }
          ],
          "sequence": [
            {
              "action": "ui.show_notification",
              "data": {
                "title": "Fragments Collected",
                "message": "You have all three fragments!",
                "duration": 5000
              }
            },
            {
              "action": "object.spawn",
              "data": {
                "prefab": "ancient_portal",
                "position": "{{ states('marker.temple_entrance') }}",
                "activate_after": 2
              }
            },
            {
              "action": "wait_for_trigger",
              "triggers": [
                {
                  "trigger": "zone",
                  "entity_id": "player.avatar",
                  "zone": "temple_entrance",
                  "event": "enter"
                }
              ],
              "timeout": "00:10:00",
              "continue_on_timeout": false
            },
            {
              "action": "scene.load",
              "data": {
                "scene": "ancient_temple_interior",
                "transition": "fade_to_black"
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.event.data.objective_id == 'defeat_guardian' }}"
            }
          ],
          "sequence": [
            {
              "action": "parallel",
              "parallel": [
                {
                  "action": "quest.complete",
                  "data": {
                    "quest_id": "ancient_artifact",
                    "rewards": {
                      "xp": 5000,
                      "items": ["legendary_sword"]
                    }
                  }
                },
                {
                  "action": "achievement.unlock",
                  "data": {
                    "achievement_id": "guardian_slayer"
                  }
                },
                {
                  "action": "cutscene.play",
                  "data": {
                    "cutscene_id": "artifact_revealed"
                  }
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}

// ----------------------------------------------------------------
// EXAMPLE 8: Performance Monitoring and Auto-Adjustment
// Demonstrates: Complex conditions, system monitoring, repeat until
// ----------------------------------------------------------------
{
  "id": "performance_optimizer",
  "alias": "Automatic performance optimization",
  "mode": "single",
  "variables": {
    "target_fps": 60,
    "min_fps": 30
  },
  "triggers": [
    {
      "trigger": "numeric_state",
      "entity_id": "sensor.current_fps",
      "below": 30,
      "for": "00:00:05"
    }
  ],
  "conditions": [
    {
      "condition": "state",
      "entity_id": "settings.auto_performance",
      "state": "enabled"
    }
  ],
  "actions": [
    {
      "action": "log.warning",
      "data": {
        "message": "FPS dropped below {{ variables.min_fps }}, optimizing..."
      }
    },
    {
      "action": "repeat",
      "until": [
        {
          "condition": "or",
          "conditions": [
            {
              "condition": "numeric_state",
              "entity_id": "sensor.current_fps",
              "above": 45
            },
            {
              "condition": "state",
              "entity_id": "graphics.quality",
              "state": "very_low"
            }
          ]
        }
      ],
      "sequence": [
        {
          "action": "graphics.reduce_quality",
          "data": {
            "step": 1
          }
        },
        {
          "action": "delay",
          "delay": "00:00:02"
        },
        {
          "action": "log.info",
          "data": {
            "message": "Quality reduced to {{ states('graphics.quality') }}, FPS: {{ states('sensor.current_fps') }}"
          }
        }
      ]
    }
  ]
}