# Daiv3.App.Maui – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Windows 11 desktop application providing the primary graphical user interface for DAIv3. Built with .NET MAUI targeting Windows 10.0.26100 (Copilot+ devices). Implements features that have **already been validated via the CLI**.

## Project Type

MAUI Desktop application (Windows only)

## Target Framework

```xml
<TargetFramework>net10.0-windows10.0.26100</TargetFramework>
```

## Key Responsibilities

- Chat interface for interacting with the orchestration engine
- Knowledge base browser and document ingestion UI
- Model management dashboard (status, download, hardware utilisation)
- Task history and learning insights display
- Settings and configuration UI

## Critical Rule: CLI First

**Never implement a MAUI feature for functionality that hasn't been validated in `Daiv3.App.Cli` first.** The CLI implementation validates correctness, edge cases, and logging before the more complex MAUI UI is built. See AI-Instructions.md § 6.

## MAUI Conventions

- Use MVVM pattern with `CommunityToolkit.Mvvm` (if approved — check `approved-dependencies.md`)
- All `ViewModel` classes inject services via constructor DI
- No business logic in code-behind or ViewModels — delegate to library services
- Use `ILogger<T>` in ViewModels; log to `%LOCALAPPDATA%\Daiv3\logs\maui-YYYY-MM-DD.log`
- Handle `OperationCanceledException` gracefully in all async UI operations

## Warning Regression Guardrails

- Do not introduce obsolete stack expansion options (`StartAndExpand`, `EndAndExpand`); prefer `Grid` column/row layout for expansion and alignment.
- For `CancellationTokenSource` fields in services/viewmodels, dispose previous instance before reassignment:
	- `_cts?.Dispose(); _cts = new CancellationTokenSource();`
	- `_linkedCts?.Dispose(); _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);`
- Validate MAUI warning delta before completion:
	- `dotnet build src/Daiv3.App.Maui/Daiv3.App.Maui.csproj --nologo --verbosity minimal`

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal
```
