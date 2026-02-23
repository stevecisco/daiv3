# Component Responsibilities & Usage

**Purpose:** This document defines the responsibility and usage of each key library component in the DAIv3 architecture, as required by KLC-ACC-002.

**Last Updated:** February 23, 2026  
**Status:** Active

---

## Overview

This document maps each library specified in [12. Key .NET Libraries & Components](../Specs/12-Key-Libraries-Components.md) to:
- Its **primary responsibility** in the system
- The **architecture layer(s)** it serves
- **Usage patterns** and integration points
- **Implementation status** and project location

---

## Component Registry

### 1. Microsoft.ML.OnnxRuntime.DirectML

**KLC Requirement:** KLC-REQ-001  
**Status:** ✅ Implemented  
**Version:** 9.0.0+

#### Responsibility
Provides in-process inference and embedding generation using ONNX Runtime with DirectML hardware acceleration (NPU/GPU/CPU).

#### Architecture Layer
- **Primary:** Model Execution Layer
- **Secondary:** Knowledge Layer (for embedding generation)

#### Usage
- **Session Creation:** `OnnxSessionOptionsFactory` creates configured `SessionOptions` with appropriate execution provider (DirectML for NPU/GPU, CPU for fallback)
- **Hardware Selection:** Automatic provider selection based on `IHardwareDetectionProvider` capabilities
- **Inference:** Used by embedding services to load ONNX models and execute inference
- **Fallback Chain:** DirectML (NPU/GPU preferred) → CPU execution provider

#### Integration Points
- `Daiv3.Infrastructure.Shared` - Session options factory and DI registration
- `Daiv3.Knowledge.Embedding` - Embedding model execution
- `Daiv3.ModelExecution` - Local model inference

#### Configuration
```csharp
// Automatic provider selection
var options = OnnxSessionOptionsFactory.CreateWithAutoProvider(hardwareDetection);

// Manual override
var options = OnnxSessionOptionsFactory.CreateWithProvider(ExecutionProvider.Npu);
```

#### Tests
- `OnnxSessionOptionsFactoryTests` - 12 unit tests covering provider selection
- Hardware-specific provider configuration validated

---

### 2. Microsoft.ML.Tokenizers

**KLC Requirement:** KLC-REQ-002  
**Status:** ✅ Implemented  
**Version:** Latest

#### Responsibility
Provides tokenization capabilities for text processing, chunking, and token counting.

#### Architecture Layer
- **Primary:** Knowledge Layer
- **Secondary:** Orchestration Layer (for context management)

#### Usage
- **Document Chunking:** `TokenBasedChunker` splits documents into ~400 token segments with ~50 token overlap
- **Token Counting:** Accurate token counting for context window management
- **Model Compatibility:** Supports tokenizers for various embedding and LLM models

#### Integration Points
- `Daiv3.Knowledge.DocProc` - Document chunking service
- `Daiv3.Orchestration` - Context window management
- `Daiv3.ModelExecution` - Token budget tracking for online providers

#### Configuration
```csharp
// Token-based chunking
var chunker = new TokenBasedChunker(tokenizer, maxTokensPerChunk: 400, overlapTokens: 50);
var chunks = await chunker.ChunkAsync(document);
```

#### Tests
- `TokenBasedChunkerTests` - Chunking strategy validation
- Token counting accuracy verified

---

### 3. System.Numerics.TensorPrimitives

**KLC Requirement:** KLC-REQ-003  
**Status:** ✅ Implemented  
**Version:** .NET 10 Framework

#### Responsibility
Provides CPU-optimized vector math operations (SIMD-accelerated) for similarity calculations and batch vector operations.

#### Architecture Layer
- **Primary:** Knowledge Layer
- **Secondary:** Model Execution Layer (for vector operations)

#### Usage
- **Cosine Similarity:** `CpuVectorSimilarityService` uses `TensorPrimitives.CosineSimilarity()` for efficient vector comparison
- **Batch Operations:** Vectorized operations for processing multiple embeddings in parallel
- **CPU Fallback:** Primary implementation when NPU/GPU are unavailable
- **Performance:** SIMD instructions provide 4-8x speedup over naive implementations

#### Integration Points
- `Daiv3.Knowledge` - Vector similarity search in two-tier index
- `Daiv3.Infrastructure.Shared.Hardware` - CPU vector operations service

#### Configuration
```csharp
// CPU vector similarity service (hardware-fallback)
services.AddSingleton<IVectorSimilarityService, CpuVectorSimilarityService>();
```

#### Tests
- `CpuVectorSimilarityServiceTests` - 48 unit tests covering similarity calculations
- Performance benchmarks validate SIMD acceleration
- Metrics collection tests (12 tests) for telemetry

---

### 4. Microsoft.Data.Sqlite

**KLC Requirement:** KLC-REQ-004  
**Status:** ✅ Implemented  
**Version:** 9.0.0+

#### Responsibility
Provides embedded SQLite database for persistence of all application data (documents, embeddings, projects, tasks, sessions, model queue).

#### Architecture Layer
- **Primary:** Persistence Layer (exclusive)

#### Usage
- **Database Context:** `DatabaseContext` manages connections and migrations
- **Repository Pattern:** `IRepository<T>` provides CRUD operations for all entities
- **Schema:** 8 tables (topic_index, chunk_index, documents, projects, tasks, sessions, model_queue, learning_memory)
- **Migrations:** Automatic schema creation and updates via `DatabaseMigrations`
- **Connection Pooling:** Configured for concurrent access

#### Integration Points
- `Daiv3.Persistence` - All repository implementations
- `Daiv3.Knowledge` - Topic and chunk index storage
- `Daiv3.Orchestration` - Project and task storage
- `Daiv3.ModelExecution` - Model queue persistence

#### Configuration
```csharp
// Database connection
services.AddSingleton<IDatabaseFactory, DatabaseFactory>();
services.Configure<PersistenceOptions>(options =>
{
    options.DatabasePath = "path/to/database.db";
    options.EnableWal = true; // Write-Ahead Logging for concurrency
});
```

#### Tests
- 42 unit tests for repository operations
- 22 integration tests with real SQLite database
- Migration tests validate schema updates
- CLI commands (`db init`, `db migrate`, `db status`) verified

---

### 5. Microsoft.Extensions.AI

**KLC Requirement:** KLC-REQ-005, KLC-REQ-006  
**Status:** ✅ Pre-Approved (Awaiting Foundry Local SDK integration)  
**Version:** Latest

#### Responsibility
Provides unified abstractions for AI services, enabling seamless integration of Foundry Local and online providers (OpenAI, Azure OpenAI, Anthropic).

#### Architecture Layer
- **Primary:** Model Execution Layer
- **Secondary:** Orchestration Layer (for provider routing)

#### Usage
- **Foundry Local Integration:** `IFoundryBridge` wraps Foundry Local SDK via `IChatClient` from Microsoft.Extensions.AI
- **Online Provider Routing:** `IOnlineProviderRouter` manages multiple online providers through common abstractions
- **Provider Abstraction:** Allows swapping between local and online execution without UI changes
- **Token Budgets:** Track and manage token usage across providers

#### Integration Points
- `Daiv3.FoundryLocal.Bridge` - Foundry Local adapter
- `Daiv3.OnlineProviders.*` - OpenAI, Azure OpenAI, Anthropic clients
- `Daiv3.ModelExecution` - Model queue and provider routing
- `Daiv3.Orchestration` - High-level task execution

#### Configuration
```csharp
// Foundry Local registration
services.AddFoundryLocalBridge(options =>
{
    options.ServiceCatalogPath = "path/to/catalog";
    options.EnableHardwareDetection = true;
});

// Online provider registration
services.AddOnlineProviderRouter(options =>
{
    options.DefaultProvider = "azure-openai";
    options.FallbackChain = new[] { "openai", "anthropic" };
});
```

#### Tests
- `FoundryBridgeTests` - 12 unit tests for local execution
- `OnlineProviderRouterTests` - 16 unit tests for provider routing
- Token budget tracking validated

---

### 6. Foundry Local SDK

**KLC Requirement:** KLC-REQ-005  
**Status:** ✅ Pre-Approved (Integration pending)  
**Version:** Latest

#### Responsibility
Provides runtime interface to Foundry Local for executing local SLM/LLM models on Windows 11 Copilot+ devices.

#### Architecture Layer
- **Primary:** Model Execution Layer
- **External Dependency:** Foundry Local Runtime (separate service)

#### Usage
- **Service Discovery:** `ServiceCatalogClient` reads Foundry Local service catalog JSON
- **Model Enumeration:** Lists available models and their capabilities
- **Chat Completion:** Execute chat requests against local models
- **Hardware Acceleration:** Leverages NPU/GPU through Foundry Local runtime
- **Managed Bridge:** `FoundryLocalManagementService` provides high-level API

#### Integration Points
- `Daiv3.FoundryLocal.Bridge` - Primary integration point
- `Daiv3.FoundryLocal.Management` - Management service and CLI
- `Daiv3.FoundryLocal.Management.Cli` - CLI tool for Foundry Local operations

#### Configuration
```csharp
// Foundry Local management
services.AddFoundryLocalManagement(options =>
{
    options.ModelCachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    options.ServiceCatalogCheckInterval = TimeSpan.FromMinutes(5);
});
```

#### Tests
- `ServiceCatalogClientTests` - Catalog parsing and model discovery
- CLI commands (`foundry status`, `foundry models`) validated
- Integration tests with Foundry Local runtime (when available)

---

### 7. HTML Parser (AngleSharp or HtmlAgilityPack)

**KLC Requirement:** KLC-REQ-007  
**Status:** ⚠️ Pending Decision (ADD Required)  
**Version:** TBD

#### Responsibility
Extracts text content from HTML documents and converts to Markdown for indexing.

#### Architecture Layer
- **Primary:** Knowledge Layer (Document Processor)

#### Usage
- **HTML Extraction:** Parse HTML documents and extract text content
- **Markdown Conversion:** Convert HTML semantic structure to Markdown
- **Web Crawling:** Process HTML from fetched web pages
- **Sanitization:** Remove scripts, styles, and irrelevant content

#### Integration Points
- `Daiv3.Knowledge.DocProc` - HTML document processor
- `Daiv3.WebFetch.Crawl` - Web content extraction

#### Configuration
*Pending decision between AngleSharp and HtmlAgilityPack*

#### Tests
*To be defined after library selection*

---

### 8. Model Context Protocol .NET SDK

**KLC Requirement:** KLC-REQ-008  
**Status:** ⚠️ Pending ADD Approval  
**Version:** TBD

#### Responsibility
Enables integration with Model Context Protocol (MCP) tool servers for extended capabilities.

#### Architecture Layer
- **Primary:** Orchestration Layer (Skills)
- **Secondary:** External Services Integration

#### Usage
- **Tool Discovery:** Enumerate available MCP tool servers and their capabilities
- **Tool Invocation:** Execute MCP tool requests from orchestration layer
- **Skill Registry:** Register MCP tools as skills in the skill registry
- **Protocol Communication:** Handle MCP protocol messages

#### Integration Points
- `Daiv3.Mcp.Integration` - MCP client implementation
- `Daiv3.Orchestration` - Skill registry and agent manager

#### Configuration
*Pending ADD approval and implementation*

#### Tests
*To be defined after implementation*

---

### 9. DocumentFormat.OpenXml

**KLC Requirement:** KLC-REQ-009  
**Status:** ✅ Pre-Approved  
**Version:** Latest

#### Responsibility
Extracts text content from Microsoft Office DOCX files for document indexing.

#### Architecture Layer
- **Primary:** Knowledge Layer (Document Processor)

#### Usage
- **DOCX Parsing:** Extract text from Word documents
- **Structure Preservation:** Maintain paragraph and heading structure
- **Metadata Extraction:** Extract document properties (title, author, etc.)
- **Format Support:** Handle modern Office Open XML formats

#### Integration Points
- `Daiv3.Knowledge.DocProc` - DOCX document processor

#### Configuration
```csharp
// DOCX processor registration
services.AddDocumentProcessor<DocxProcessor>(".docx");
```

#### Tests
*To be implemented with document processor*

---

### 10. PDF Library (PdfPig)

**KLC Requirement:** KLC-REQ-009  
**Status:** ⚠️ Pending ADD Approval  
**Version:** TBD

#### Responsibility
Extracts text content from PDF files for document indexing.

#### Architecture Layer
- **Primary:** Knowledge Layer (Document Processor)

#### Usage
- **PDF Text Extraction:** Extract text from PDF pages
- **Layout Analysis:** Preserve reading order and structure
- **OCR Fallback:** May require OCR for image-based PDFs (future enhancement)
- **Metadata Extraction:** Extract PDF properties and annotations

#### Integration Points
- `Daiv3.Knowledge.DocProc` - PDF document processor

#### Configuration
*Pending ADD approval and implementation*

#### Tests
*To be defined after implementation*

---

### 11. Custom Scheduler (Replaces Quartz.NET)

**KLC Requirement:** KLC-REQ-010  
**Status:** ✅ Decision Made (Quartz.NET Rejected)  
**Version:** N/A (Custom Implementation)

#### Responsibility
Provides background task scheduling for document indexing, crawling, and maintenance operations.

#### Architecture Layer
- **Primary:** Infrastructure / Scheduler Service

#### Usage
- **Task Scheduling:** Schedule recurring and one-time tasks
- **Persistence:** Store schedule definitions in SQLite
- **Execution:** `BackgroundService` runs scheduled tasks
- **Priority Management:** Support for task priorities and dependencies
- **Resilience:** Handle failures and retry logic

#### Integration Points
- `Daiv3.Scheduler` - Custom scheduler implementation
- `Daiv3.Persistence` - Task and schedule storage
- `Daiv3.Orchestration` - Task execution integration

#### Configuration
```csharp
// Custom scheduler registration
services.AddSchedulerService(options =>
{
    options.CheckInterval = TimeSpan.FromSeconds(30);
    options.MaxConcurrentTasks = 4;
});
```

#### Tests
*To be implemented with scheduler service*

#### Rationale
Quartz.NET rejected in favor of custom implementation to:
- Minimize external dependencies
- Maintain full control over scheduling logic
- Integrate tightly with SQLite persistence
- Reduce attack surface

---

### 12. UI Framework (.NET MAUI)

**KLC Requirement:** KLC-REQ-011  
**Status:** ✅ In Use (Decision Documented)  
**Version:** .NET 10 MAUI

#### Responsibility
Provides cross-platform UI framework for desktop application (Windows 11 primary target).

#### Architecture Layer
- **Primary:** Presentation Layer

#### Usage
- **MVVM Pattern:** ViewModels and data binding
- **Navigation:** Shell-based navigation between pages
- **Cross-Platform:** Windows (primary), Android/iOS/macOS (future)
- **Native Controls:** Platform-specific UI controls
- **Pages:** Chat, Dashboard, Projects, Settings

#### Integration Points
- `Daiv3.App.Maui` - MAUI application project
- `Daiv3.Orchestration` - Service layer integration
- ViewModels communicate with orchestration layer services

#### Configuration
```csharp
// MAUI app configuration
var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>();
builder.Services.AddOrchestrationLayer();
builder.Services.AddModelExecutionLayer();
builder.Services.AddKnowledgeLayer();
builder.Services.AddPersistenceLayer();
```

#### Tests
- 41/43 unit tests for ViewModels and UI logic
- MVVM pattern validated
- Navigation and command binding tested

#### Project Structure
- `Pages/` - XAML pages (Chat, Dashboard, Projects, Settings)
- `ViewModels/` - MVVM ViewModels with INotifyPropertyChanged
- `Converters/` - Value converters for data binding
- `MauiProgram.cs` - DI container and service registration

---

## Component-to-Layer Mapping

| Component | Persistence | Knowledge | Model Execution | Orchestration | Presentation |
|-----------|-------------|-----------|-----------------|---------------|--------------|
| Microsoft.Data.Sqlite | ✅ Primary | - | - | - | - |
| Microsoft.ML.Tokenizers | - | ✅ Primary | ✅ Secondary | - | - |
| TensorPrimitives | - | ✅ Primary | ✅ Secondary | - | - |
| ONNX Runtime DirectML | - | ✅ Secondary | ✅ Primary | - | - |
| Microsoft.Extensions.AI | - | - | ✅ Primary | ✅ Secondary | - |
| Foundry Local SDK | - | - | ✅ Primary | - | - |
| DocumentFormat.OpenXml | - | ✅ Primary | - | - | - |
| HTML Parser (TBD) | - | ✅ Primary | - | - | - |
| PdfPig (TBD) | - | ✅ Primary | - | - | - |
| MCP SDK (TBD) | - | - | - | ✅ Primary | - |
| Custom Scheduler | ✅ Secondary | - | - | ✅ Secondary | - |
| .NET MAUI | - | - | - | - | ✅ Primary |

---

## Cross-Cutting Concerns

### Hardware Detection
**Component:** `IHardwareDetectionProvider` (Custom)  
**Layer:** Infrastructure.Shared  
**Used By:** Model Execution, Knowledge Layer  
**Purpose:** Detect NPU/GPU/CPU capabilities and guide execution provider selection

### Logging
**Component:** `Microsoft.Extensions.Logging`  
**Layer:** All layers  
**Purpose:** Structured logging with `ILogger<T>` for diagnostics and telemetry

### Dependency Injection
**Component:** `Microsoft.Extensions.DependencyInjection`  
**Layer:** All layers  
**Purpose:** Service registration, lifetime management, and testability

### Configuration
**Component:** `Microsoft.Extensions.Configuration`  
**Layer:** All layers  
**Purpose:** Options pattern for component configuration

---

## Verification

### Checklist for KLC-ACC-002 Compliance

- [x] All KLC-REQ-001 through KLC-REQ-011 components documented
- [x] Each component has defined responsibility
- [x] Architecture layer assignment specified
- [x] Usage patterns documented
- [x] Integration points identified
- [x] Configuration examples provided
- [x] Testing status tracked
- [x] Cross-layer mapping visualized

### Related Documents

- [Approved Dependencies](./approved-dependencies.md) - Dependency approval registry
- [Architecture Layer Boundaries](./architecture-layer-boundaries.md) - Layer constraints
- [Layer Interface Specifications](./layer-interface-specifications.md) - Interface contracts
- [Module Libraries Map](./module-libraries-map.md) - Dependency graph
- [Specification 12: Key .NET Libraries](../Specs/12-Key-Libraries-Components.md) - Source requirements

---

**Document Status:** Complete  
**KLC-ACC-002 Status:** ✅ Satisfied  
**Verified By:** AI Assistant  
**Date:** February 23, 2026
