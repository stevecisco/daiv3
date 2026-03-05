# Daiv3.Mcp.Integration – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Model Context Protocol (MCP) integration layer. Provides adapters, handlers, and resource management to enable standardised AI model–tool communication following the MCP specification. Consumed by `Daiv3.Orchestration` when the orchestrator needs to invoke MCP-compatible tools or servers.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- MCP client and server adapters
- Tool/resource descriptor models aligned to the MCP spec
- Request/response serialisation for MCP message format
- Integration with the orchestration tool dispatch pipeline

## Rules

- Changes to MCP message contracts must remain compatible with MCP spec versioning
- Keep this library free of business logic — pure protocol layer only

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.Orchestration` | Primary consumer — dispatches tool calls via MCP |
| `Daiv3.Core` | Shared domain types referenced in tool descriptors |

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.Mcp.Integration.Tests/Daiv3.Mcp.Integration.Tests.csproj --nologo --verbosity minimal
```
