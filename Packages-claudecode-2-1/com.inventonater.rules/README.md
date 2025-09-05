# Unity Rules Engine v2.1

A high-performance Event-Condition-Action (ECA) rules engine for Unity, optimized for LLM authoring and weekend hackathon development.

## Features

- **Clean Architecture**: Interface-driven design with dependency injection support
- **Multiple Trigger Types**: Events, timers, thresholds, and pattern sequences
- **Flexible Conditions**: Comparison and existence checks on entity values
- **Rich Actions**: Sound playback, value manipulation, logging, and delays
- **Unity Integration**: UniTask async support, Unity-native input handling
- **LLM-Friendly**: JSON schema optimized for AI generation with explicit types and units

## Quick Start

1. Add to Unity Package Manager via `package.json`
2. Create a GameObject and add `RulesEngineManager` component
3. Place rule JSON files in `Assets/StreamingAssets/Rules/`
4. Run your scene!

## Architecture Highlights

### Best-in-Class Design
This implementation combines the strongest attributes from multiple approaches:

- **Clean Separation** (from codex1): IDisposable pattern for resource management
- **Dependency Injection** (from codex3): Constructor-based DI for testability
- **Interface-Driven** (from cline): All major components behind interfaces
- **Pattern Matching** (from claude): Robust sequence detection with timing
- **Type Safety** (from codex2): Strongly-typed DTOs with clear structure

### Core Components

- `RuleEngine`: Main orchestrator using IDisposable pattern
- `EntityStore`: Thread-safe key-value store with change notifications
- `EventBus`: Publish-subscribe event system
- `PatternSequenceWatcher`: Temporal pattern matching
- `ConditionEvaluator`: Flexible condition evaluation
- `ActionRunner`: Async action execution with cancellation

## Rule Structure

```json
{
  "id": "unique_rule_id",
  "mode": "single|queued",
  "triggers": [
    { "type": "event", "name": "click" },
    { "type": "time_schedule", "every_ms_10_to_600000": 1000 },
    { "type": "numeric_threshold", "entity": "health", "below": "30" },
    { "type": "pattern_sequence", "sequence": [...], "within_ms_10_to_5000": 2000 }
  ],
  "conditions": [
    { "type": "comparison", "entity": "score", "operator": "greater_than", "value": "100" }
  ],
  "actions": [
    { "type": "play_sound", "sound": "beep", "volume_0_to_1": 0.5 },
    { "type": "set_value", "entity": "level", "value": "2" },
    { "type": "log", "message": "Level up!", "severity": "info" }
  ]
}
```

## Performance

- **Minimal Allocations**: Pooled objects and struct-based data
- **O(1) Operations**: Hash-based lookups for events and entities
- **Async-First**: UniTask for non-blocking operations
- **Efficient Timers**: Unity-native timer implementation

## Testing

The modular, interface-driven design enables:
- Unit testing without Unity dependencies
- Mock implementations for all services
- Isolated component testing
- Integration testing with test rules

## License

MIT License - Perfect for hackathons and prototypes!