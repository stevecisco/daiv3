# Daiv3.FoundryLocal.Management.Cli – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Standalone command-line utility for managing Microsoft Foundry Local models and configurations. Useful for DevOps tasks, model provisioning scripts, and troubleshooting outside of the main application.

## Project Type

CLI (Console executable)

## Target Framework

Follows the dual-TFM pattern — inherits Windows vs. cross-platform behaviour from `Daiv3.FoundryLocal.Management`.

## Key Responsibilities

- List available models in the Foundry Local catalogue
- Download/remove specific models
- Show Foundry Local service status and hardware capabilities
- Provide a scriptable interface for CI/CD model provisioning

## Conventions

- Uses `System.CommandLine` for argument parsing (consistent with `Daiv3.App.Cli`)
- All output should be human-readable; add `--json` flag for machine-readable output where useful
- Document all commands in `Docs/CLI-Command-Examples.md` after implementation

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.FoundryLocal.Management` | Core logic (this CLI is a thin wrapper) |

## Test Projects

No dedicated test project. CLI integration is covered indirectly via `Daiv3.FoundryLocal.Management.Tests`.

```powershell
dotnet test tests/unit/Daiv3.FoundryLocal.Management.Tests/Daiv3.FoundryLocal.Management.Tests.csproj --nologo --verbosity minimal
```
