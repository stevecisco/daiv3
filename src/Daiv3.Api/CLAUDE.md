# Daiv3.Api – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

ASP.NET Core REST API service providing an HTTP interface to the DAIv3 system. Exposes orchestration, knowledge search, and persistence operations for integration with external tools, automation scripts, and remote clients. Includes Swagger/OpenAPI documentation.

## Project Type

ASP.NET Core Web API

## Target Framework

`net10.0-windows10.0.26100` (inherits Windows-specific library features from referenced libraries).

## Key Responsibilities

- REST endpoints for chat/task execution (delegates to `Daiv3.Orchestration`)
- Knowledge search endpoints (delegates to `Daiv3.Knowledge`)
- Model management status endpoints (delegates to `Daiv3.FoundryLocal.Management`)
- OpenAPI/Swagger documentation via Swashbuckle or built-in .NET OpenAPI
- Authentication/authorisation for API access (secure all endpoints)

## Security Requirements

- All endpoints must be authenticated (no anonymous access to functional endpoints)
- Validate all inputs at the controller boundary
- Never return sensitive data (API keys, connection strings) in responses
- Log all authentication failures and suspicious request patterns
- Rate limit AI endpoints to prevent abuse

## Conventions

- Controller actions are thin — delegate to service layer immediately
- Use `ILogger<T>` throughout; no `Console.WriteLine`
- Return `ProblemDetails` (RFC 7807) for error responses
- Mark all endpoints with `[ProducesResponseType]` attributes for accurate Swagger docs

## Test Projects

Architecture tests cover API layer dependency rules:

```powershell
dotnet test tests/unit/Daiv3.Architecture.Tests/Daiv3.Architecture.Tests.csproj --nologo --verbosity minimal
dotnet test tests/integration/Daiv3.Architecture.Integration.Tests/Daiv3.Architecture.Integration.Tests.csproj --nologo --verbosity minimal
```
