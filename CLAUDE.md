# DAIv3 – Claude AI Instructions

> **Full development guidelines:** [Docs/AI-Instructions.md](Docs/AI-Instructions.md)
> This file is the quick-reference entry point. Always load the full guidelines for complete detail.

---

## Project Overview

**DAIv3** – Distributed AI System Version 3  
A comprehensive distributed AI system for local model execution, vector search, knowledge management, and intelligent task orchestration on Windows 11 Copilot+ devices.

**Target platform:** Windows 11 Copilot+, .NET 10  
**Target hardware:** NPU (primary), GPU (fallback), CPU (final fallback)  
**Solution file:** `Daiv3.FoundryLocal.slnx` (always build/test from workspace root)

---

## Source Projects (`src/`)

| Project | Type | Purpose |
|---------|------|---------|
| `Daiv3.Api` | ASP.NET API | REST API surface – orchestration & persistence |
| `Daiv3.App.Cli` | CLI | Command-line interface for all system operations |
| `Daiv3.App.Maui` | Desktop (MAUI) | Windows 11 desktop UI (implement **after** CLI validation) |
| `Daiv3.Core` | Library | Domain models, contracts, shared abstractions |
| `Daiv3.FoundryLocal.Bridge` | Library | Abstraction bridge to Microsoft Foundry Local SDK |
| `Daiv3.FoundryLocal.Management` | Library | Foundry Local lifecycle management (download, init, sessions) |
| `Daiv3.FoundryLocal.Management.Cli` | CLI | Standalone Foundry model management utility |
| `Daiv3.Infrastructure.Shared` | Library | Shared infrastructure: logging, HTTP factory, DI helpers |
| `Daiv3.Knowledge` | Library | Knowledge management orchestration, two-tier vector search |
| `Daiv3.Knowledge.DocProc` | Library | Document processing (PDF, Office, Markdown) |
| `Daiv3.Knowledge.Embedding` | Library | ONNX embedding generation with NPU/DirectML support |
| `Daiv3.Mcp.Integration` | Library | Model Context Protocol (MCP) integration layer |
| `Daiv3.ModelExecution` | Library | Local SLM execution via Foundry Local, hardware-aware routing |
| `Daiv3.OnlineProviders.Abstractions` | Library | Base interfaces for cloud AI provider integrations |
| `Daiv3.OnlineProviders.Anthropic` | Library | Anthropic Claude provider implementation |
| `Daiv3.OnlineProviders.AzureOpenAI` | Library | Azure OpenAI provider implementation |
| `Daiv3.OnlineProviders.OpenAI` | Library | OpenAI provider implementation |
| `Daiv3.Orchestration` | Library | Multi-step workflow orchestration, agent execution, tools routing |
| `Daiv3.Persistence` | Library | SQLite repository layer, schema migrations, application state |
| `Daiv3.Scheduler` | Library | Background task scheduler with recurring job support |
| `Daiv3.WebFetch.Crawl` | Library | Web crawling, HTML→Markdown conversion, scheduled ingestion |
| `Daiv3.Worker` | Worker | Background worker hosting orchestrator & scheduler services |

Each project has its own `CLAUDE.md` under `src/<ProjectName>/CLAUDE.md` with project-specific detail.

---

## Non-Negotiable Rules

1. **Read requirements first.** Review `Docs/Requirements/Reqs/` before writing any code.
2. **98% certainty rule.** Ask clarifying questions rather than guessing.
3. **Code must compile.** Zero build errors before marking anything complete.
4. **Tests are mandatory.** Write tests in the same session as implementation — not after.
5. **All tests must pass** (excluding explicit `[Fact(Skip=…)]`) before marking complete or committing.
6. **Check approved-dependencies.md** before adding or upgrading any NuGet package.
7. **Never downgrade packages** without explicit user approval.
8. **One commit per completed requirement.** Implement sequentially, commit after each one.
9. **CLI before MAUI.** Validate every user-facing feature in CLI before building the MAUI equivalent.
10. **Clean temporary files before committing.** Remove all temporary artifacts (test outputs, debug logs, scratch files) from `temp/` or workspace before staging/committing.
11. **Update Master-Implementation-Tracker.md** before yielding back to the user.

---

## Code Quality Gates

- ✅ Compiles without errors
- ✅ Warning/error delta tracked in `Docs/Build-Warnings-Errors-Tracker.md`
- ✅ Warning claims are backed by same-session command evidence (`temp/build-warnings-<date>.txt`)
- ✅ `ILogger<T>` used for all logging (no `Console.WriteLine` in production code)
- ✅ Error handling at all public API boundaries
- ✅ Dependency injection throughout; no hard-coded dependencies
- ✅ Proper `IDisposable`/`IAsyncDisposable` resource cleanup
- ✅ Structured logging with named parameters (not string interpolation)

Warning regression-proof commands (run from repo root):

```powershell
dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal 2>&1 | Tee-Object -FilePath temp/build-warnings-<date>.txt
Select-String temp/build-warnings-<date>.txt -Pattern ': warning ' | Measure-Object
```

MAUI analyzer guardrails:
- Do not use `StartAndExpand` / `EndAndExpand`; use `Grid`-based layout expansion.
- Dispose prior `CancellationTokenSource` before any reassignment to avoid `IDISP003`.

---

## Testing Strategy

### During Development – Run Project-Scoped Tests Only

Run only the tests for the project(s) you modified. Do **not** run the full suite on every change.

| Modified project(s) | Run these tests |
|---|---|
| `Daiv3.Persistence` | `tests/unit/Daiv3.Persistence.Tests/` + `tests/integration/Daiv3.Persistence.IntegrationTests/` |
| `Daiv3.Knowledge` / `Daiv3.Knowledge.DocProc` | `tests/unit/Daiv3.Knowledge.Tests/` + `tests/integration/Daiv3.Knowledge.IntegrationTests/` |
| `Daiv3.Knowledge.Embedding` | `tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/` |
| `Daiv3.Orchestration` | `tests/unit/Daiv3.Orchestration.Tests/` + `tests/integration/Daiv3.Orchestration.IntegrationTests/` |
| `Daiv3.FoundryLocal.*` | `tests/unit/Daiv3.FoundryLocal.Management.Tests/` + `tests/integration/Daiv3.FoundryLocal.IntegrationTests/` |
| `Daiv3.Persistence` | `tests/unit/Daiv3.Persistence.Tests/` + `tests/integration/Daiv3.Persistence.IntegrationTests/` |
| `Daiv3.Scheduler` | `tests/unit/Daiv3.Scheduler.Tests/` |
| `Daiv3.ModelExecution` | `tests/unit/Daiv3.ModelExecution.Tests/` |
| `Daiv3.Mcp.Integration` | `tests/unit/Daiv3.Mcp.Integration.Tests/` |
| `Daiv3.OnlineProviders.*` (any) | `tests/unit/Daiv3.OnlineProviders.Tests/` |
| `Daiv3.WebFetch.Crawl` | `tests/unit/Daiv3.WebFetch.Crawl.Tests/` |
| `Daiv3.App.Cli` | `tests/unit/Daiv3.App.Cli.Tests/` |
| `Daiv3.App.Maui` | `tests/unit/Daiv3.App.Maui.Tests/` |
| `Daiv3.Infrastructure.Shared` | `tests/unit/Daiv3.Infrastructure.Shared.Tests/` |
| Cross-cutting / architecture | `tests/unit/Daiv3.Architecture.Tests/` + `tests/integration/Daiv3.Architecture.Integration.Tests/` |

```powershell
# Single project example
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo --verbosity minimal

# Multiple projects
dotnet test `
  tests/unit/Daiv3.Knowledge.Tests/Daiv3.Knowledge.Tests.csproj `
  tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj `
  --nologo --verbosity minimal
```

### Requirement Complete – Run Full Suite (Regression Gate)

Before marking a requirement complete **and** before committing:

```powershell
dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal
# Exit code MUST be 0
```

---

## Build Commands

All commands run from the **workspace root** (`c:\_prj\stevecisco\private\daiv3`).

```powershell
# Build entire solution
dotnet build Daiv3.FoundryLocal.slnx --nologo

# Build specific project
dotnet build src/Daiv3.Persistence/Daiv3.Persistence.csproj --nologo

# Full test suite (use at requirement completion only)
dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal

# Canonical test runner (wraps the above)
.\run-tests.bat
```

---

## Git Commit Strategy

**One commit per requirement. Commit immediately after completion. Never batch.**

```powershell
# Check what you're about to stage
git status --short

# Stage only files for this requirement (avoid git add .)
git add <file1> <file2> ...

# Commit with requirement ID
git commit -m "REQ-XXX - Brief description"
```

---

## Target Framework Pattern

Libraries with platform-specific features use dual TFM targeting:

```xml
<TargetFramework>net10.0</TargetFramework>
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

- Test projects: `net10.0` only (never multi-target test projects)
- Pure domain libraries with no platform dependencies: `net10.0` only

---

## Dependency Management

1. **Check** `Docs/Requirements/Architecture/approved-dependencies.md` before adding or upgrading anything.
2. **Pre-approved (no ADD needed):** All `Microsoft.*`, `Azure.*`, `System.*`, and .NET 10 framework packages.
3. **All others:** Create an Architecture Decision Document (ADD) in `Docs/Requirements/Architecture/decisions/` and wait for explicit user approval.
4. **Never downgrade** a package without explicit approval. Analyse and present alternatives first.

---

## Key Documentation Files

| File | Purpose |
|------|---------|
| [Docs/AI-Instructions.md](Docs/AI-Instructions.md) | **Complete guidelines** — authoritative source |
| [Docs/Build-Warnings-Errors-Tracker.md](Docs/Build-Warnings-Errors-Tracker.md) | Warning/error baseline & per-requirement deltas |
| [Docs/Requirements/Master-Implementation-Tracker.md](Docs/Requirements/Master-Implementation-Tracker.md) | Primary status dashboard |
| [Docs/CLI-Command-Examples.md](Docs/CLI-Command-Examples.md) | CLI command reference (update when adding commands) |
| [Docs/Requirements/Architecture/approved-dependencies.md](Docs/Requirements/Architecture/approved-dependencies.md) | Dependency approval registry |
| [Docs/Requirements/Architecture/architecture-overview.md](Docs/Requirements/Architecture/architecture-overview.md) | System architecture |
| [Docs/Requirements/Reqs/](Docs/Requirements/Reqs/) | Individual requirement documents |

---

## PowerShell Safety Rules

- ❌ **NEVER** `[System.Reflection.Assembly]::LoadFrom()` or `Add-Type -Path` — locks DLLs permanently until VS Code restart
- ❌ **NEVER** use `tail` — use `Get-Content -Tail N` or `Select-Object -Last N`
- ✅ Use `dotnet build --verbosity detailed` for build diagnostics
- ✅ Read existing log files instead of re-running the app: `%LOCALAPPDATA%\Daiv3\logs\`

---

## IDisposable Warning Prevention

When implementing `IDisposable` on any heavily-tested class, always proactively suppress `IDISP001` in test files in the **same commit**. Never suppress in production code — fix the disposal issue instead. See [Docs/AI-Instructions.md § 3.6](Docs/AI-Instructions.md) for full patterns.

---

## Feature Completion Checklist

Before marking any requirement complete:
- [ ] All code compiles without errors
- [ ] Warning/error delta validated (no unapproved net-new warnings)
- [ ] Unit tests created and passing
- [ ] Integration tests created and passing (if applicable)
- [ ] **Full suite run and exit code is 0**: `dotnet test Daiv3.FoundryLocal.slnx --nologo`
- [ ] CLI validated with realistic data (user-facing features)
- [ ] Requirement document updated with implementation details & test traceability
- [ ] Master-Implementation-Tracker.md updated to "Complete" (100%)
- [ ] Git commit created (requirement-scoped staging)
