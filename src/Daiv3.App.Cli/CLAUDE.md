# Daiv3.App.Cli – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Primary command-line interface for DAIv3. Exposes all system capabilities (database management, knowledge ingestion, task execution, model bootstrapping, scheduling) as composable CLI commands using `System.CommandLine`. **All user-facing features must be validated here before implementing in MAUI.**

## Project Type

CLI (Console executable)

## Target Framework

Windows-targeted executable (inherits Windows-specific library features):
```xml
<TargetFramework>net10.0-windows10.0.26100</TargetFramework>
```

## Key Responsibilities

- `db init` / `db status` — database management commands
- `knowledge ingest` / `knowledge search` — document ingestion and retrieval
- `task run` — execute orchestration workflows
- `model list` / `model download` — Foundry Local model management
- `crawl` — trigger web content crawls
- All commands must provide meaningful `--help` output and `--json` output option where applicable

## CLI-First Development Rule

**Implement and test every user-facing feature here BEFORE building the MAUI equivalent.** This is mandatory — see AI-Instructions.md § 6.

## Conventions

- Use `System.CommandLine` v2+ with `CommandLineBuilder` pattern
- Each command is a separate class (not giant `Program.cs` switch)
- Return exit code 0 on success, non-zero on failure
- Use `ILogger<T>` for all output beyond direct user-facing messages
- Log to `%LOCALAPPDATA%\Daiv3\logs\cli-YYYY-MM-DD.log`

## Documentation Requirement

Every new CLI command MUST be documented in `Docs/CLI-Command-Examples.md` before being marked complete.

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.App.Cli.Tests/Daiv3.App.Cli.Tests.csproj --nologo --verbosity minimal
```
