# Daiv3.ModelExecution – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Bridge layer for local SLM (Small Language Model) execution via Microsoft Foundry Local. Handles model lifecycle, selection routing, task classification, hardware-aware model assignment, and fallback chains (NPU → GPU → CPU → online provider).

## Project Type

Library

## Target Framework

`net10.0` (hardware capability routing is delegated to `Daiv3.Knowledge.Embedding` and `Daiv3.FoundryLocal.Management`; this library stays platform-agnostic).

## Key Responsibilities

- Model selection based on task type and hardware capabilities
- Execution request routing (local vs. online fallback decision)
- Model lifecycle events (load, unload, health check)
- Result streaming and cancellation support
- Integration with `Daiv3.FoundryLocal.Bridge` for Foundry Local invocation

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.FoundryLocal.Bridge` | Foundry Local SDK abstraction |
| `Daiv3.FoundryLocal.Management` | Model download and session management |
| `Daiv3.OnlineProviders.*` | Fallback when local models unavailable |
| `Daiv3.Orchestration` | Primary consumer |

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.ModelExecution.Tests/Daiv3.ModelExecution.Tests.csproj --nologo --verbosity minimal
```
