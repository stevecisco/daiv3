# Daiv3.Core – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Core domain library. Contains shared models, contracts, interfaces, and domain primitives used across the entire DAIv3 system. Has no platform-specific dependencies and no business logic — only shared abstractions.

## Project Type

Library – **no executable entry point**

## Target Framework

`net10.0` only (no platform-specific APIs here).

## Responsibilities

- Domain entity definitions (models, DTOs, value objects)
- Cross-cutting interfaces consumed by multiple projects
- Shared settings/configuration models
- Common enumerations and constants

## Key Rules for This Project

- **No external package dependencies** beyond .NET 10 framework packages.
- **No business logic** — that belongs in the specific domain library (e.g., `Daiv3.Orchestration`, `Daiv3.Knowledge`).
- **No DI registrations** — this is a pure abstraction library; consumers register implementations.
- Additions here affect the entire solution — review impact before adding anything.

## Dependencies

None (foundational library — nothing in `src/` should create a circular dependency by referencing this project and being referenced by it).

## Test Projects

`Daiv3.Core` is validated through the tests of its consumers. There is no dedicated unit test project.

Cross-cutting architectural rules are validated in:
```
tests/unit/Daiv3.Architecture.Tests/
tests/integration/Daiv3.Architecture.Integration.Tests/
```
