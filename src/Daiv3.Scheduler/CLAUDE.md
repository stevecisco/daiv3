# Daiv3.Scheduler – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Background task scheduler with support for recurring jobs, one-shot deferred tasks, and model lifecycle event triggers. Provides the timing infrastructure consumed by `Daiv3.Orchestration` and `Daiv3.WebFetch.Crawl` for scheduled operations.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- `IScheduler` — add, remove, and list scheduled jobs
- Recurring job support (cron-style or interval-based)
- One-shot deferred execution with cancellation support
- Model lifecycle event hooks (e.g., "warm up model at 08:00 daily")
- Hosted service integration (`IHostedService`) for use in Worker and CLI

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.Orchestration` | Uses scheduler for periodic workflow triggers |
| `Daiv3.WebFetch.Crawl` | Uses scheduler for crawl recurrence |
| `Daiv3.Worker` | Hosts the scheduler `IHostedService` |
| `Daiv3.App.Cli` | Can trigger/list scheduled jobs manually |

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.Scheduler.Tests/Daiv3.Scheduler.Tests.csproj --nologo --verbosity minimal
```
