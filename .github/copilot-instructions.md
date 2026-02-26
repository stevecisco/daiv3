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
- ✅ All code **must compile** without errors/warnings
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

# Build with strict warnings (from root directory)
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

See [AI-Instructions.md - Sub-Agent section](../Docs/AI-Instructions.md#sub-agent--contextualized-task-instructions) for complete handoff template.

---

## Critical Rules

### ✅ DO
- Reference shared instructions for complete guidance
- Compile without errors/warnings
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

**Version:** 1.2  
**Last Updated:** February 25, 2026  
**Status:** Active - Repository-wide GitHub Copilot instructions  
**Detailed Instructions Version:** [AI-Instructions.md v1.7](../Docs/AI-Instructions.md)

**Recent Updates:**
- Added critical guidance on PowerShell command syntax (-Last parameter, not tail)
- Added pattern for reading from existing log files instead of re-piping console output
- Improved diagnostic efficiency guidelines
