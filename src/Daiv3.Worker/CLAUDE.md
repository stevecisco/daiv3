# Daiv3.Worker – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Background worker service. Hosts the orchestrator and scheduler as long-running `IHostedService` instances for headless/server deployments. Processes queued tasks, manages model lifecycle events, and runs recurring scheduled jobs without any UI.

## Project Type

Worker Service (Console executable — `Microsoft.Extensions.Hosting` generic host)

## Target Framework

`net10.0-windows10.0.26100` (inherits hardware-acceleration features from referenced libraries).

## Key Responsibilities

- Host `Daiv3.Orchestration` task processing loop as `IHostedService`
- Host `Daiv3.Scheduler` recurring job runner as `IHostedService`
- Graceful shutdown handling (`CancellationToken`-aware)
- Structured logging to file via `ILogger<T>`
- Health check endpoint (optional) for deployment monitoring

## Conventions

- All services registered via DI extension methods from the respective libraries
- No business logic lives here — this is a hosting container only
- Use `appsettings.json` + environment variables for configuration; user secrets not applicable for worker deployments

## Graceful Shutdown

- All `ExecuteAsync` overrides MUST respect the passed `CancellationToken`
- Long-running loops: `while (!stoppingToken.IsCancellationRequested)`
- Unhandled exceptions in hosted services should be logged at Critical level

## Test Projects

No dedicated unit test project for the worker itself. Test the hosted services' logic via the respective library test projects:

```powershell
dotnet test tests/unit/Daiv3.Orchestration.Tests/ --nologo --verbosity minimal
dotnet test tests/unit/Daiv3.Scheduler.Tests/ --nologo --verbosity minimal
```
