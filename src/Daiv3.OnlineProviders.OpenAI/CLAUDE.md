# Daiv3.OnlineProviders.OpenAI – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

OpenAI cloud provider implementation. Implements `IOnlineProvider` from `Daiv3.OnlineProviders.Abstractions` using a custom `HttpClient`-based integration against the OpenAI REST API.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- `OpenAiProvider` — implements `IOnlineProvider` for chat completions and streaming
- `IHttpClientFactory` usage for connection pooling and retry (no raw `new HttpClient()`)
- Request/response mapping to/from OpenAI API format
- API key loading from configuration (user secrets in dev, environment variables in production — never hardcoded)

## Security

- API keys MUST come from `IConfiguration` (user secrets / env vars)
- Never log API keys — log only provider name and response status codes
- See AI-Instructions.md § 9 for the full secrets management guidance

## Test Projects

All three online provider implementations share one test project:

```powershell
dotnet test tests/unit/Daiv3.OnlineProviders.Tests/Daiv3.OnlineProviders.Tests.csproj --nologo --verbosity minimal
```

Unit tests use mock/stub HTTP message handlers — no real API calls in unit tests.
