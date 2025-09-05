# Inventonater Rules Engine for Unity

Weekend MVP rules engine that converts LLM-friendly JSON into Unity triggers, conditions, and actions.

## Features

- **JSON-driven rules** with explicit types and array-always convention
- **4 trigger types**: event, numeric_threshold, time_schedule, pattern_sequence
- **2 condition types**: state_equals, numeric_compare  
- **4 action types**: service_call, wait_duration, repeat_count, stop
- **Pattern sequences** for double-click, Konami code, combos
- **Compatibility aliases** for common variations
- **Desktop-focused** with Quest-ready architecture

## Quick Start

1. Import this package into your Unity 6.2 project
2. Import the Demo sample from Package Manager
3. Open Demo scene and hit Play
4. Try: click, double-click, hold mouse, Space twice, or Konami code

## JSON Rule Example

```json
{
  "id": "double_click_pattern",
  "triggers": [{
    "type": "pattern_sequence",
    "within_ms_10_to_5000": 250,
    "sequence": [
      { "name": "mouse.left.down" },
      { "name": "mouse.left.down" }
    ]
  }],
  "actions": [{
    "type": "service_call",
    "service": "audio.play",
    "data": { "clip": "beep", "volume_0_to_1": 1.0 }
  }]
}
```

## Schema Conventions

- Always use arrays (even for single items)
- Explicit `type` field for all components
- Units/ranges in field names (e.g., `every_ms_10_to_600000`)
- String enums for clarity
- Unknown fields ignored with warning

## License

MIT License - See LICENSE.md
