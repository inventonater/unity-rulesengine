# Rules Engine Demo

## Quick Start

1. Create an empty scene
2. Add an empty GameObject
3. Attach the `DemoSetup.cs` script
4. Hit Play!

## Controls

- **Click**: Plays a beep sound
- **Double-click**: Plays a louder beep
- **Hold mouse button** (300ms): Shows "Held!" toast
- **Move mouse fast**: Triggers 3 quick beeps when speed > 600
- **Press Space twice** (within 300ms): Plays triple beep combo
- **Konami Code** (↑↑↓↓←→←→BA within 1.5s): Shows "KONAMI!" toast + beeps
- **F1**: Toggle debug mode (enables periodic debug logs)
- **F2**: Toggle DevPanel (paste JSON rules, emit events, view state)

## Sample Rules

The demo loads JSON rules from the `Rules/` folder:
- `click_beep.json` - Simple click → beep
- `double_click_pattern.json` - Pattern sequence for double-click
- `hold_to_toast.json` - Numeric threshold with duration
- `speed_gate.json` - Mouse speed threshold with restart mode
- `konami_pattern.json` - Complex 10-step pattern sequence
- `space_combo_triple_beep.json` - Two-key combo
- `periodic_hint.json` - Timer with state condition
- `heartbeat_log.json` - Simple timer

## DevPanel Features

Press F2 to open the DevPanel:
- Load rules from any directory
- Paste and test JSON rules
- Emit custom events
- View current entity state
- See recent events

## Architecture

- **EventBus**: Async event distribution
- **RuleEngine**: Core rule processor with pattern matching
- **EntityStore**: Numeric and string state storage
- **Services**: Audio, logging, toast UI, state manipulation
- **PatternSequenceWatcher**: Time-windowed event sequences
- **TimerService**: Scheduled triggers
- **DesktopInput**: Mouse/keyboard to events + entities