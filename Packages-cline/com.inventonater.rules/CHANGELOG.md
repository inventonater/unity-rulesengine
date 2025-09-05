# Changelog

All notable changes to this project will be documented in this file.

## [0.1.0] - 2025-01-04

### Added
- Initial weekend MVP release
- Core rules engine with EventBus and RuleEngine
- 4 trigger types: event, numeric_threshold, time_schedule, pattern_sequence
- 2 condition types: state_equals, numeric_compare
- 4 action types: service_call, wait_duration, repeat_count, stop
- Pattern sequence support for double-click and Konami code
- Desktop input handling (mouse and keyboard)
- Services: audio.play, debug.log, ui.toast, state.set
- DevPanel for runtime JSON import/testing
- Compatibility aliases for common variations (timer→time_schedule, value→numeric_threshold, pattern→pattern_sequence)
- Sample JSON rules demonstrating all features
- UPM package structure with Unity 6.2 support
