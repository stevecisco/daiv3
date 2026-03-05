# Daiv3.FoundryLocal.Management – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Manages the full Microsoft Foundry Local SDK lifecycle: model catalogue, model download, initialisation, session management, and health monitoring. This is the reference implementation for the dual-TFM platform optimization pattern used project-wide.

## Project Type

Library — **multi-TFM (reference implementation)**

## Target Framework

```xml
<TargetFramework>net10.0</TargetFramework>
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

Conditional package references:
- `net10.0` → `Microsoft.AI.Foundry.Local` (cross-platform)
- `net10.0-windows10.0.26100` → `Microsoft.AI.Foundry.Local.WinML` (Windows-optimised)

## Key Responsibilities

- Model catalogue discovery and filtering
- Download-on-demand with progress reporting
- Foundry Local process lifecycle (start, health-check, shutdown)
- Session factory and request routing
- Hardware capability readout for model selection guidance

## Platform Notes

- Windows-specific features (WinML, NPU) are handled via `#if NET10_0_WINDOWS10_0_26100_OR_GREATER` guards
- Always provide a functional cross-platform fallback path
- See the dual-TFM pattern in AI-Instructions.md § 2 — this project is the canonical example

## Test Projects

```powershell
# Unit tests
dotnet test tests/unit/Daiv3.FoundryLocal.Management.Tests/Daiv3.FoundryLocal.Management.Tests.csproj --nologo --verbosity minimal

# Integration tests (requires Foundry Local installed locally)
dotnet test tests/integration/Daiv3.FoundryLocal.IntegrationTests/Daiv3.FoundryLocal.IntegrationTests.csproj --nologo --verbosity minimal
```
