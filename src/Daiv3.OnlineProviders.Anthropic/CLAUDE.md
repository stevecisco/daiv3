# Daiv3.OnlineProviders.Anthropic – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Anthropic Claude cloud provider implementation. Implements `IOnlineProvider` from `Daiv3.OnlineProviders.Abstractions` using a custom `HttpClient`-based integration against the Anthropic Messages API.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- `AnthropicProvider` — implements `IOnlineProvider` for Anthropic Claude models
- Custom HTTP client integration (no third-party Anthropic SDK — evaluate via ADD if ever reconsidered)
- Anthropic API versioning headers (`anthropic-version`)
- Request/response mapping (Anthropic uses a distinct message format from OpenAI)
- Streaming support via Server-Sent Events (SSE)

## Security

- API keys MUST come from `IConfiguration` — never hardcoded
- Never log the API key — log only the model identifier and response status
- See AI-Instructions.md § 9 for the full secrets management guidance

## Note on Official SDK

As of project creation, an official Anthropic .NET SDK was under evaluation. If you are reconsidering adding it, create an Architecture Decision Document (ADD) first and get explicit user approval before adding the package.

## Test Projects

All three online provider implementations share one test project:

```powershell
dotnet test tests/unit/Daiv3.OnlineProviders.Tests/Daiv3.OnlineProviders.Tests.csproj --nologo --verbosity minimal
```
