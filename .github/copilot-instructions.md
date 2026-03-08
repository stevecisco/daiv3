# GitHub Copilot Instructions for DAIv3 Project

> **🎯 Universal Instructions:** This file is used by GitHub Copilot across **all IDEs** (VS Code, Visual Studio, JetBrains, etc.) and by **GitHub Copilot Workspace** for PR generation and background tasks.

---

## Quick Reference

This project uses comprehensive AI assistant guidelines. For complete instructions, see:

**📘 [Complete AI Development Guidelines](../Docs/AI-Instructions.md)**

The shared instructions document contains:
- Critical development principles
- Code quality & testing requirements
- Target framework configuration patterns
- Dependency management philosophy
- Documentation-driven development workflow
- Logging & error handling standards
- Build & testing commands

---

## Project Overview

**DAIv3** - Distributed AI System Version 3

A comprehensive distributed AI system with support for:
- Local model execution on Windows 11 Copilot+ devices
- NPU/DirectML hardware acceleration
- Vector search and knowledge management
- Intelligent task orchestration

**Target Platform:** Windows 11 Copilot+, .NET 10  
**Target Hardware:** NPU (primary), GPU (fallback), CPU (final fallback)

---

## Essential Principles (Quick Reference)

### Before Writing Code
1. **Review requirements** in `./Docs/Requirements/Reqs/`
2. **Check approved dependencies** in `./Docs/Requirements/Architecture/approved-dependencies.md`
3. **Update requirement docs** with implementation design
4. **Ask clarifying questions** if 98%+ certainty not achieved

### Code Quality Gates
- ✅ All code **must compile** without errors
- ✅ Warnings are tracked via `Docs/Build-Warnings-Errors-Tracker.md` (baseline + per-requirement deltas)
- ✅ All **unit tests must pass** (100% passing before completion)
- ✅ All **integration tests must pass** (for DB, file I/O, network operations)
- ✅ **Tests MUST be created** during implementation (not after)
- ✅ Use **dependency injection** and testable patterns
- ✅ Implement **comprehensive logging** with `ILogger<T>`
- ✅ Implement **proper error handling** at all boundaries
- ✅ Verify **resource cleanup** (no connection/file leaks)

### Testing Requirements (MANDATORY)
**Tests are NOT optional - they are part of the implementation:**
1. Create **unit tests immediately** after implementing code (same session)
2. Create **integration tests** for infrastructure components (DB, file I/O, external services)
3. **All tests MUST pass** before marking feature complete
4. Document any **test failures as BLOCKING issues** in requirement doc
5. Never mark requirement "Complete" with failing tests

### Testing Strategy
1. Implement code → 2. Create unit tests → 3. Create integration tests → 4. Verify all pass → 5. CLI validation → 6. **Document CLI commands** → 7. MAUI implementation

### Project-Scoped Test Execution (During Development)
Tests are broken out per project. **Run only the test projects associated with the modified source project(s)** during active development — do NOT run the full suite on every change.

**Single project example:**
```
dotnet test tests/unit/Daiv3.Persistence.Tests/Daiv3.Persistence.Tests.csproj --nologo --verbosity minimal
```

**Multiple modified projects — run each corresponding test project:**
```
dotnet test tests/unit/Daiv3.Knowledge.Tests/... tests/unit/Daiv3.Orchestration.Tests/... --nologo
```

See [AI-Instructions.md § 3.10](../Docs/AI-Instructions.md) for the complete project → test project mapping table.

### Full Suite Test Execution Rule
- **Run the full suite only when a requirement is complete**, as the regression gate before marking complete or committing.
- For "run full suite" requests or pre-completion verification, always run:
   - `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Do not rely only on IDE/editor test tooling counts for this repository; it can under-discover tests.
- If totals are suspiciously low versus known baseline, rerun via solution-level command before reporting.

### Warning/Error Delta Workflow (MANDATORY)
1. Capture/confirm baseline from `Docs/Build-Warnings-Errors-Tracker.md`
2. After each requirement, rerun build/tests and compare warning/error deltas
3. Fix any net-new errors and warnings introduced by the requirement
4. If unresolved after up to 3 attempts, ask user whether to track as temporary debt or continue remediation
5. When a warning pattern is resolved, add a prevention note to shared AI instructions/tracker to avoid repeating it
6. Never claim "baseline warnings only" without same-session command evidence
   - Run: `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal 2>&1 | Tee-Object -FilePath temp/build-warnings-<date>.txt`
   - Verify: `Select-String temp/build-warnings-<date>.txt -Pattern ': warning ' | Measure-Object`
7. MAUI-specific regression checks for UI changes
   - Avoid obsolete `StartAndExpand` / `EndAndExpand` layout options; use `Grid` layout patterns
   - Dispose previous `CancellationTokenSource` before reassigning (`IDISP003` prevention)

### Git Commit Strategy (MANDATORY)
**When asked to work on multiple requirements, implement them ONE AT A TIME in sequential order:**
1. ✅ Implement first requirement **completely** (code + tests + docs)
2. ✅ Update requirement doc and Master-Implementation-Tracker.md
3. ✅ **Clean temporary files** before staging (CRITICAL STEP)
4. ✅ **Create git commit** for that requirement (stage only related files)
5. ✅ **Then** proceed to next requirement
6. ✅ Repeat until all requirements complete

**Key Rules:**
- Create **one commit per completed requirement** (not one commit at end)
- Commit **immediately** after requirement completion (do not batch)
- **BEFORE staging**: Remove ALL temporary artifacts created during development/testing
   - Examples: `temp/*_output.txt`, `temp/*_results.txt`, scratch logs, debug files, test captures
   - Command: `git status --short` to identify untracked files in `temp/` and workspace
   - Delete: `Remove-Item temp/file1.txt, temp/file2.txt -ErrorAction SilentlyContinue`
   - **Purpose:** Keep repository clean and focused; prevent accidental commits of temporary test artifacts
- Stage **only requirement-related files** (avoid `git add .` in multi-requirement work)
- Use format: `<REQ-ID> - Brief description`
- See [AI-Instructions.md](../Docs/AI-Instructions.md) § 5. Git Commits for Multi-Requirement Work for complete workflow

### Debugging Best Practices
- ⚠️ **NEVER use `[System.Reflection.Assembly]::LoadFrom()` in PowerShell** - locks DLLs and prevents compilation
- ⚠️ **NEVER use `Add-Type -Path` in terminals** - same issue, requires VS Code restart
- ✅ Use `dotnet build --verbosity detailed` for build diagnostics instead
- ✅ Use `Console.WriteLine()` or `ILogger` for runtime diagnostics
- ✅ Create dedicated test programs instead of runtime reflection
- 🔧 If DLL locked: Restart VS Code immediately, don't waste time retrying builds

### Dependency Management
- ⚠️ **Check `approved-dependencies.md` BEFORE adding or upgrading ANY dependency**
- ⚠️ **Create Architecture Decision Document (ADD)** for external libraries
- ⚠️ **Wait for approval** before adding non-Microsoft dependencies
- ✅ Microsoft packages (.NET, Azure, ML.NET) are pre-approved

---

## Key Documentation

| Document | Purpose |
|----------|---------|
| [AI-Instructions.md](../Docs/AI-Instructions.md) | Complete development guidelines (READ THIS FIRST) |
| [Build-Warnings-Errors-Tracker.md](../Docs/Build-Warnings-Errors-Tracker.md) | Warning/error baseline and per-requirement delta log |
| [Master-Implementation-Tracker.md](../Docs/Requirements/Master-Implementation-Tracker.md) | Primary tracking document |
| [CLI-Command-Examples.md](../Docs/CLI-Command-Examples.md) | CLI command reference (UPDATE when adding CLI commands) |
| [approved-dependencies.md](../Docs/Requirements/Architecture/approved-dependencies.md) | Dependency approval registry |
| [architecture-overview.md](../Docs/Requirements/Architecture/architecture-overview.md) | System architecture |

---

## Common Build & Test Commands

**CRITICAL: Always run dotnet commands from the workspace root directory with full paths.**

```bash
# Build solution (from root directory)
dotnet build Daiv3.FoundryLocal.slnx

# Optional strict audit build (do not use as default gate)
dotnet build Daiv3.FoundryLocal.slnx /p:TreatWarningsAsErrors=true

# Build specific project (from root directory with full path)
dotnet build src/Daiv3.FoundryLocal.Management/Daiv3.FoundryLocal.Management.csproj

# Run all tests (from root directory)
dotnet test Daiv3.FoundryLocal.slnx

# Run with verbose output (from root directory)
dotnet test Daiv3.FoundryLocal.slnx --verbosity detailed
```

**Note:** Always use full relative paths from workspace root. Never change directories before running dotnet commands.

---

## GitHub Copilot Workspace Specific

When working on pull requests or background tasks:

1. **Always reference the shared instructions:** `./Docs/AI-Instructions.md`
2. **Update tracking documents:**
   - `./Docs/Requirements/Master-Implementation-Tracker.md`
   - Individual requirement docs in `./Docs/Requirements/Reqs/`
3. **Check dependencies:** `./Docs/Requirements/Architecture/approved-dependencies.md`
4. **Include test coverage** in all PRs
5. **Verify builds pass** before submitting

---

## For Sub-Agents & Background Tasks

When spawning autonomous agents or background processes, provide:

```
AI Instructions: ./Docs/AI-Instructions.md
Requirement Document: ./Docs/Requirements/Reqs/[REQ-ID].md
Master Tracker: ./Docs/Requirements/Master-Implementation-Tracker.md
Dependency Registry: ./Docs/Requirements/Architecture/approved-dependencies.md
```

See [AI-Instructions.md](../Docs/AI-Instructions.md) for the complete sub-agent handoff template.

---

## Critical Rules

### ✅ DO
- Reference shared instructions for complete guidance
- Compile without errors
- Track warning/error deltas in `Docs/Build-Warnings-Errors-Tracker.md`
- Pass all tests before completion
- Check approved-dependencies.md before adding packages
- Use structured logging with ILogger<T>
- Test in CLI before implementing in MAUI
- Update CLI-Command-Examples.md when adding CLI commands

### ❌ DON'T
- Skip requirement document reviews
- Add dependencies without checking approved-dependencies.md
- Upgrade dependencies without approval
- Use Console.WriteLine for logging
- Implement MAUI before CLI validation
- Make assumptions - ask clarifying questions

---

**Version:** 1.6  
**Last Updated:** March 6, 2026  
**Status:** Active - Repository-wide GitHub Copilot instructions  
**Detailed Instructions Version:** [AI-Instructions.md v2.0](../Docs/AI-Instructions.md)

**Recent Updates:**
- Added mandatory same-session warning-proof evidence commands before claiming baseline-only warnings
- Added MAUI analyzer guardrails for deprecated `StartAndExpand` and `CancellationTokenSource` reassignment disposal
- Added mandatory warning/error baseline + delta workflow and tracker reference (`Docs/Build-Warnings-Errors-Tracker.md`)
- Clarified default build gate: errors block completion, warnings are tracked and remediated via delta process
- **CRITICAL:** Added explicit sequential multi-requirement workflow - implement ONE AT A TIME, commit after EACH requirement
- Added explicit commit-per-requirement policy with requirement-scoped staging guidance
- Added critical guidance on PowerShell command syntax (-Last parameter, not tail)
- Added pattern for reading from existing log files instead of re-piping console output
- Improved diagnostic efficiency guidelines
