# Daiv3.OnlineProviders.AzureOpenAI – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Azure OpenAI cloud provider implementation. Implements `IOnlineProvider` from `Daiv3.OnlineProviders.Abstractions` using `Azure.Identity` for authentication against an Azure OpenAI deployment endpoint.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- `AzureOpenAiProvider` — implements `IOnlineProvider` using the Azure OpenAI REST API
- `DefaultAzureCredential` (via `Azure.Identity`) for production authentication
- API key fallback for development environments
- Request/response mapping to/from Azure OpenAI chat completions format
- Endpoint and deployment name configuration

## Security

- API keys and endpoint URLs MUST come from `IConfiguration` — never hardcoded
- Prefer `DefaultAzureCredential` over API key auth in production deployments
- Never log credentials — log only endpoint base URL and response status codes
- See AI-Instructions.md § 9 for the full secrets management guidance

## Test Projects

All three online provider implementations share one test project:

```powershell
dotnet test tests/unit/Daiv3.OnlineProviders.Tests/Daiv3.OnlineProviders.Tests.csproj --nologo --verbosity minimal
```
