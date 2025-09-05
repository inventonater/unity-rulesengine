# Rules Engine Demo Scene

This demo showcases the Unity Rules Engine with JSON-driven triggers, conditions, and actions.

## Quick Start

1. **Import the Sample**: In Unity Package Manager, find "Inventonater Rules" and import the Demo sample
2. **Create a Scene**: Create a new Unity scene
3. **Add Rules Engine**: Create an empty GameObject and add the `RulesEngineManager` component
4. **Add Beep Sound** (Optional): Place a beep.wav file in `Runtime/Desktop/Resources/`
5. **Hit Play**: The engine will auto-load sample rules and display the DevPanel

## Controls

- **Click**: Play beep sound
- **Hold Left Mouse**: Show toast notification (hold for 300ms)
- **Double-click**: Play loud beep
- **Move Mouse Fast**: Trigger speed gate (3 beeps)
- **Space Twice**: Triple beep combo
- **Konami Code**: ↑↑↓↓←→←→BA (shows toast + beeps)
- **F1**: Toggle debug mode (enables periodic hint logs)
- **F2**: Toggle DevPanel visibility

## DevPanel Features

- **Load Rule**: Paste JSON and load a single rule
- **Load All Sample Rules**: Load all 8 demo rules
- **Emit Event**: Manually trigger any event
- **Quick Events**: Buttons for common events
- **Reload All**: Refresh all rules

## Sample Rules Included

1. `click_beep.json` - Simple click trigger
2. `hold_to_toast.json` - Numeric threshold with duration
3. `periodic_hint.json` - Scheduled timer with condition
4. `speed_gate.json` - Mouse speed threshold with restart mode
5. `double_click_pattern.json` - Pattern sequence example
6. `konami_pattern.json` - Complex pattern sequence
7. `heartbeat_log.json` - Simple timer schedule
8. `space_combo_triple_beep.json` - Space key pattern

## JSON Rule Structure

```json
{
  "id": "unique_rule_id",
  "mode": "single|restart|queued|parallel",
  "triggers": [
    {
      "type": "event|numeric_threshold|time_schedule|pattern_sequence",
      // trigger-specific fields
    }
  ],
  "conditions": [
    {
      "type": "state_equals|numeric_compare",
      // condition-specific fields
    }
  ],
  "actions": [
    {
      "type": "service_call|wait_duration|repeat_count|stop",
      // action-specific fields
    }
  ]
}
```

## LLM-Friendly Features

- **Explicit types**: Every component has a `type` field
- **Arrays always**: Even single items use arrays
- **Units in names**: Fields like `every_ms_10_to_600000` show valid ranges
- **String enums**: Clear string values instead of magic numbers
- **Compatibility aliases**: Accepts common variations (timer→time_schedule)

## Custom Integration

```csharp
// Get the manager instance
var manager = RulesEngineManager.Instance;

// Load a single rule
manager.LoadRuleJson(jsonString);

// Load multiple rules
manager.LoadRulesJson(jsonArrayString);

// Emit custom event
manager.EmitEvent("my.custom.event");

// Set entity state
manager.SetState("game.level", "boss");
manager.SetNumeric("player.health", 100);
```

## Architecture

- **EventBus**: Channel-based async event streaming
- **RuleEngine**: Core processing with mode support
- **PatternSequenceWatcher**: Tracks ordered event sequences
- **EntityStore**: Key-value state management
- **Services**: Extensible action handlers

## Tips

- Rules with unknown fields log warnings but continue working
- Numeric values are automatically clamped to valid ranges
- Pattern sequences reset if the time window expires
- The DevPanel can be dragged by its title bar
- All logs are prefixed with their component name for easy filtering

## Extending

To add new services:
1. Extend the `Services.ExecuteServiceAsync` switch statement
2. Add your service handler method
3. Use in rules with `"type": "service_call", "service": "your.service"`

To add new trigger types:
1. Extend `TriggerDto` with new fields
2. Update `RuleEngine.ProcessEvent` 
3. Add key generation in `RuleRepository.GetTriggerKey`
