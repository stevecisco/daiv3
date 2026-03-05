# Daiv3.Orchestration – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

System-wide task orchestration engine. Coordinates multi-step AI workflows, agent execution, skill management, tool routing, message brokering, learning feedback, and web content ingestion scheduling. This is the runtime brain of the DAIv3 system.

## Project Type

Library

## Target Framework

`net10.0` (no platform-specific dependencies; hardware routing is delegated to `Daiv3.ModelExecution` and `Daiv3.Knowledge.Embedding`).

## Key Responsibilities

- Workflow execution pipeline (plan → execute → reflect → store)
- Agent lifecycle and skill/tool dispatch
- `IMessageBroker` — in-process message routing between components
- Learning feedback integration with `Daiv3.Persistence`
- `WebContentIngestionService` — scheduled web crawl ingestion triggers
- Integration with `Daiv3.Knowledge` for RAG-based context retrieval
- Integration with `Daiv3.ModelExecution` for local LLM calls
- Integration with `Daiv3.OnlineProviders.*` for cloud fallback

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.Core` | Domain models and interfaces |
| `Daiv3.ModelExecution` | Local SLM execution |
| `Daiv3.Knowledge` | RAG context retrieval |
| `Daiv3.Persistence` | State, learning metrics, session storage |
| `Daiv3.OnlineProviders.*` | Cloud LLM fallback |
| `Daiv3.Scheduler` | Periodic task triggers |
| `Daiv3.Mcp.Integration` | MCP tool calls |

## Test Projects

```powershell
# Unit tests
dotnet test tests/unit/Daiv3.Orchestration.Tests/Daiv3.Orchestration.Tests.csproj --nologo --verbosity minimal

# Integration tests
dotnet test tests/integration/Daiv3.Orchestration.IntegrationTests/Daiv3.Orchestration.IntegrationTests.csproj --nologo --verbosity minimal
```
