# VS Code Copilot Instructions for DAIv3 Project

> **📌 VS Code Specific:** This file contains VS Code-specific instructions. For complete project guidelines, see the shared instructions document.

---

## Quick Reference

This project uses comprehensive AI assistant guidelines. For complete instructions, see:

**📘 [Complete AI Development Guidelines](../Docs/AI-Instructions.md)**

The shared instructions document contains all critical development principles, workflows, and standards.

---

## Project Overview

**DAIv3** - Distributed AI System Version 3

A comprehensive distributed AI system with support for local model execution, vector search, knowledge management, and intelligent task orchestration on Windows 11 Copilot+ devices.

---

## VS Code Specific Notes

### Recommended Extensions
- C# Dev Kit
- GitHub Copilot
- GitHub Copilot Chat
- .NET Install Tool
- EditorConfig for VS Code

### VS Code Settings
See `.vscode/settings.json` for project-specific settings.

### VS Code Tasks
Common tasks are defined in `.vscode/tasks.json`:
- Build solution
- Run tests
- Clean solution

### Launch Configurations
Debug configurations in `.vscode/launch.json`:
- CLI Application
- MAUI Application
- Unit Tests
- Integration Tests

---

## Essential Principles (Quick Reference)

For complete details, see **[AI-Instructions.md](../Docs/AI-Instructions.md)**.

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

**Version:** 2.0  
**Last Updated:** February 22, 2026  
**Status:** Active - VS Code-specific instructions (references shared guidelines)  

**📖 For complete details, see [AI-Instructions.md](../Docs/AI-Instructions.md)**
