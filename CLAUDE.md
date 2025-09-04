# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity-focused Event-Condition-Action (ECA) rules engine architecture repository. The project contains comprehensive architectural documentation for building high-performance, deterministic automation systems suitable for VR/AR applications, particularly Unity/Quest environments.

## Key Architecture Concepts

### Core Design Principles
- **L0/L1 hot path**: Deterministic execution with constant or tightly bounded time, zero allocations per tick
- **No MonoBehaviours/ScriptableObjects**: Pure C# implementations that integrate via PlayerLoop or UniTask
- **JSON-based configuration**: Human-readable artifacts using Newtonsoft.Json
- **Hot-swappable policies**: Atomic runtime data swaps without service interruption
- **IL2CPP/AOT compatible**: Designed to work within Unity's compilation constraints

### Architecture Components

1. **ECA (Event-Condition-Action) System** (`architecture/unity-eca.md`)
   - Compiles JSON policies to deterministic runtime tables
   - Hierarchical Finite State Machines (HFSM) for stateful flows
   - Complex Event Processing (CEP) for temporal patterns (dwell, debounce)
   - Symbol table-based approach avoiding string lookups at runtime

2. **Automation Runner** (`architecture/automation-runner-*.md`)
   - Home Assistant-style automation model (triggers → conditions → actions)
   - Event bus architecture with lock-free publishing
   - JSONPath-based templating for dynamic values
   - Concurrency modes: single, queued, restart

3. **Recommended Dependencies** (`architecture/eca-dependencies.md`)
   - Microsoft RulesEngine for JSON-defined rules
   - UniRx/R3 for reactive event composition
   - MessagePipe for high-performance pub/sub
   - NJsonSchema for JSON validation
   - UniTask for async operations

## Important Unity/IL2CPP Considerations

- **No JIT compilation**: Avoid expression trees and dynamic code generation
- **AOT constraints**: Use interpreters (JsonLogic.Net, NCalc) instead of runtime compilation
- **Memory management**: Preallocate arrays, use ArrayPool<T>, avoid LINQ in hot paths
- **Time handling**: Use monotonic clocks, not DateTime.Now

## Performance Requirements

- **Zero GC allocations** in evaluation hot path
- **Constant-time** rule evaluation
- **P95 latency targets** must be validated for each event type
- **Thread-safe hot-swapping** using volatile writes

## Testing Approach

When implementing features:
1. Unit test condition evaluation, ROI checks, and dwell timing
2. Create golden trace tests for decision stability
3. Run micro-benchmarks for P95 latency verification
4. Profile for GC allocations using Unity Profiler

## Development Patterns

### When Adding New Features
- Review existing architecture documents to understand established patterns
- Maintain separation between authoring model (JSON) and runtime model (compiled)
- Keep hot path code allocation-free
- Use UniTask for async operations, never in L0/L1 decision paths

### Code Organization
- Pure .NET Standard assemblies for core logic (no Unity dependencies)
- Separate Unity integration layer (minimal, optional)
- Use struct-based approaches for hot path data
- Implement registries for extensible triggers, conditions, and actions