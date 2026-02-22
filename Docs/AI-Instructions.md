# DAIv3 - AI Assistant Development Guidelines

> **📌 Purpose:** This document contains comprehensive development guidelines for AI assistants working on the DAIv3 project. It should be referenced by all AI tools (GitHub Copilot, Claude, Cursor, etc.) and across all IDEs (VS Code, Visual Studio, etc.).

## Project Overview

**Project Name:** DAIv3 - Distributed AI System Version 3

**Purpose:** A comprehensive distributed AI system with support for local model execution, vector search, knowledge management, and intelligent task orchestration on Windows 11 Copilot+ devices.

---

## Critical Development Principles

### 1. Code Quality & Compilation
- **All code MUST compile without errors or warnings** before any feature is considered complete
- Code MUST adhere to C# naming conventions and .NET best practices
- Each code change must be validated to ensure it does not break existing functionality

### 2. Target Framework & Platform Configuration

**Follow the established TFM (Target Framework Moniker) pattern used in existing projects.**

**Standard Pattern (Multi-Targeting for Platform Optimization):**

All libraries and applications should use the conditional TFM pattern to support both cross-platform and Windows-specific optimizations:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>

<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

**When to Use This Pattern:**

1. **Hardware-Specific Libraries (NPU, DirectML, etc.):**
   - Libraries that interact with Windows-specific hardware (NPU via DirectML)
   - Conditional package references work at the library level
   - Platform-specific APIs are only available when building for Windows
   - Example: `Daiv3.Knowledge.Embedding` (uses DirectML for NPU acceleration)

2. **Platform-Optimized Implementations:**
   - Libraries with Windows-specific APIs or optimizations
   - Cross-platform base implementation with Windows enhancements
   - Allows graceful fallback on non-Windows platforms

3. **All Executable Projects:**
   - CLI applications: `Daiv3.App.Cli`
   - MAUI applications: `Daiv3.App.Maui`
   - Worker services: `Daiv3.Worker`
   - The executable's TFM determines which library capabilities are available

**Conditional Package References:**

Use conditional `<ItemGroup>` blocks to reference platform-specific packages:

```xml
<!-- Cross-platform packages -->
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="Microsoft.AI.Foundry.Local" />
</ItemGroup>

<!-- Windows-specific packages (with NPU/DirectML support) -->
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-windows10.0.26100'">
  <PackageReference Include="Microsoft.AI.Foundry.Local.WinML" />
  <PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" />
</ItemGroup>
```

**How Platform-Specific Features Work:**

- **Library-level TFM is sufficient** for platform-specific features (NPU, DirectML)
- Features do NOT need to propagate to top-level executable for hardware access
- When executable references a library:
  - If executable is `net10.0-windows*`: Windows-specific library version is used
  - If executable is `net10.0`: Cross-platform library version is used
- Platform detection and capability selection happens at library level

**When NOT to Use Multi-Targeting:**

- Pure domain/business logic libraries with no platform dependencies
- Libraries that only use cross-platform .NET APIs
- Test projects (use `net10.0` only)

**Build Verification:**

Add a build target to verify TFM selection:

```xml
<Target Name="ShowTargetFramework" BeforeTargets="Build">
  <Message Importance="High" Text="Building [ProjectName] for $(TargetFramework)" />
</Target>
```

**Platform-Specific Code Patterns:**

Use conditional compilation for platform-specific implementations:

```csharp
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
    // Windows-specific implementation with NPU/DirectML
    _logger.LogInformation("Using DirectML execution provider for NPU acceleration");
    session = new InferenceSession(modelPath, SessionOptions.MakeSessionOptionWithDirectML());
#else
    // Cross-platform fallback
    _logger.LogInformation("Using CPU execution provider (cross-platform)");
    session = new InferenceSession(modelPath);
#endif
```

**Summary:**
- Use dual-TFM pattern for all libraries with potential platform-specific features
- Hardware-specific features (NPU) work at library level, no propagation needed
- Executable's TFM determines which library implementation is used
- Always provide cross-platform fallback when possible
- See `FoundryLocal.Management` project as reference implementation

### 3. Testability & Testing Requirements
- **Code MUST be designed to be testable** - favor dependency injection, interfaces, and loose coupling
- **All unit tests MUST pass** before marking any feature as complete
- Aim for high test coverage on critical paths (orchestration, model execution, knowledge management)
- Tests must be meaningful and verify actual behavior, not just code coverage metrics
- Integration tests in `FoundryLocal.IntegrationTests` MUST pass for any changes affecting core APIs

### 4. Dependency & Library Management Philosophy

**Prefer Homegrown, Self-Contained Code:**
- **MINIMIZE external dependencies** to reduce attack surface and enable rapid bug fixes
- **PREFER implementing features in-house** unless the feature is:
  - Part of the .NET framework/runtime
  - An Azure service or SDK
  - A Microsoft-provided library
  - A Microsoft-recommended or officially supported library
- **ISOLATE specialized features** into their own dedicated libraries with supporting unit test projects
- **MAINTAIN control** over critical code paths to enable quick security patches and customizations

**External Dependency Decision Process:**

When considering an external NuGet package or library, you MUST:

1. **Default to Custom Implementation First:**
   - Assess if the feature can be reasonably implemented in-house
   - Consider long-term maintainability and control
   - Evaluate if the feature is core to our product's value proposition

2. **If External Package is Necessary:**
   - Create an architecture decision document in `./Docs/Requirements/Architecture/decisions/`
   - Document filename format: `ADD-YYYYMMDD-<feature-name>.md` (Architecture Decision Document)
   - Include the following sections:

   **Required Architecture Decision Document Sections:**
   ```markdown
   # Architecture Decision: <Feature Name>
   
   ## Context & Need
   - What feature/capability is needed?
   - Why is it needed?
   - What are the use cases?
   
   ## Decision
   - What approach was chosen? (Custom implementation vs. external library)
   
   ## Available Options
   
   ### Option 1: Custom Implementation
   **Pros:**
   - [List advantages]
   **Cons:**
   - [List disadvantages]
   **Estimated Effort:**
   - [S/M/L/XL with justification]
   
   ### Option 2: [Library Name 1]
   **Package:** [NuGet package name and link]
   **Version:** [Current stable version]
   **Last Updated:** [Package last updated date]
   **License:** [License type]
   **GitHub Stars / Downloads:** [Popularity metrics]
   **Maintainer:** [Individual/Organization]
   **Microsoft Affiliation:** [Yes/No - if yes, explain]
   
   **Pros:**
   - [Specific advantages]
   **Cons:**
   - [Security concerns, maintenance risks, etc.]
   **Pricing:**
   - [Free/Commercial/Enterprise pricing]
   **Security Considerations:**
   - [Known CVEs, security audit status, etc.]
   **Community & Support:**
   - [Active development, issue response time, etc.]
   **Key Discussions:**
   - [Link to relevant GitHub issues, blog posts, comparisons]
   
   ### Option 3: [Library Name 2]
   [Repeat same structure as Option 2]
   
   ## Comparison Matrix
   | Criteria | Custom | Library 1 | Library 2 |
   |----------|--------|-----------|-----------|
   | Security Control | ✅ Full | ⚠️ Limited | ⚠️ Limited |
   | Maintenance | [Assessment] | [Assessment] | [Assessment] |
   | Feature Fit | [Assessment] | [Assessment] | [Assessment] |
   | Learning Curve | [Assessment] | [Assessment] | [Assessment] |
   | Long-term Cost | [Assessment] | [Assessment] | [Assessment] |
   
   ## Recommendation
   [Clear recommendation with justification]
   
   ## Implementation Notes
   - If external library: isolation strategy, abstraction approach
   - If custom: high-level design approach
   
   ## Decision Date
   [Date]
   
   ## Decision Maker
   [Name/Role]
   
   ## Status
   [Proposed / Accepted / Rejected / Superseded]
   ```

3. **Check Dependency Registry Before Adding or Upgrading:**
   - **BEFORE adding ANY external dependency:**
     - Check `./Docs/Requirements/Architecture/approved-dependencies.md` for explicit approval
     - Verify version matches approved version (if applicable)
     - Check if dependency is in a pre-approved category
   - **BEFORE upgrading ANY dependency (including pre-approved ones):**
     - Check approved-dependencies.md for version-specific issues or restrictions
     - Create entry in "Pending Upgrades" section of approved-dependencies.md
      - Create Architecture Decision Document (ADD) for the upgrade (e.g., `ADD-YYYYMMDD-dependency-upgrade-<package>.md`) with release notes, breaking changes, security review, and test plan
      - Fetch and summarize release notes and breaking changes (NuGet and upstream release pages) and include them in the ADD implementation notes
     - Present upgrade justification, breaking changes, and security review to user
     - Wait for explicit approval before upgrading
   - **If dependency is NOT found and NOT in pre-approved category:**
     - STOP immediately
     - Create Architecture Decision Document (ADD)
     - Present to user for approval

4. **Halt Implementation Until Decision Approved:**
   - Create the architecture decision document (new dependency or upgrade)
   - Present it to the user for review
   - Wait for explicit approval before proceeding
   - Do NOT add the dependency or implement against it without approval
   - Once approved, update `approved-dependencies.md` with approval details

**Pre-Approved Categories (Auto-Approved):**

The following are automatically approved and do NOT require ADD or registry entry:
- **All .NET 10 framework packages** (System.*, Microsoft.Extensions.*, etc.)
- **All Microsoft official packages:**
  - Microsoft.Extensions.* (DI, Configuration, Logging, Hosting, etc.)
  - Microsoft.ML.* (ONNX Runtime, Tokenizers, DirectML, etc.)
  - Azure SDK packages (Azure.*, Microsoft.Azure.*)
  - Microsoft.Data.Sqlite
  - Microsoft.Extensions.AI
  - DocumentFormat.OpenXml
- **Foundry Local SDK** (project dependency)

**Important:** Even pre-approved packages require approval for version upgrades. Check release notes and security advisories before upgrading.

**Dependency Registry:**
- **Location:** `./Docs/Requirements/Architecture/approved-dependencies.md`
- **Purpose:** Single source of truth for all dependency decisions
- **Required:** Check this file before adding or upgrading ANY dependency

**For Everything Else:**
- Treat as requiring architecture decision review
- Create the decision document
- Get user approval

### 5. Documentation-Driven Development
- **BEFORE writing ANY code**, you MUST:
  1. Review the relevant requirement document(s) in `./Docs/Requirements/Reqs/`
  2. Review the corresponding specification document in `./Docs/Requirements/Specs/`
  3. Review the architecture documents in `./Docs/Requirements/Architecture/`
  4. Update the requirement document with:
     - Detailed implementation design
     - Task breakdown (specific, actionable items)
     - Technical approach and decisions
     - Acceptance criteria
     - Known constraints or limitations
  5. Update `./Docs/Requirements/Master-Implementation-Tracker.md` with initial status

### 6. CLI-First Testing Strategy

**Real-World Validation Before GUI Implementation:**

After unit tests and non-destructive integration tests pass, features MUST be validated in a real-world scenario using the CLI application before implementing UI in the MAUI application.

**Implementation Workflow:**
1. **Develop core library functionality** in appropriate library (e.g., `Daiv3.Knowledge`, `Daiv3.Orchestration`)
2. **Write and pass unit tests** for the library functionality
3. **Write and pass integration tests** (non-destructive only) for the library functionality
4. **Implement CLI command/feature** in `Daiv3.App.Cli` that exercises the library functionality
5. **Manually test via CLI** with real data and real-world scenarios
6. **Identify and fix bugs** discovered during CLI testing
7. **Update CLI implementation** based on lessons learned
8. **Only after CLI validation is complete:**
   - Design MAUI UI/UX for the feature
   - Implement MAUI application feature
   - MAUI implementation can leverage lessons learned from CLI experience

**Rationale:**
- CLI testing reveals integration issues earlier
- Real-world usage patterns expose edge cases not covered by unit tests
- Debugging is simpler in CLI vs. GUI
- CLI commands serve as working examples for MAUI implementation
- Reduces rework in complex MAUI UI implementation

**CLI Test Scenarios:**
- Test with realistic data volumes
- Test with edge cases (empty data, malformed input, etc.)
- Test error conditions and recovery
- Test performance with production-like loads
- Verify logging and error messages are helpful

### 7. Logging & Observability

**All libraries and applications MUST implement comprehensive, configurable logging and performance metrics.**

**Logging Requirements:**

1. **Use Microsoft.Extensions.Logging.Abstractions:**
   - All libraries MUST use `ILogger<T>` via dependency injection
   - Never use concrete logger implementations directly
   - Never use `Console.WriteLine` or similar for operational logging

2. **Log Levels (use appropriately):**
   - **Trace:** Extremely detailed diagnostic information (disabled by default)
   - **Debug:** Detailed information for debugging (disabled in production by default)
   - **Information:** General informational messages about application flow
   - **Warning:** Unexpected situations that don't prevent functionality
   - **Error:** Errors and exceptions that are handled
   - **Critical:** Catastrophic failures requiring immediate attention

3. **What to Log:**
   - **Always log:**
     - Application startup/shutdown
     - Configuration changes
     - Errors and exceptions (with full stack traces at Debug level)
     - Security-relevant events (authentication, authorization failures)
     - Performance metrics for long-running operations
     - External service calls (start, duration, result)
   - **Log at appropriate levels:**
     - Method entry/exit (Trace)
     - Key decision points (Debug/Information)
     - State changes (Information)
     - Unexpected but handled conditions (Warning)
     - All exceptions (Error/Critical)

4. **Structured Logging:**
   ```csharp
   // ✅ DO - Structured logging with named parameters
   _logger.LogInformation("Processing document {DocumentId} for project {ProjectId}", documentId, projectId);
   
   // ❌ DON'T - String interpolation loses structure
   _logger.LogInformation($"Processing document {documentId} for project {projectId}");
   ```

5. **Performance Metrics:**
   - Use `Activity` (System.Diagnostics) for tracking operation duration
   - Log operation timing at Information or Debug level
   - Track key performance indicators:
     - Model loading time
     - Embedding generation time
     - Vector search time
     - Document processing time
     - Queue wait times

6. **Configuration:**
   - All logging levels MUST be configurable via `appsettings.json`
   - Support per-namespace log level configuration
   - Default configuration should be production-appropriate (Information and above)
   - Provide `appsettings.Development.json` with Debug/Trace for development

   **Example appsettings.json:**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft": "Warning",
         "Microsoft.Hosting.Lifetime": "Information",
         "Daiv3.Knowledge": "Debug",
         "Daiv3.ModelExecution": "Information"
       }
     }
   }
   ```

7. **Log Output Format:**
   - Logs MUST be human-readable for debugging
   - Consider structured output (JSON) for production log aggregation
   - Include timestamps, log level, source, and message
   - Include correlation IDs for tracing request flows

### 8. Error Handling & Resilience

**Comprehensive error handling MUST be implemented throughout the application.**

**Error Handling Requirements:**

1. **Top-Level Functions (Public API methods, Controllers, CLI Commands):**
   - MUST have try-catch blocks
   - MUST log all exceptions with full context
   - MUST return or throw appropriate errors to callers
   - MUST NOT leak sensitive information in error messages to end users

   ```csharp
   public async Task<Result<Document>> ProcessDocumentAsync(string documentId)
   {
       try
       {
           _logger.LogInformation("Starting document processing for {DocumentId}", documentId);
           
           // Implementation
           var result = await _processor.ProcessAsync(documentId);
           
           _logger.LogInformation("Completed document processing for {DocumentId}", documentId);
           return Result.Success(result);
       }
       catch (DocumentNotFoundException ex)
       {
           _logger.LogWarning(ex, "Document not found: {DocumentId}", documentId);
           return Result.Failure<Document>($"Document {documentId} not found");
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to process document {DocumentId}", documentId);
           return Result.Failure<Document>("An error occurred processing the document");
       }
   }
   ```

2. **Key Areas Requiring Error Handling:**
   - File I/O operations
   - Database operations
   - External service calls (Foundry Local, online providers, MCP servers)
   - Model loading and inference
   - Embedding generation
   - Document parsing
   - Network operations
   - User input validation

3. **Exception Logging:**
   - Log exceptions at Error or Critical level
   - Include full exception details: message, stack trace, inner exceptions
   - Include operation context (what was being attempted)
   - Include relevant IDs and parameters
   - Use structured logging for exception properties

   ```csharp
   catch (ModelLoadException ex)
   {
       _logger.LogError(ex, 
           "Failed to load model {ModelId} for inference. Attempt {AttemptNumber}", 
           modelId, attemptNumber);
       // Handle or rethrow
   }
   ```

4. **Custom Exception Types:**
   - Create domain-specific exception types for expected error conditions
   - Inherit from appropriate base exceptions
   - Include relevant context in exception properties
   - Document when and why each exception is thrown

5. **Error Recovery:**
   - Implement retry logic for transient failures (with exponential backoff)
   - Gracefully degrade functionality when optional services are unavailable
   - Provide meaningful error messages to users
   - Log recovery attempts and outcomes

6. **Validation:**
   - Validate all inputs at boundaries (API, CLI, UI)
   - Log validation failures at Warning level with details
   - Return clear validation error messages
   - Don't proceed with invalid data

7. **Resource Cleanup:**
   - Use `using` statements for IDisposable resources
   - Implement proper cleanup in catch/finally blocks
   - Log resource disposal failures
   - Ensure database connections, file handles, etc. are always released

8. **Global Exception Handling:**
   - Implement unhandled exception handlers at application level
   - Log all unhandled exceptions at Critical level
   - Provide user-friendly error UI/messages
   - Consider crash dumps or error reports for diagnostics

**Error Handling Anti-Patterns to Avoid:**
- ❌ Empty catch blocks (swallowing exceptions)
- ❌ Catching `Exception` without logging or rethrowing
- ❌ Using exceptions for control flow
- ❌ Throwing generic exceptions (use specific types)
- ❌ Logging and rethrowing (creates duplicate logs)
- ❌ Leaking sensitive data in exception messages

---

## Requirements & Documentation Reference

All requirements, specifications, and architectural guidance are documented in the Docs/Requirements folder:

### Key Document Locations

- **Architecture Overview:** `./Docs/Requirements/Architecture/architecture-overview.md`
- **Module & Libraries Map:** `./Docs/Requirements/Architecture/module-libraries-map.md`
- **Sequence Diagrams:**
  - Data Ingestion Flow: `./Docs/Requirements/Architecture/sequence-ingestion-web.md`
  - User Request Flow: `./Docs/Requirements/Architecture/sequence-user-request.md`

### Requirement Categories

Requirements are organized by category with individual requirement documents:

- **Architecture (ARCH-\*):** `./Docs/Requirements/Reqs/ARCH-*.md`
- **Hardware & Runtime (HW-\*):** `./Docs/Requirements/Reqs/HW-*.md`
- **Key Libraries & Components (KLC-\*):** `./Docs/Requirements/Reqs/KLC-*.md`
- **Knowledge Management (KM-\*):** `./Docs/Requirements/Reqs/KM-*.md`
- **Model Execution Queue (MQ-\*):** `./Docs/Requirements/Reqs/MQ-*.md`
- **Web Frontend Connector (WFC-\*):** `./Docs/Requirements/Reqs/WFC-*.md`
- **Agent & Tool System (AST-\*):** `./Docs/Requirements/Reqs/AST-*.md`
- **Common Tools (CT-\*):** `./Docs/Requirements/Reqs/CT-*.md`
- **Execution Services (ES-\*):** `./Docs/Requirements/Reqs/ES-*.md`
- **Chat & Persistence (CP-\*):** `./Docs/Requirements/Reqs/CP-*.md`
- **Language Model Integration (LM-\*):** `./Docs/Requirements/Reqs/LM-*.md`
- **Knowledge Base Planning (KBP-\*):** `./Docs/Requirements/Reqs/KBP-*.md`

---

## Development Workflow

### Phase 1: Planning & Requirements Analysis

Before beginning ANY implementation work:

1. **Clarify Intent (98%+ Certainty Required)**
   - If requirements are ambiguous, ask clarifying questions
   - DO NOT make assumptions about functionality
   - Update requirement/specification documents to remove all ambiguity
   - Have user confirm understanding before proceeding

2. **Review Documentation**
   - Read the target requirement document(s) completely
   - Understand dependencies and predecessor/successor requirements
   - Review architecture & specification documents
   - Identify any blocking requirements that must be completed first

3. **Update Requirement Document**
   - Add "Implementation Design" section with:
     - High-level approach
     - Key design decisions
     - Data structures and algorithms
     - Interface/API changes
     - Acceptance criteria
   - Add "Task Breakdown" section with:
     - Specific, actionable development tasks
     - Estimated effort (S/M/L/XL)
     - Dependencies between tasks
   - Add "Testing Strategy" section with:
     - Unit test scenarios
     - Integration test scenarios (if applicable)
     - Edge cases to handle

4. **Update Master-Implementation-Tracker.md**
   - Find the requirement row in the tracker
   - Set Status to "In Progress"
   - Set Progress % to 0% (will be updated as work progresses)
   - Add initial notes if relevant

### Phase 2: Implementation

1. **Code Development**
   - Follow code structure in `./src/` and `./tests/`
   - Ensure code compiles without errors/warnings
   - Use existing patterns and abstractions
   - Write testable code with dependency injection and interfaces
   - **Implement comprehensive logging** using `ILogger<T>` throughout
   - **Implement proper error handling** at all API boundaries and key areas

2. **Unit Testing**
   - Write tests concurrent with implementation
   - Tests should be in `./tests/unit/` or alongside production code if xUnit pattern
   - Every feature must have corresponding test coverage
   - **All unit tests MUST pass before proceeding to next step**

3. **Integration Testing**
   - Write non-destructive integration tests for the feature
   - Test integration with database, file system, and external services
   - **All integration tests MUST pass before proceeding to next step**

4. **CLI Implementation & Real-World Testing**
   - **Implement feature in `Daiv3.App.Cli` BEFORE implementing in MAUI**
   - Create CLI command(s) that exercise the library functionality
   - Test with realistic data and scenarios
   - Identify and fix bugs discovered during CLI usage
   - Verify logging is helpful and appropriate
   - Verify error messages are clear and actionable
   - Test edge cases and error conditions
   - Document lessons learned for MAUI implementation

5. **MAUI Implementation (Only After CLI Validation)**
   - Design UI/UX for the feature in MAUI
   - Leverage lessons learned from CLI implementation
   - Implement MAUI application feature
   - Apply same logging and error handling patterns

6. **Incremental Documentation Updates**
   - As tasks complete, update the requirement document:
     - Mark tasks complete in "Task Breakdown"
     - Update Progress % based on remaining tasks
     - Add technical notes, decisions, blockers
     - Document CLI testing results and lessons learned
   - After each significant milestone:
     - Update `./Docs/Requirements/Master-Implementation-Tracker.md`
     - Update Status column (In Progress)
     - Update Progress % column
     - Add or update Notes column with blockers/decisions

### Phase 3: Completion

1. **Final Verification**
   - All code compiles without errors/warnings
   - All unit tests pass
   - Integration tests pass (if applicable)
   - Code review completed

2. **Final Documentation Update**
   - Update requirement document with:
     - "Completion Date" and final status
     - Summary of implementation
     - Any post-implementation notes
   - Update Master-Implementation-Tracker.md:
     - Set Status to "Complete"
     - Set Progress % to 100%
     - Final notes

---

## Critical Guardrails

### DO Requirements

- ✅ **DO** ask clarifying questions if requirements are unclear
- ✅ **DO** ensure all code compiles without errors/warnings
- ✅ **DO** write testable code (loose coupling, dependency injection)
- ✅ **DO** run and verify all unit tests pass
- ✅ **DO** update requirement documents with detailed designs before coding
- ✅ **DO** update requirement documents incrementally as work progresses
- ✅ **DO** update Master-Implementation-Tracker.md as work progresses
- ✅ **DO** reference requirement/specification/architecture documents in code comments when relevant
- ✅ **DO** check for blocking predecessors before starting work
- ✅ **DO** pass these instructions to any spawned sub-agents or contextualized tasks
- ✅ **DO** prefer custom implementations over external dependencies
- ✅ **DO** check `approved-dependencies.md` before adding or upgrading ANY dependency
- ✅ **DO** create architecture decision documents for any external library consideration
- ✅ **DO** wait for user approval before adding external dependencies or upgrading versions
- ✅ **DO** update `approved-dependencies.md` after any dependency decision is made
- ✅ **DO** implement comprehensive logging using `ILogger<T>` in all libraries
- ✅ **DO** implement proper error handling at all API boundaries and key areas
- ✅ **DO** test features in CLI before implementing in MAUI
- ✅ **DO** use structured logging with named parameters
- ✅ **DO** make logging levels configurable via appsettings.json

### DON'T Requirements

- ❌ **DON'T** start coding without reviewing requirements and design documents
- ❌ **DON'T** make assumptions about functionality - ask clarifying questions instead
- ❌ **DON'T** consider a feature complete until unit tests pass
- ❌ **DON'T** skip documentation updates before, during, or after implementation
- ❌ **DON'T** ignore compilation errors or warnings
- ❌ **DON'T** write code that is difficult to test (tight coupling, static dependencies)
- ❌ **DON'T** commit or mark complete without updating tracking documents
- ❌ **DON'T** assume requirements from other developers' intents - update specs for clarity
- ❌ **DON'T** add external NuGet packages without checking `approved-dependencies.md` first
- ❌ **DON'T** add external NuGet packages without creating an architecture decision document
- ❌ **DON'T** use external libraries unless they are .NET framework, Azure, Microsoft, or explicitly approved
- ❌ **DON'T** upgrade dependency versions without checking `approved-dependencies.md` and getting approval
- ❌ **DON'T** use Console.WriteLine or similar for operational logging
- ❌ **DON'T** swallow exceptions without logging
- ❌ **DON'T** implement MAUI features before validating in CLI
- ❌ **DON'T** use string interpolation for log messages (use structured logging)
- ❌ **DON'T** skip error handling at API boundaries and key areas

---

## Sub-Agent & Contextualized Task Instructions

**CRITICAL:** These instructions MUST be passed to any spawned sub-agents, background tasks, or contextualized task contexts.

When creating or launching any autonomous agent, background task, or separate context:

1. **Include This File in Context**
   - Provide the full path to this `AI-Instructions.md` file (`./Docs/AI-Instructions.md`)
   - Ensure the agent reads and understands all guidance

2. **Explicit Requirement Handoff**
   - Clearly identify which requirement(s) the task is working on
   - Provide direct links to the requirement document(s)
   - Include links to relevant architecture/specification documents
   - Specify acceptance criteria and success conditions

3. **Documentation Update Responsibility**
   - Agent is responsible for updating requirement documents
   - Agent must update Master-Implementation-Tracker.md
   - Agent must follow the incremental update pattern

4. **Quality Gates**
   - Agent must verify code compiles
   - Agent must run and pass unit tests
   - Agent must run and pass integration tests
   - Agent must implement CLI command for real-world testing (before MAUI)
   - Agent must implement comprehensive logging with ILogger<T>
   - Agent must implement proper error handling
   - Agent must not mark work complete unless all gates pass

Example context handoff:
```
Working on requirement: ARCH-REQ-001
Requirement Document: ./Docs/Requirements/Reqs/ARCH-REQ-001.md
Specification Document: ./Docs/Requirements/Specs/03-System-Architecture.md
Architecture Reference: ./Docs/Requirements/Architecture/architecture-overview.md
Master Tracker: ./Docs/Requirements/Master-Implementation-Tracker.md
Dependency Registry: ./Docs/Requirements/Architecture/approved-dependencies.md
AI Instructions: ./Docs/AI-Instructions.md

You MUST:
1. Review all referenced documents
2. Update the requirement document with design details before coding
3. Update Master-Implementation-Tracker.md as work progresses
4. Check approved-dependencies.md before adding or upgrading dependencies
5. Implement comprehensive logging using ILogger<T> with structured logging
6. Implement proper error handling at all API boundaries
7. Ensure code compiles without errors/warnings
8. Ensure all unit tests pass
9. Ensure all integration tests pass
10. Implement and test in CLI before implementing in MAUI
11. Ask clarifying questions if 98%+ certainty is not achieved
```

---

## Workspace Structure

```
c:\_prj\stevecisco\private\daiv3/
├── .github/
│   └── copilot-instructions.md              ← GitHub Copilot instructions (all IDEs)
├── .vscode/
│   └── copilot-instructions.md              ← VS Code-specific instructions
├── Docs/
│   ├── AI-Instructions.md                   ← THIS FILE (Shared instructions)
│   └── Requirements/
│       ├── Master-Implementation-Tracker.md ← PRIMARY TRACKING DOCUMENT
│       ├── Implementation-Plan.md           ← PHASE PLANNING
│       ├── Reqs/                            ← INDIVIDUAL REQUIREMENT DOCS
│       ├── Specs/                           ← SPECIFICATION DOCUMENTS
│       └── Architecture/
│           ├── architecture-overview.md     ← ARCHITECTURE DIAGRAMS
│           ├── module-libraries-map.md
│           ├── sequence-ingestion-web.md
│           ├── sequence-user-request.md
│           ├── approved-dependencies.md     ← DEPENDENCY APPROVAL REGISTRY
│           └── decisions/                   ← ARCHITECTURE DECISION DOCUMENTS
│               └── ADD-YYYYMMDD-*.md        ← External dependency decisions
├── src/                                     ← PRODUCTION CODE
│   ├── Daiv3.*/                             ← All Daiv3 projects
│   ├── FoundryLocal.Management/
│   └── FoundryLocal.Management.Cli/
├── tests/                                   ← TEST CODE
│   ├── unit/
│   └── integration/
└── FoundryLocal.IntegrationTests/           ← INTEGRATION TESTS
```

---

## Compilation & Testing Commands

**CRITICAL: Always run dotnet commands from the workspace root directory with full relative paths to project/solution files.**

### Build Commands
```bash
# Build entire solution (from root directory)
dotnet build Daiv3.FoundryLocal.slnx

# Build with no warnings tolerance (from root directory)
dotnet build Daiv3.FoundryLocal.slnx /p:TreatWarningsAsErrors=true

# Build specific project (from root directory with full path)
dotnet build src/Daiv3.FoundryLocal.Management/Daiv3.FoundryLocal.Management.csproj
dotnet build src/Daiv3.FoundryLocal.Management.Cli/Daiv3.FoundryLocal.Management.Cli.csproj
```

### Testing Commands
```bash
# Run all tests (from root directory)
dotnet test Daiv3.FoundryLocal.slnx

# Run specific test project (from root directory with full path)
dotnet test tests/integration/Daiv3.FoundryLocal.IntegrationTests/Daiv3.FoundryLocal.IntegrationTests.csproj

# Run with verbose output (from root directory)
dotnet test Daiv3.FoundryLocal.slnx --verbosity detailed

# Run with code coverage (from root directory)
dotnet test Daiv3.FoundryLocal.slnx /p:CollectCoverage=true
```

**Why Full Paths:**
- Eliminates need to change directories
- Prevents errors from running commands in wrong directory
- Makes commands more explicit and self-documenting
- Works consistently regardless of current working directory (as long as you start from root)

---

## Referenced Technologies & Standards

- **Target Runtime:** Windows 11 Copilot+, .NET 10
- **Hardware:** NPU (primary), GPU (fallback), CPU (final fallback)
- **Model Execution:** Microsoft.ML.OnnxRuntime.DirectML
- **Data Persistence:** SQLite (Microsoft.Data.Sqlite)
- **Vector Database:** SQLite with vector extensions
- **AI Integration:** Microsoft.Extensions.AI, Foundry Local SDK
- **Logging:** Microsoft.Extensions.Logging.Abstractions with structured logging
- **Code Quality:** C# best practices, SOLID principles, DI/IoC patterns
- **Testing Strategy:** Unit → Integration → CLI → MAUI
- **Target Frameworks:** Multi-targeting with conditional Windows optimizations (net10.0 / net10.0-windows10.0.26100)

---

## Contact & Questions

If any requirement is genuinely ambiguous or contradictory:
1. Ask clarifying questions explicitly
2. Document the clarification in the requirement document
3. Update the specification if needed
4. Proceed only when 98%+ certain of intent

---

**Last Updated:** February 22, 2026
**Version:** 1.5
**Status:** Active - Shared instructions referenced by all AI tools

**Changelog:**
- **v1.5 (Feb 22, 2026):** Extracted from .vscode/copilot-instructions.md as shared instructions for all AI tools
- **v1.4 (Feb 22, 2026):** Added Target Framework & Platform Configuration guidance; multi-targeting pattern for Windows-specific optimizations (NPU/DirectML)
- **v1.3 (Feb 22, 2026):** Added CLI-first testing strategy; comprehensive logging & observability requirements; error handling & resilience guidelines
- **v1.2 (Feb 22, 2026):** Added approved-dependencies.md registry requirement; version upgrade approval process
- **v1.1 (Feb 22, 2026):** Added dependency & library management philosophy, architecture decision document requirements
- **v1.0 (Feb 21, 2026):** Initial release
