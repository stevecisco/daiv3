# Daiv3.Infrastructure.Shared – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Shared infrastructure utilities consumed by multiple projects. Provides logging abstractions, `IHttpClientFactory` configuration helpers, configuration extension methods, DI registration helpers, and other cross-cutting plumbing that doesn't belong in `Daiv3.Core` (which is domain-only).

## Project Type

Library

## Target Framework

`net10.0` (no platform-specific features).

## Key Responsibilities

- `IHttpClientFactory` policy helpers (retry, timeout, circuit-breaker via Polly if approved)
- Logging enrichment extensions
- Configuration binding helpers
- Common DI extension methods shared across multiple projects
- Application-wide constants and shared utility classes

## Rules

- **No business logic.** Infrastructure and plumbing only.
- **No circular dependency.** This project may reference `Daiv3.Core` but nothing else in `src/`.
- New utilities here should be genuinely shared (used by ≥2 other projects). Otherwise put them in the consuming project.

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.Infrastructure.Shared.Tests/Daiv3.Infrastructure.Shared.Tests.csproj --nologo --verbosity minimal
```
