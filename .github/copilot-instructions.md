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
- ✅ All **unit tests must pass**
- ✅ Use **dependency injection** and testable patterns
- ✅ Implement **comprehensive logging** with `ILogger<T>`
- ✅ Implement **proper error handling** at all boundaries

### Testing Strategy
1. Unit tests → 2. Integration tests → 3. CLI validation → 4. MAUI implementation

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
| [approved-dependencies.md](../Docs/Requirements/Architecture/approved-dependencies.md) | Dependency approval registry |
| [architecture-overview.md](../Docs/Requirements/Architecture/architecture-overview.md) | System architecture |

---

## Common Build & Test Commands

**CRITICAL: Always run dotnet commands from the workspace root directory with full paths.**

```bash
# Build solution (from root directory)
dotnet build FoundryLocal.IntegrationTests.slnx

# Build with strict warnings (from root directory)
dotnet build FoundryLocal.IntegrationTests.slnx /p:TreatWarningsAsErrors=true

# Build specific project (from root directory with full path)
dotnet build src/FoundryLocal.Management/FoundryLocal.Management.csproj

# Run all tests (from root directory)
dotnet test FoundryLocal.IntegrationTests.slnx

# Run with verbose output (from root directory)
dotnet test FoundryLocal.IntegrationTests.slnx --verbosity detailed
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

### ❌ DON'T
- Skip requirement document reviews
- Add dependencies without checking approved-dependencies.md
- Upgrade dependencies without approval
- Use Console.WriteLine for logging
- Implement MAUI before CLI validation
- Make assumptions - ask clarifying questions

---

**Version:** 1.0  
**Last Updated:** February 22, 2026  
**Status:** Active - Repository-wide GitHub Copilot instructions  

**📖 For complete details, see [AI-Instructions.md](../Docs/AI-Instructions.md)**
