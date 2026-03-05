# Daiv3.OnlineProviders.Abstractions – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Base abstraction library for all cloud AI provider integrations. Defines the interfaces, request/response contracts, and base classes shared by the OpenAI, AzureOpenAI, and Anthropic provider implementations.

## Project Type

Library (pure abstractions — no SDK or HTTP dependencies)

## Target Framework

`net10.0`

## Key Responsibilities

- `IOnlineProvider` interface — uniform invocation surface for all cloud LLMs
- Request/response types (`OnlineRequest`, `OnlineResponse`, streaming variants)
- `IOnlineProviderFactory` — resolves the correct provider by name/type at runtime
- Base configuration options class (`OnlineProviderOptions`)
- Provider registration extension methods consumed by DI setup in executables

## Rules

- **No HTTP client code here** — that belongs in the concrete provider projects.
- **No SDK references** — this must remain SDK-agnostic so provider implementations are swappable.
- Additions to the interface surface affect all three provider implementations — coordinate changes carefully.

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.OnlineProviders.OpenAI` | Implementation of this abstraction |
| `Daiv3.OnlineProviders.AzureOpenAI` | Implementation of this abstraction |
| `Daiv3.OnlineProviders.Anthropic` | Implementation of this abstraction |
| `Daiv3.Orchestration` | Primary consumer via `IOnlineProvider` |
| `Daiv3.ModelExecution` | Uses for cloud fallback |

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.OnlineProviders.Tests/Daiv3.OnlineProviders.Tests.csproj --nologo --verbosity minimal
```
