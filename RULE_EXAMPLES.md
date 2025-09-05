# Unity Rules Engine - Curated Example Library

## Overview

This document provides a comprehensive set of rule examples demonstrating key patterns for Quest VR automation. All examples use the Home Assistant compatible schema format optimized for LLM generation.

## Basic Examples

### 1. Simple Button Haptic Feedback

**Use Case**: Provide haptic feedback when a button is pressed

```json
{
  "schema_version": 1,
  "id": "button_haptic_feedback",
  "alias": "Button Press Feedback",
  "description": "Simple haptic pulse on primary button press",
  "mode": "single",
  "trigger": [
    {
      "platform": "event",
      "event_type": "xr.button.pressed",
      "event_data": {
        "button": "primary",
        "hand": "right"
      }
    }
  ],
  "action": [
    {
      "service": "haptics.pulse",
      "data": {
        "hand": "right",
        "intensity": 0.5,
        "duration": { "milliseconds": 30 }
      }
    }
  ],
  "metadata": {
    "tier": "critical",
    "capabilities": ["haptics"]
  }
}
```

### 2. Zone Entry Notification

**Use Case**: Alert player when entering a danger zone

```json
{
  "schema_version": 1,
  "id": "danger_zone_alert",
  "alias": "Danger Zone Warning",
  "mode": "single",
  "trigger": [
    {
      "platform": "zone",
      "entity_id": "player.avatar",
      "zone": "lava_pit",
      "event": "enter"
    }
  ],
  "action": [
    {
      "parallel": [
        {
          "service": "ui.notification",
          "data": {
            "message": "Warning: Hazardous Area!",
            "color": "#FF4444",
            "duration": { "seconds": 3 }
          }
        },
        {
          "service": "audio.play",
          "data": {
            "sound": "warning_alarm",
            "volume": 0.8
          }
        },
        {
          "service": "haptics.pattern",
          "data": {
            "hand": "both",
            "pattern": "warning_pulse"
          }
        }
      ]
    }
  ],
  "metadata": {
    "tier": "critical",
    "capabilities": ["zones", "haptics", "audio", "ui"]
  }
}
```

### 3. Time-Based Lighting

**Use Case**: Automatically adjust lighting at specific times

```json
{
  "schema_version": 1,
  "id": "evening_lighting",
  "alias": "Evening Light Transition",
  "mode": "single",
  "trigger": [
    {
      "platform": "time",
      "at": "18:00:00"
    }
  ],
  "action": [
    {
      "service": "lighting.transition",
      "data": {
        "preset": "evening",
        "duration": { "seconds": 5 }
      }
    },
    {
      "service": "environment.enable",
      "target": {
        "entity_id": "group.street_lights"
      }
    }
  ],
  "metadata": {
    "tier": "standard",
    "capabilities": ["lighting", "environment"]
  }
}
```

## Intermediate Examples

### 4. Health-Based Warning System

**Use Case**: Progressive warnings and effects as health decreases

```json
{
  "schema_version": 1,
  "id": "low_health_warning",
  "alias": "Low Health Alert System",
  "mode": "restart",
  "trigger": [
    {
      "platform": "numeric_state",
      "entity_id": "player.health",
      "below": 30,
      "id": "critical_health"
    },
    {
      "platform": "numeric_state",
      "entity_id": "player.health",
      "below": 50,
      "above": 30,
      "id": "low_health"
    }
  ],
  "condition": [
    {
      "condition": "state",
      "entity_id": "player.status",
      "state": "alive"
    }
  ],
  "action": [
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'critical_health' }}"
            }
          ],
          "sequence": [
            {
              "repeat": {
                "while": [
                  {
                    "condition": "numeric_state",
                    "entity_id": "player.health",
                    "below": 30
                  }
                ],
                "sequence": [
                  {
                    "parallel": [
                      {
                        "service": "vfx.screen_effect",
                        "data": {
                          "effect": "blood_vignette",
                          "intensity": 0.8
                        }
                      },
                      {
                        "service": "haptics.pulse",
                        "data": {
                          "hand": "both",
                          "intensity": "{{ 1.0 - (states('player.health') | float / 30) }}",
                          "duration": { "milliseconds": 200 }
                        }
                      },
                      {
                        "service": "audio.play",
                        "data": {
                          "sound": "heartbeat_fast",
                          "volume": 0.9
                        }
                      }
                    ]
                  },
                  {
                    "delay": { "seconds": 1 }
                  }
                ]
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'low_health' }}"
            }
          ],
          "sequence": [
            {
              "service": "vfx.screen_effect",
              "data": {
                "effect": "damage_indicator",
                "intensity": 0.4
              }
            },
            {
              "service": "ui.notification",
              "data": {
                "message": "Health Low: {{ states('player.health') }}%",
                "color": "#FFAA00",
                "duration": { "seconds": 2 }
              }
            }
          ]
        }
      ]
    }
  ],
  "metadata": {
    "tier": "standard",
    "capabilities": ["haptics", "audio", "vfx", "ui"]
  }
}
```

### 5. Gesture-Based Teleportation

**Use Case**: Teleport player using hand gesture with preview

```json
{
  "schema_version": 1,
  "id": "gesture_teleport",
  "alias": "Gesture Teleportation",
  "mode": "single",
  "variables": {
    "preview_active": false,
    "target_position": null
  },
  "trigger": [
    {
      "platform": "event",
      "event_type": "xr.gesture.detected",
      "event_data": {
        "gesture": "point_hold",
        "hand": "dominant",
        "confidence_min": 0.8
      }
    }
  ],
  "condition": [
    {
      "condition": "state",
      "entity_id": "player.movement_mode",
      "state": "teleport"
    },
    {
      "condition": "template",
      "value_template": "{{ states('player.stamina') | float > 20 }}"
    }
  ],
  "action": [
    {
      "variables": {
        "target_position": "{{ states.attr('raycast.hit_point', 'position') }}"
      }
    },
    {
      "service": "object.spawn",
      "data": {
        "prefab": "teleport_preview",
        "position": "{{ target_position }}",
        "id": "teleport_preview_marker"
      }
    },
    {
      "wait_for_trigger": [
        {
          "platform": "event",
          "event_type": "xr.gesture.completed",
          "event_data": {
            "gesture": "fist",
            "hand": "dominant"
          }
        }
      ],
      "timeout": { "seconds": 3 },
      "continue_on_timeout": false
    },
    {
      "parallel": [
        {
          "service": "vfx.screen_fade",
          "data": {
            "color": "#000000",
            "duration": { "milliseconds": 200 }
          }
        },
        {
          "service": "player.teleport",
          "data": {
            "position": "{{ target_position }}",
            "maintain_height": true
          }
        },
        {
          "service": "haptics.pulse",
          "data": {
            "hand": "both",
            "intensity": 1.0,
            "duration": { "milliseconds": 100 }
          }
        }
      ]
    },
    {
      "service": "object.destroy",
      "target": {
        "entity_id": "teleport_preview_marker"
      }
    }
  ],
  "metadata": {
    "tier": "reactive",
    "capabilities": ["gestures", "teleport", "haptics", "vfx"]
  }
}
```

### 6. Combo Attack System

**Use Case**: Detect and execute combat combos based on button sequences

```json
{
  "schema_version": 1,
  "id": "combat_combo",
  "alias": "Three-Hit Combo",
  "mode": "restart",
  "variables": {
    "combo_count": 0,
    "damage_multiplier": 1.0
  },
  "trigger": [
    {
      "platform": "pattern",
      "sequence": [
        {
          "event": "xr.button.pressed",
          "data": { "button": "trigger", "hand": "right" }
        },
        {
          "event": "xr.button.pressed",
          "data": { "button": "trigger", "hand": "right" }
        },
        {
          "event": "xr.button.pressed",
          "data": { "button": "trigger", "hand": "right" }
        }
      ],
      "within": { "milliseconds": 1200 },
      "id": "triple_attack"
    }
  ],
  "condition": [
    {
      "condition": "state",
      "entity_id": "player.combat_state",
      "state": "engaged"
    },
    {
      "condition": "numeric_state",
      "entity_id": "player.stamina",
      "above": 30
    }
  ],
  "action": [
    {
      "variables": {
        "combo_count": "{{ states('counter.combo') | int + 1 }}",
        "damage_multiplier": 1.5
      }
    },
    {
      "parallel": [
        {
          "service": "animation.play",
          "data": {
            "clip": "combo_finisher",
            "speed": 1.2
          }
        },
        {
          "service": "combat.damage_area",
          "data": {
            "shape": "cone",
            "angle": 60,
            "range": 3.0,
            "damage": "{{ 25 * damage_multiplier }}"
          }
        },
        {
          "service": "vfx.spawn",
          "data": {
            "effect": "slash_trail",
            "color": "#00FFFF",
            "duration": { "milliseconds": 500 }
          }
        },
        {
          "service": "haptics.pattern",
          "data": {
            "hand": "right",
            "pattern": "combo_impact"
          }
        },
        {
          "service": "audio.play_spatial",
          "data": {
            "sound": "sword_combo_finish",
            "position": "{{ states.attr('weapon.sword', 'tip_position') }}",
            "volume": 0.9
          }
        }
      ]
    },
    {
      "service": "counter.increment",
      "target": {
        "entity_id": "counter.combo"
      }
    },
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ combo_count >= 10 }}"
            }
          ],
          "sequence": [
            {
              "service": "achievement.unlock",
              "data": {
                "achievement_id": "combo_master"
              }
            }
          ]
        }
      ]
    }
  ],
  "metadata": {
    "tier": "reactive",
    "capabilities": ["combat", "animation", "haptics", "audio", "vfx"]
  }
}
```

## Advanced Examples

### 7. Dynamic Difficulty Adjustment

**Use Case**: Automatically adjust game difficulty based on performance metrics

```json
{
  "schema_version": 1,
  "id": "dynamic_difficulty",
  "alias": "Performance-Based Difficulty",
  "mode": "single",
  "variables": {
    "death_count": 0,
    "success_rate": 1.0,
    "current_difficulty": "normal"
  },
  "trigger": [
    {
      "platform": "time_pattern",
      "minutes": "/5"
    }
  ],
  "action": [
    {
      "variables": {
        "death_count": "{{ states('counter.player_deaths') | int }}",
        "success_rate": "{{ states('sensor.mission_success_rate') | float }}",
        "current_difficulty": "{{ states('game.difficulty') }}"
      }
    },
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ death_count > 3 and current_difficulty != 'easy' }}"
            }
          ],
          "sequence": [
            {
              "service": "game.set_difficulty",
              "data": {
                "level": "easy"
              }
            },
            {
              "service": "ui.notification",
              "data": {
                "message": "Difficulty adjusted to Easy",
                "duration": { "seconds": 3 }
              }
            },
            {
              "service": "enemy.adjust_stats",
              "data": {
                "health_multiplier": 0.7,
                "damage_multiplier": 0.8
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ success_rate > 0.9 and death_count == 0 and current_difficulty != 'hard' }}"
            }
          ],
          "sequence": [
            {
              "service": "game.set_difficulty",
              "data": {
                "level": "hard"
              }
            },
            {
              "service": "ui.notification",
              "data": {
                "message": "Difficulty increased to Hard",
                "duration": { "seconds": 3 }
              }
            },
            {
              "service": "enemy.adjust_stats",
              "data": {
                "health_multiplier": 1.5,
                "damage_multiplier": 1.3,
                "ai_reaction_time": 0.7
              }
            }
          ]
        }
      ],
      "default": [
        {
          "service": "log.debug",
          "data": {
            "message": "Difficulty unchanged. Deaths: {{ death_count }}, Success: {{ success_rate }}"
          }
        }
      ]
    }
  ],
  "metadata": {
    "tier": "standard",
    "capabilities": ["game_state", "ui"]
  }
}
```

### 8. Interactive Object System

**Use Case**: Context-sensitive object interaction with multiple states

```json
{
  "schema_version": 1,
  "id": "interactive_chest",
  "alias": "Treasure Chest Interaction",
  "mode": "single",
  "variables": {
    "chest_state": "locked",
    "has_key": false,
    "interaction_count": 0
  },
  "trigger": [
    {
      "platform": "event",
      "event_type": "object.interaction",
      "event_data": {
        "object_type": "chest",
        "action": "grab"
      },
      "id": "grab_interaction"
    },
    {
      "platform": "event",
      "event_type": "object.proximity",
      "event_data": {
        "object_type": "chest",
        "distance_max": 2.0
      },
      "id": "proximity_trigger"
    }
  ],
  "action": [
    {
      "variables": {
        "chest_state": "{{ states('object.chest.state') }}",
        "has_key": "{{ states('inventory.golden_key') == 'possessed' }}",
        "interaction_count": "{{ interaction_count + 1 }}"
      }
    },
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'proximity_trigger' and chest_state == 'locked' }}"
            }
          ],
          "sequence": [
            {
              "service": "ui.hint",
              "data": {
                "message": "{{ 'Press grip to unlock' if has_key else 'Locked - Key required' }}",
                "position": "above_object"
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'grab_interaction' and chest_state == 'locked' and has_key }}"
            }
          ],
          "sequence": [
            {
              "parallel": [
                {
                  "service": "animation.play",
                  "target": {
                    "entity_id": "object.chest"
                  },
                  "data": {
                    "clip": "unlock_and_open",
                    "speed": 1.0
                  }
                },
                {
                  "service": "audio.play_spatial",
                  "data": {
                    "sound": "chest_unlock",
                    "position": "{{ states.attr('object.chest', 'position') }}"
                  }
                },
                {
                  "service": "haptics.pulse",
                  "data": {
                    "hand": "{{ trigger.event_data.hand }}",
                    "intensity": 0.7,
                    "duration": { "milliseconds": 150 }
                  }
                }
              ]
            },
            {
              "delay": { "seconds": 1.5 }
            },
            {
              "service": "inventory.remove",
              "data": {
                "item": "golden_key"
              }
            },
            {
              "service": "object.set_state",
              "target": {
                "entity_id": "object.chest"
              },
              "data": {
                "state": "open"
              }
            },
            {
              "service": "loot.spawn",
              "data": {
                "table": "treasure_chest_epic",
                "position": "{{ states.attr('object.chest', 'position') }}"
              }
            },
            {
              "service": "vfx.spawn",
              "data": {
                "effect": "treasure_sparkle",
                "position": "{{ states.attr('object.chest', 'position') }}",
                "duration": { "seconds": 3 }
              }
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'grab_interaction' and chest_state == 'locked' and not has_key }}"
            }
          ],
          "sequence": [
            {
              "service": "haptics.pulse",
              "data": {
                "hand": "{{ trigger.event_data.hand }}",
                "intensity": 0.3,
                "duration": { "milliseconds": 50 }
              }
            },
            {
              "service": "audio.play",
              "data": {
                "sound": "locked_rattle"
              }
            },
            {
              "choose": [
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ interaction_count % 3 == 0 }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "ui.notification",
                      "data": {
                        "message": "Find the Golden Key to unlock",
                        "duration": { "seconds": 3 }
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
  ],
  "metadata": {
    "tier": "standard",
    "capabilities": ["inventory", "animation", "haptics", "audio", "vfx", "ui"]
  }
}
```

### 9. Environmental Hazard System

**Use Case**: Complex environmental hazard with multiple phases

```json
{
  "schema_version": 1,
  "id": "lava_room_hazard",
  "alias": "Rising Lava Room",
  "mode": "restart",
  "variables": {
    "lava_level": 0,
    "phase": "inactive",
    "safe_platforms": ["platform_1", "platform_2", "platform_3"]
  },
  "trigger": [
    {
      "platform": "zone",
      "entity_id": "player.avatar",
      "zone": "lava_room",
      "event": "enter",
      "id": "room_enter"
    },
    {
      "platform": "state",
      "entity_id": "puzzle.lava_room",
      "to": "activated",
      "id": "puzzle_start"
    }
  ],
  "action": [
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'puzzle_start' }}"
            }
          ],
          "sequence": [
            {
              "variables": {
                "phase": "active",
                "lava_level": 0
              }
            },
            {
              "parallel": [
                {
                  "service": "audio.play_loop",
                  "data": {
                    "sound": "lava_bubbling",
                    "volume": 0.6
                  }
                },
                {
                  "service": "vfx.particle_system",
                  "data": {
                    "effect": "lava_steam",
                    "intensity": 0.5
                  }
                }
              ]
            },
            {
              "repeat": {
                "count": 10,
                "sequence": [
                  {
                    "variables": {
                      "lava_level": "{{ lava_level + 0.5 }}"
                    }
                  },
                  {
                    "parallel": [
                      {
                        "service": "environment.set_lava_height",
                        "data": {
                          "height": "{{ lava_level }}",
                          "duration": { "seconds": 3 }
                        }
                      },
                      {
                        "service": "lighting.adjust",
                        "data": {
                          "color": "#FF6600",
                          "intensity": "{{ 0.5 + (lava_level * 0.1) }}"
                        }
                      },
                      {
                        "service": "temperature.increase",
                        "data": {
                          "amount": 5,
                          "affect_player": true
                        }
                      }
                    ]
                  },
                  {
                    "choose": [
                      {
                        "conditions": [
                          {
                            "condition": "template",
                            "value_template": "{{ lava_level >= 2.0 }}"
                          }
                        ],
                        "sequence": [
                          {
                            "service": "camera.shake",
                            "data": {
                              "intensity": 0.3,
                              "duration": { "seconds": 2 }
                            }
                          },
                          {
                            "service": "platform.collapse",
                            "data": {
                              "platform_id": "{{ safe_platforms[0] }}",
                              "delay": { "seconds": 1 }
                            }
                          },
                          {
                            "variables": {
                              "safe_platforms": "{{ safe_platforms[1:] }}"
                            }
                          }
                        ]
                      }
                    ]
                  },
                  {
                    "condition": "template",
                    "value_template": "{{ states('puzzle.lava_room') != 'completed' }}"
                  },
                  {
                    "delay": { "seconds": 5 }
                  },
                  {
                    "choose": [
                      {
                        "conditions": [
                          {
                            "condition": "template",
                            "value_template": "{{ states('sensor.player_height') <= lava_level }}"
                          }
                        ],
                        "sequence": [
                          {
                            "service": "player.damage",
                            "data": {
                              "amount": 10,
                              "type": "fire"
                            }
                          },
                          {
                            "service": "vfx.screen_effect",
                            "data": {
                              "effect": "fire_damage",
                              "intensity": 0.7
                            }
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
          ]
        }
      ]
    },
    {
      "wait_for_trigger": [
        {
          "platform": "state",
          "entity_id": "puzzle.lava_room",
          "to": "completed"
        }
      ],
      "timeout": { "minutes": 5 }
    },
    {
      "parallel": [
        {
          "service": "environment.set_lava_height",
          "data": {
            "height": 0,
            "duration": { "seconds": 5 }
          }
        },
        {
          "service": "audio.stop",
          "data": {
            "sound": "lava_bubbling"
          }
        },
        {
          "service": "achievement.unlock",
          "data": {
            "achievement_id": "lava_room_survivor"
          }
        }
      ]
    }
  ],
  "metadata": {
    "tier": "reactive",
    "capabilities": ["environment", "hazards", "audio", "vfx", "lighting"]
  }
}
```

### 10. Performance Optimization System

**Use Case**: Automatic performance optimization based on frame rate

```json
{
  "schema_version": 1,
  "id": "performance_optimizer",
  "alias": "Auto Performance Tuning",
  "mode": "single",
  "variables": {
    "target_fps": 72,
    "current_quality": "high",
    "throttle_level": 1.0,
    "consecutive_drops": 0
  },
  "trigger": [
    {
      "platform": "numeric_state",
      "entity_id": "sensor.fps",
      "below": 68,
      "for": { "seconds": 3 },
      "id": "fps_drop"
    },
    {
      "platform": "numeric_state",
      "entity_id": "sensor.device_temperature",
      "above": 45,
      "id": "thermal_warning"
    },
    {
      "platform": "time_pattern",
      "seconds": "/30",
      "id": "periodic_check"
    }
  ],
  "action": [
    {
      "variables": {
        "current_fps": "{{ states('sensor.fps') | float }}",
        "temperature": "{{ states('sensor.device_temperature') | float }}",
        "current_quality": "{{ states('graphics.quality_level') }}"
      }
    },
    {
      "choose": [
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'fps_drop' or (trigger.id == 'thermal_warning' and current_fps < 72) }}"
            }
          ],
          "sequence": [
            {
              "variables": {
                "consecutive_drops": "{{ consecutive_drops + 1 }}"
              }
            },
            {
              "service": "log.warning",
              "data": {
                "message": "Performance drop detected. FPS: {{ current_fps }}, Temp: {{ temperature }}°C"
              }
            },
            {
              "choose": [
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ current_quality == 'high' }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "graphics.set_quality",
                      "data": {
                        "level": "medium"
                      }
                    },
                    {
                      "service": "rendering.adjust",
                      "data": {
                        "lod_bias": 1.5,
                        "shadow_distance": 20,
                        "particle_density": 0.7
                      }
                    }
                  ]
                },
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ current_quality == 'medium' and consecutive_drops >= 2 }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "graphics.set_quality",
                      "data": {
                        "level": "low"
                      }
                    },
                    {
                      "service": "rendering.adjust",
                      "data": {
                        "lod_bias": 2.0,
                        "shadow_distance": 10,
                        "particle_density": 0.4,
                        "disable_post_processing": true
                      }
                    },
                    {
                      "service": "rules.set_tier_enabled",
                      "data": {
                        "tier": "reactive",
                        "enabled": false
                      }
                    }
                  ]
                },
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ current_quality == 'low' and temperature > 47 }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "rules.set_evaluation_rate",
                      "data": {
                        "rate": 45
                      }
                    },
                    {
                      "service": "ui.notification",
                      "data": {
                        "message": "Thermal throttling active",
                        "duration": { "seconds": 3 }
                      }
                    }
                  ]
                }
              ]
            }
          ]
        },
        {
          "conditions": [
            {
              "condition": "template",
              "value_template": "{{ trigger.id == 'periodic_check' and current_fps >= 72 and temperature < 43 }}"
            }
          ],
          "sequence": [
            {
              "variables": {
                "consecutive_drops": 0
              }
            },
            {
              "choose": [
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ current_quality == 'low' and current_fps >= 85 }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "graphics.set_quality",
                      "data": {
                        "level": "medium"
                      }
                    },
                    {
                      "service": "rules.set_tier_enabled",
                      "data": {
                        "tier": "reactive",
                        "enabled": true
                      }
                    }
                  ]
                },
                {
                  "conditions": [
                    {
                      "condition": "template",
                      "value_template": "{{ current_quality == 'medium' and current_fps >= 90 }}"
                    }
                  ],
                  "sequence": [
                    {
                      "service": "graphics.set_quality",
                      "data": {
                        "level": "high"
                      }
                    },
                    {
                      "service": "rules.set_evaluation_rate",
                      "data": {
                        "rate": 90
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
  ],
  "metadata": {
    "tier": "critical",
    "capabilities": ["performance", "graphics", "thermal"]
  }
}
```

## Pattern Reference Guide

### Trigger Patterns
- **Single Event**: Simple event-based activation
- **State Change**: React to entity state transitions
- **Numeric Threshold**: Monitor numeric values with optional duration
- **Zone Events**: Spatial enter/leave detection
- **Time-Based**: Scheduled or periodic execution
- **Pattern Detection**: Complex event sequences within time windows

### Condition Patterns
- **State Validation**: Check current entity states
- **Numeric Comparisons**: Value range checking
- **Time Windows**: Day/time restrictions
- **Template Expressions**: Complex boolean logic
- **Logical Operators**: AND/OR/NOT combinations

### Action Patterns
- **Service Calls**: Execute engine services
- **Delays**: Time-based waits
- **Wait for Triggers**: Pause until condition met
- **Variable Management**: Store and update state
- **Branching Logic**: Choose based on conditions
- **Repetition**: Loops with conditions
- **Parallel Execution**: Concurrent actions
- **Error Handling**: Stop with reason

## Best Practices

### Performance Optimization
1. Use appropriate execution tiers (critical/standard/reactive)
2. Minimize template expressions in critical path
3. Batch parallel actions when possible
4. Use numeric conditions over template expressions
5. Implement throttling for high-frequency triggers

### LLM Generation Tips
1. Provide clear use case descriptions
2. Use consistent entity naming conventions
3. Include all required metadata fields
4. Specify capabilities explicitly
5. Test with validation before deployment

### Debugging
1. Use meaningful rule IDs and aliases
2. Add debug log actions during development
3. Monitor performance metrics
4. Use the diagnostic "why didn't fire" tool
5. Test edge cases with synthetic events

## Conclusion

This curated example library demonstrates the flexibility and power of the Unity Rules Engine for Quest VR automation. From simple button feedback to complex environmental systems, these patterns provide a foundation for creating engaging, reactive VR experiences while maintaining optimal performance on Quest hardware.