# Daiv3.FoundryLocal.Bridge – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Abstraction bridge for the Microsoft Foundry Local SDK. Decouples the rest of the system from direct SDK dependency via interfaces and extension methods, enabling unit testing of `Daiv3.ModelExecution` without requiring a live Foundry Local installation.

## Project Type

Library

## Target Framework

Follows the dual-TFM pattern used by `Daiv3.FoundryLocal.Management`:

```xml
<TargetFramework>net10.0</TargetFramework>
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

## Key Responsibilities

- `IFoundryLocalClient` interface — abstracts the SDK surface consumed by `Daiv3.ModelExecution`
- `FoundryLocalClientAdapter` — wraps the real SDK; returned only when Foundry Local is installed
- Extension methods for DI registration
- Null/no-op implementations for test scenarios

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.FoundryLocal.Management` | Uses this bridge internally for SDK calls |
| `Daiv3.ModelExecution` | Depends on the abstraction interface |

## Test Projects

Bridge logic is tested via `Daiv3.FoundryLocal.Management.Tests`:

```powershell
dotnet test tests/unit/Daiv3.FoundryLocal.Management.Tests/Daiv3.FoundryLocal.Management.Tests.csproj --nologo --verbosity minimal
```
