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

## DI Registration Pattern for Repository Interfaces

**CRITICAL:** Services that depend on `IRepository<TEntity>` will fail at runtime if only concrete repository types are registered.

### Pattern

When creating a MAUI service that uses repositories:

1. **Check registration** in `src/Daiv3.Persistence/PersistenceServiceExtensions.cs`:
   ```csharp
   services.AddScoped<IRepository<EntityType>>(sp => sp.GetRequiredService<EntityTypeRepository>());
   ```

2. **If missing, add it** to the `AddPersistence()` method after concrete repository registration:
   ```csharp
   // Register concrete repository
   services.AddScoped<ProjectRepository>();
   
   // Register interface-to-implementation mapping (REQUIRED for MAUI service DI)
   services.AddScoped<IRepository<Project>>(sp => sp.GetRequiredService<ProjectRepository>());
   ```

3. **Common entities needing registration:**
   - `IRepository<Project>` → `ProjectRepository`
   - `IRepository<ProjectTask>` → `TaskRepository`
   - `IRepository<Agent>` → `AgentRepository`
   - `IRepository<Document>` → `DocumentRepository`

4. **Validation:**
   - Run the MAUI app: `run-maui.bat`
   - Navigate to the feature page/tab
   - If app crashes with "Unable to resolve service for type 'Daiv3.Persistence.IRepository<...>'", the interface registration is missing

### Historical Reference

**CT-REQ-014 Calendar Crash (March 2026):**  
Calendar tab crashed on navigation because `CalendarService` required `IRepository<Project>` and `IRepository<ProjectTask>`, but only `ProjectRepository` and `TaskRepository` were registered. Fixed by adding interface registrations to `PersistenceServiceExtensions.cs`.

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal
```
