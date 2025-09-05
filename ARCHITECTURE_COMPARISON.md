# Architecture Proposals Comparison Report

## Executive Summary

Three architecture proposals were analyzed for the Unity Rules Engine targeting Quest VR:

1. **eca-json-gpt-ifdo** - Focus on tiny FSM compilation with "if-do" pattern
2. **eca-json-gpt-whendo** - Simplified v0.1 schema with "when-do" pattern  
3. **eca-yaml-r3-claude** - Three-tier hybrid model with advanced features

## Key Areas of Comparison

### 1. Schema Design Philosophy

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Format | JSON with minimal DSL | JSON with Home Assistant compatibility | YAML with rich expressiveness | **eca-json-gpt-whendo** (LLM compatibility) |
| Top-level structure | `on`/`if`/`do` | `triggers`/`conditions`/`actions` | `trigger`/`conditions`/`actions` | **eca-json-gpt-whendo** (clarity) |
| Expression language | Tiny typed DSL | Minimal expr with interpolation | Custom script support | **eca-json-gpt-ifdo** (sandboxed safety) |
| Temporal patterns | Explicit `pattern` trigger | Observable patterns | Complex state machines | **eca-yaml-r3-claude** (powerful but complex) |

### 2. Performance Strategy

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Target latency | ≤0.5ms worst case | <0.5ms p95 | <0.1ms critical tier | **eca-yaml-r3-claude** (tiered approach) |
| Memory strategy | Pre-allocate, zero-alloc hot path | Object pools, struct events | Native collections, Burst | **eca-yaml-r3-claude** (Burst optimization) |
| Compilation approach | Tiny FSM per rule | Precompiled delegates | Visitor pattern + Burst jobs | **eca-json-gpt-ifdo** (simplicity) |
| Timer implementation | Timer wheel | Timer wheel | Multiple strategies | Tie (all use timer wheel) |

### 3. Architecture Separation

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Core/Unity separation | Standalone .NET Standard 2.1 | Standalone .NET Standard 2.1 | Three-tier hybrid | **eca-json-gpt-whendo** (clean separation) |
| Unity integration | Adapter pattern | Adapter with UniTask | Direct Burst integration | **eca-yaml-r3-claude** (performance) |
| Testing strategy | Unit tests on core | Property-based + integration | Multi-level validation | **eca-yaml-r3-claude** (comprehensive) |
| Development workflow | CLI tools + simulator | CLI + hot reload | Network hot reload | **eca-yaml-r3-claude** (device debugging) |

### 4. Action Execution Model

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Execution modes | single/restart/queue/parallel | single/restart/queued/parallel | + state machines | **eca-yaml-r3-claude** (flexibility) |
| Action types | call/wait/choose/parallel/stop | + delay/repeat/vars | + custom scripts | **eca-json-gpt-whendo** (balance) |
| Wait semantics | Multiple wait variants | Unified wait with conditions | State-based transitions | **eca-json-gpt-whendo** (clarity) |
| Concurrency control | Explicit max instances | Max with overflow policy | Per-tier concurrency | **eca-json-gpt-ifdo** (predictable) |

### 5. Developer Experience

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Schema validation | Strict with linter | Three-tier validation | Four-tier with perf estimates | **eca-yaml-r3-claude** (comprehensive) |
| Error messages | Include rule ID & context | Actionable with suggestions | Performance warnings | **eca-json-gpt-whendo** (actionable) |
| Documentation | Implementation focused | Schema cheatsheet | Full examples library | **eca-yaml-r3-claude** (templates) |
| Binary format | .uar compiled format | MessagePack option | No binary mentioned | **eca-json-gpt-ifdo** (deployment ready) |

### 6. LLM Generation Support

| Aspect | eca-json-gpt-ifdo | eca-json-gpt-whendo | eca-yaml-r3-claude | Winner |
|--------|-------------------|---------------------|-------------------|---------|
| Schema simplicity | Lean surface area | Home Assistant familiar | Complex but templateable | **eca-json-gpt-whendo** (proven pattern) |
| Examples coverage | 10 focused examples | 10 comprehensive examples | 6 detailed templates | Tie (different strengths) |
| Interpolation safety | ${expr} in action data only | ${...} in strings | Template variables | **eca-json-gpt-whendo** (controlled) |
| Generation success rate | Not specified | Target >85% | Measured 92% | **eca-yaml-r3-claude** (proven metrics) |

## Critical Differences

### Unique to eca-json-gpt-ifdo:
- Explicit FSM compilation per rule
- Binary .uar format specification
- "Why didn't this fire?" diagnostic buffer
- Emphasis on minimal runtime overhead

### Unique to eca-json-gpt-whendo:
- Home Assistant format compatibility
- Observable pattern support with R3
- Time source abstraction (game vs realtime)
- Property-based testing approach

### Unique to eca-yaml-r3-claude:
- Three-tier execution model (critical/standard/reactive)
- Burst compiler integration
- State machine support with transitions
- Thermal management system
- Template library for LLM generation

## Areas of Overlap

All three proposals agree on:
- Standalone C# core with Unity adapter pattern
- .NET Standard 2.1 for IL2CPP compatibility
- Zero-allocation hot path requirement
- Timer wheel for temporal operations
- Entity/Service registry pattern
- JSON/YAML authoring with validation
- Development hot reload capability
- 100+ concurrent rules target

## Recommended "Winner" for Key Decisions

### Must-Have Winners:

1. **Schema Format**: **eca-json-gpt-whendo** - Home Assistant compatibility maximizes LLM success
2. **Performance Tier System**: **eca-yaml-r3-claude** - Three-tier model optimizes Quest constraints  
3. **Memory Management**: **eca-yaml-r3-claude** - Burst + Native Collections for zero GC
4. **Core Separation**: **eca-json-gpt-whendo** - Clean .NET Standard approach with UniTask boundary
5. **Expression Safety**: **eca-json-gpt-ifdo** - Tiny sandboxed DSL prevents security issues
6. **Binary Distribution**: **eca-json-gpt-ifdo** - .uar format for production deployment
7. **Hot Reload**: **eca-yaml-r3-claude** - Network reload for device debugging
8. **Validation Pipeline**: **eca-yaml-r3-claude** - Four-tier validation with performance estimates

### Nice-to-Have Winners:

1. **State Machines**: **eca-yaml-r3-claude** - Powerful for complex interactions
2. **Diagnostic Tools**: **eca-json-gpt-ifdo** - "Why didn't fire" buffer
3. **Template Library**: **eca-yaml-r3-claude** - Accelerates LLM generation
4. **Thermal Management**: **eca-yaml-r3-claude** - Critical for Quest longevity

## Synthesis Recommendations

The final implementation should combine:

1. **eca-json-gpt-whendo**'s Home Assistant schema as the primary authoring format
2. **eca-yaml-r3-claude**'s three-tier execution model for performance optimization
3. **eca-json-gpt-ifdo**'s sandboxed expression evaluator and diagnostic tools
4. **eca-yaml-r3-claude**'s Burst compilation for critical path
5. **eca-json-gpt-ifdo**'s binary compilation format for deployment
6. **eca-yaml-r3-claude**'s comprehensive validation and hot reload systems

This hybrid approach leverages proven LLM compatibility, Quest-optimized performance, and production-ready tooling.