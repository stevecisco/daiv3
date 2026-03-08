# Daiv3 Solution Structure

## Overview

All projects are prefixed with `Daiv3` to clearly identify components belonging to the Daiv3 product ecosystem. This includes both product-specific modules and technology adapters that, while potentially reusable, are maintained within the Daiv3 solution boundary.

## Dependency Philosophy

**Minimize External Dependencies:**
- Prefer custom implementations to reduce attack surface and enable rapid bug fixes
- External packages require architecture decision document (ADD) approval
- Default to .NET framework, Azure, and Microsoft-supported libraries only
- Complex features should be isolated in dedicated libraries with unit tests

**Pre-Approved Dependencies:**
- .NET BCL and runtime libraries
- Microsoft.Extensions.* packages
- Microsoft.ML.* packages (ONNX Runtime, Tokenizers, DirectML)
- Azure SDK packages (Azure.*, Microsoft.Azure.*)
- System.* packages
- Microsoft.Data.Sqlite
- Foundry Local SDK
- Microsoft.Extensions.AI

**All other external dependencies require:**
1. Architecture Decision Document in `Docs/Requirements/Architecture/decisions/`
2. Analysis of custom implementation vs. external library trade-offs
3. Security, licensing, maintenance, and pricing review
4. Explicit approval before implementation

See [Copilot Instructions - Dependency Management](../../.vscode/copilot-instructions.md#3-dependency--library-management-philosophy) for complete guidelines.

## Solution Layout

**/src**

### Apps
- **Daiv3.App.Maui** - .NET MAUI cross-platform UI application
  - Primary user interface for chat, dashboard, project management, and settings
  - Full filesystem access for document indexing and project management
  - Native Windows integration via MAUI platform-specific APIs
  - Target platforms: Windows (primary), with potential for macOS/Linux

- **Daiv3.App.Cli** - Command-line interface application
  - CLI alternative for headless operation, scripting, and automation
  - Supports all core operations: chat, indexing, project management, configuration
  - Ideal for server deployments, CI/CD integration, and power users
  - Interactive and non-interactive modes

### Core Product Libraries

- **Daiv3.Core** - Domain models, contracts, and shared abstractions
  - Request/response models
  - Domain entities (Project, Task, Agent, Skill, etc.)
  - Shared interfaces and enums
  - No external dependencies

- **Daiv3.Orchestration** - Task orchestration layer
  - Task orchestrator
  - Intent resolver
  - Agent manager
  - Skill registry
  - Coordination logic between components

- **Daiv3.Knowledge** - Knowledge management orchestration
  - Knowledge layer coordination
  - Tier 1/2 index management
  - Learning memory
  - Document ingestion pipeline coordination

- **Daiv3.Persistence** - Data persistence layer
  - SQLite repositories for projects, app state, configuration
  - Schema management and migrations
  - Unit of work and repository patterns

- **Daiv3.Scheduler** - Background job scheduling
  - Custom scheduler implementation using BackgroundService
  - Cron expression support
  - Recurring indexing jobs
  - Task execution scheduling
  - Event-based triggers
  - SQLite-persisted job state

- **Daiv3.Worker** - Background processing host
  - Console/worker service for background operations
  - Document ingestion queue processing
  - Scheduled job execution
  - Can run independently from UI apps

- **Daiv3.Api** - Local HTTP API (optional)
  - ASP.NET Core API for local integrations
  - RESTful endpoints for third-party tool integration
  - WebSocket support for real-time updates
  - Optional component for advanced scenarios

### Model Execution

- **Daiv3.ModelExecution** - Model execution coordination
  - Model queue management
  - Request routing and prioritization
  - Token budget tracking
  - Execution strategy selection

- **Daiv3.FoundryLocal.Management** - Foundry Local management service
  - Service catalog client
  - Model lifecycle management
  - Configuration and options
  - *Note: Named with `FoundryLocal` prefix as it's specific to managing Foundry Local runtime and could be reused by other systems that depend on Foundry Local*

- **Daiv3.FoundryLocal.Management.Cli** - Foundry Local CLI
  - Command-line operations for Foundry Local management
  - Model installation, updates, and removal
  - Service control operations
  - *Note: Named with `FoundryLocal` prefix for same reason as Management library*

- **Daiv3.FoundryLocal.Bridge** - Foundry Local execution adapter
  - Foundry Local SDK integration
  - Chat completion interface
  - Model loading and lifecycle
  - Response streaming

### Online Providers

- **Daiv3.OnlineProviders.Abstractions** - Common provider interfaces
  - Provider routing logic
  - Token budget guard
  - Provider selection strategy
  - Request/response contracts

- **Daiv3.OnlineProviders.OpenAI** - OpenAI provider client
  - OpenAI API integration
  - Model mapping and configuration

- **Daiv3.OnlineProviders.AzureOpenAI** - Azure OpenAI provider client
  - Azure OpenAI-specific authentication
  - Endpoint configuration
  - Deployment management

- **Daiv3.OnlineProviders.Anthropic** - Anthropic provider client
  - Claude API integration
  - Model mapping and configuration

### Knowledge Processing

- **Daiv3.Knowledge.DocProc** - Document processing pipeline
  - Format detection (PDF, DOCX, HTML, MD, TXT)
  - Content extraction
  - Text chunking strategy
  - Summarization coordination

- **Daiv3.Knowledge.Embedding** - Embedding and vector storage
  - ONNX Runtime DirectML integration
  - Embedding generation (NPU/GPU/CPU)
  - Vector similarity search
  - SQLite vector persistence
  - Tier 1 (topic) and Tier 2 (chunk) indexes

### External Integrations

- **Daiv3.Mcp.Integration** - MCP tool server integration
  - ModelContextProtocol .NET SDK wrapper
  - Tool server discovery and connection
  - Request/response handling

- **Daiv3.WebFetch.Crawl** - Web content fetching
  - HTTP client for web content retrieval
  - HTML to Markdown conversion
  - Link crawling and depth management
  - Content cleaning and normalization

### Infrastructure

- **Daiv3.Infrastructure.Shared** - Cross-cutting concerns
  - HTTP client factory and policies
  - File system abstractions
  - Logging and telemetry
  - Configuration helpers
  - Resilience policies

**/tests**

- **Daiv3.UnitTests** - Unit tests
  - Core domain logic
  - Orchestration
  - Persistence
  - Knowledge processing

- **Daiv3.IntegrationTests** - Integration tests (existing)
  - End-to-end scenarios
  - External service integration
  - Database operations

- **Daiv3.Maui.UITests** - UI automation tests (optional)
  - MAUI UI testing
  - User workflow validation

## Target Frameworks

All projects use **multi-targeting** pattern for Windows-specific optimizations while maintaining cross-platform compatibility.

### Pattern (Default + Windows-Specific)

Projects use conditional TFM switching based on build platform:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
</PropertyGroup>

<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

**Result:**
- When built on Windows: `net10.0-windows10.0.26100` (with NPU/DirectML support)
- When built on Linux/macOS: `net10.0` (cross-platform)

### Target Framework Summary

| Project Type | Base TFM | Windows TFM | Notes |
|--------------|----------|-------------|-------|
| **MAUI Apps** | `net10.0` | `net10.0-windows10.0.26100` | MAUI will also add platform-specific TFMs |
| **CLI Apps** | `net10.0` | `net10.0-windows10.0.26100` | Multi-platform support |
| **Worker Services** | `net10.0` | `net10.0-windows10.0.26100` | Background processing |
| **Libraries with Hardware Features** | `net10.0` | `net10.0-windows10.0.26100` | DirectML, NPU, Windows APIs |
| **Pure Business Logic Libraries** | `net10.0` | `net10.0-windows10.0.26100` | Use pattern for consistency |
| **Test Projects** | `net10.0` | - | Tests use cross-platform only |

**Hardware-Specific Features:**
- NPU/DirectML features work at **library level** (no propagation to executable needed)
- Conditional package references select platform-appropriate packages
- Runtime behavior adapts based on executable's TFM

**See:** `FoundryLocal.Management` project for reference implementation

## Key Dependencies by Layer

### Daiv3.App.Maui
- → Daiv3.Orchestration
- → Daiv3.Persistence
- → Daiv3.Core

### Daiv3.App.Cli
- → Daiv3.Orchestration
- → Daiv3.Persistence
- → Daiv3.Core
- → **✅ System.CommandLine** (Microsoft library, verify approval)

### Daiv3.Orchestration
- → Daiv3.Core
- → Daiv3.ModelExecution
- → Daiv3.Knowledge
- → Daiv3.Persistence
- → Daiv3.Scheduler
- → Daiv3.WebFetch.Crawl
- → Daiv3.Mcp.Integration

### Daiv3.ModelExecution
- → Daiv3.Core
- → Daiv3.FoundryLocal.Bridge
- → Daiv3.OnlineProviders.Abstractions
- → Microsoft.Extensions.AI

### Daiv3.FoundryLocal.Bridge
- → Foundry Local SDK (Windows TFM - with WinML support)
- → Foundry Local SDK (Baseline via conditional package reference for cross-platform)
- → Microsoft.Extensions.AI

**Note:** Uses conditional package references based on TFM:
- `net10.0-windows*`: Microsoft.AI.Foundry.Local.WinML (NPU support)
- `net10.0`: Microsoft.AI.Foundry.Local (cross-platform baseline)

### Daiv3.OnlineProviders.* (all)
- → Daiv3.OnlineProviders.Abstractions
- → Microsoft.Extensions.AI
- → **⚠️ Provider SDKs - ADD required** (Recommend custom HTTP client implementations)

### Daiv3.Knowledge
- → Daiv3.Core
- → Daiv3.Knowledge.DocProc
- → Daiv3.Knowledge.Embedding
- → Daiv3.Persistence

### Daiv3.Knowledge.DocProc
- → Daiv3.Core
- → **⚠️ PDF extraction - ADD required** (PdfPig, custom, or alternative)
- → **✅ DocumentFormat.OpenXml** (Microsoft-owned, verify approval)
- → **⚠️ HTML parsing - ADD required** (Consider custom implementation)

### Daiv3.Knowledge.Embedding
- → Microsoft.ML.OnnxRuntime.DirectML (Windows TFM - NPU/GPU acceleration)
- → Microsoft.ML.OnnxRuntime (Cross-platform fallback via conditional package reference)
- → Microsoft.ML.Tokenizers
- → System.Numerics.TensorPrimitives
- → Microsoft.Data.Sqlite

**Note:** Uses conditional package references based on TFM:
- `net10.0-windows*`: DirectML provider for NPU/GPU
- `net10.0`: CPU-only provider for cross-platform

### Daiv3.Persistence
- → Microsoft.Data.Sqlite
- → Daiv3.Core

### Daiv3.Scheduler
- → Daiv3.Core
- → Daiv3.Persistence
- → Microsoft.Extensions.Hosting (BackgroundService)

### Daiv3.Worker
- → Daiv3.Orchestration
- → Daiv3.Scheduler
- → Microsoft.Extensions.Hosting

### Daiv3.Mcp.Integration
- → **⚠️ MCP implementation - ADD required** (Custom protocol vs. MCP SDK)
- → Daiv3.Infrastructure.Shared

### Daiv3.WebFetch.Crawl
- → **⚠️ HTML-to-Markdown - ADD required** (Custom implementation recommended)
- → Daiv3.Infrastructure.Shared
- → System.Net.Http

## Naming Rationale

### Daiv3.FoundryLocal.* Exception

The `Daiv3.FoundryLocal.Management` and `Daiv3.FoundryLocal.Management.Cli` projects maintain the "FoundryLocal" sub-namespace because:
1. They are specifically concerned with managing the Foundry Local runtime
2. They could be reused by other systems that depend on Foundry Local
3. The naming clearly indicates their purpose and scope
4. They are modular and technology-specific rather than product-workflow-specific

All other reusable, technology-specific modules (MCP integration, web fetching, knowledge processing) still carry the Daiv3 prefix because they are maintained within the Daiv3 solution boundary and their evolution is tied to Daiv3 product requirements.

## External Dependency Status & Required Decisions

Based on the dependency philosophy, the following external dependencies require Architecture Decision Documents (ADDs) before implementation:

### ⚠️ Pending ADD Approval

| Component | Feature | Options | Recommendation | Priority |
|-----------|---------|---------|----------------|----------|
| **Daiv3.Knowledge.DocProc** | PDF Extraction | PdfPig, Custom, iText, etc. | ADD Required | High |
| **Daiv3.Knowledge.DocProc** | HTML Parsing | Custom, AngleSharp, HtmlAgilityPack | **Custom Implementation** | High |
| **Daiv3.Mcp.Integration** | MCP Protocol | Custom, MCP SDK | ADD Required | Medium |
| **Daiv3.WebFetch.Crawl** | HTML-to-Markdown | Custom, Markdig, ReverseMarkdown | **Custom Implementation** | High |
| **Daiv3.OnlineProviders.\*** | Provider APIs | Custom HTTP, Official SDKs | **Custom HTTP Clients** | High |

### ✅ Custom Implementations Chosen

| Component | Feature | Rationale |
|-----------|---------|-----------|
| **Daiv3.Scheduler** | Job Scheduling | Core feature, manageable complexity, full control, uses BackgroundService + SQLite |
| **Daiv3.Knowledge.Embedding** | Vector Search | In-house cosine similarity using TensorPrimitives, no external vector DB |

### 🔍 Pending Verification (Likely Pre-Approved)

| Library | Project | Status |
|---------|---------|--------|
| System.CommandLine | Daiv3.App.Cli | Microsoft library - verify official status |
| DocumentFormat.OpenXml | Daiv3.Knowledge.DocProc | Microsoft library - verify official status |

### Decision Timeline

**Before Phase 1 Implementation:**
1. Verify System.CommandLine and DocumentFormat.OpenXml are officially supported
2. Create ADD for PDF extraction library decision
3. Create ADD for MCP integration approach

**Custom implementations can begin immediately:**
- Daiv3.Scheduler (custom job scheduling)
- HTML-to-Markdown converter (Daiv3.WebFetch.Crawl)
- Online provider HTTP clients (Daiv3.OnlineProviders.\*)

## Build and Deployment

### Solution Files
- `Daiv3.sln` - Main solution file including all projects
- `Daiv3.IntegrationTests.slnx` - Existing integration test solution (can be merged or kept separate)

### Output Structure
```
/publish
  /Daiv3.App.Maui
    - win-x64
    - win-arm64
  /Daiv3.App.Cli
    - win-x64
    - win-arm64
    - linux-x64
    - osx-arm64
  /Daiv3.Worker
    - win-x64
    - win-arm64
```

## Future Considerations

### Library Evolution
- Skill marketplace packages may be distributed as separate NuGet packages with `Daiv3.Skills.*` prefix
- Plugin/extension system may introduce `Daiv3.Extensions.*` namespace
- If any library becomes broadly useful outside Daiv3, it can be extracted and renamed at that time

### Custom Libraries as Reusable Components
As custom implementations mature, they may be valuable to the broader .NET community:
- **Daiv3.Scheduler** → Could be extracted as standalone scheduling library
- **HTML-to-Markdown converter** → Could be published as independent utility
- **In-process vector search** → Could become a lightweight SQLite vector extension library

These extractions would only occur after:
1. Proven stability and performance in Daiv3
2. Clear demand from external users
3. Commitment to independent maintenance
4. Proper documentation and samples

### Dependency Re-evaluation
As the .NET ecosystem evolves, periodically review:
- New Microsoft-supported libraries that could replace custom implementations
- Security improvements in existing dependencies
- Performance benchmarks comparing custom vs. external libraries
- Maintenance burden of custom implementations vs. mature ecosystem libraries
