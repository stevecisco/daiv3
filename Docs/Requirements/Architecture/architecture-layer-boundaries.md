# Architecture Layer Boundaries - DAIv3

## Purpose

This document specifies the architectural layers of the DAIv3 system, the boundaries between them, the interfaces exposed by each layer, and the constraints that maintain architectural integrity.

---

## Layered Architecture Overview

DAIv3 implements a five-layer architecture with unidirectional dependencies (higher layers depend on lower layers only). This design enables:

- **Maintainability:** Each layer has clearly defined responsibilities
- **Testability:** Layers can be tested independently via mockable interfaces
- **Flexibility:** Implementation can change within a layer without affecting consumers
- **Scalability:** New capabilities can be added to layers without restructuring

```
┌─────────────────────────────────────────────────────────────┐
│ PRESENTATION LAYER                                          │
│ (Daiv3.App.Cli, Daiv3.App.Maui, Daiv3.Api)                │
└─────────────────────────────────────────────────────────────┘
                           ↓ depends on
┌─────────────────────────────────────────────────────────────┐
│ ORCHESTRATION LAYER                                         │
│ (Daiv3.Orchestration, Daiv3.Scheduler)                     │
└─────────────────────────────────────────────────────────────┘
                           ↓ depends on
┌─────────────────────────────────────────────────────────────┐
│ MODEL EXECUTION LAYER                                       │
│ (Daiv3.ModelExecution, Daiv3.FoundryLocal.*, Online*)      │
└─────────────────────────────────────────────────────────────┘
                           ↓ depends on
┌─────────────────────────────────────────────────────────────┐
│ KNOWLEDGE LAYER                                             │
│ (Daiv3.Knowledge, Daiv3.Knowledge.DocProc, Embedding)      │
└─────────────────────────────────────────────────────────────┘
                           ↓ depends on
┌─────────────────────────────────────────────────────────────┐
│ PERSISTENCE LAYER                                           │
│ (Daiv3.Persistence, Daiv3.Infrastructure.Shared)           │
└─────────────────────────────────────────────────────────────┘
```

---

## LAYER 1: PERSISTENCE LAYER

### Purpose
Provides data access, entity management, and hardware abstraction. No layer depends on this layer from above (except through explicit references).

### Responsibilities
- SQLite database operations and migrations
- Entity definitions and repository patterns
- File system operations and I/O
- Hardware detection and capability abstraction
- Configuration and settings management
- Connection pooling and resource management

### Projects in This Layer
| Project | Purpose |
|---------|---------|
| `Daiv3.Persistence` | SQLite database, repositories, migrations, entities |
| `Daiv3.Infrastructure.Shared` | Hardware detection, platform abstraction, utilities |
| `Daiv3.Core` | Base types, utilities, common logging |

### Key Interfaces

#### IRepository<T>
```csharp
namespace Daiv3.Persistence.Interfaces;

public interface IRepository<T> where T : IEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task<T> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

#### IEntity
```csharp
namespace Daiv3.Persistence.Interfaces;

public interface IEntity
{
    Guid Id { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
}
```

#### IHardwareDetectionProvider
```csharp
namespace Daiv3.Infrastructure.Shared.Hardware;

public interface IHardwareDetectionProvider
{
    HardwareCapabilities DetectCapabilities();
    bool HasNpu { get; }
    bool HasGpu { get; }
    string CpuName { get; }
    string OsVersion { get; }
}
```

#### IDatabaseFactory
```csharp
namespace Daiv3.Persistence.Interfaces;

public interface IDatabaseFactory
{
    Task<SqliteConnection> CreateConnectionAsync(CancellationToken ct = default);
    Task ApplyMigrationsAsync(CancellationToken ct = default);
}
```

### Constraints
- **No Dependencies on Higher Layers:** Must not reference Orchestration, Model Execution, Knowledge, or Presentation layers
- **Single Responsibility:** Data access and hardware abstraction only
- **Async All I/O:** All I/O operations must be async
- **Error Handling:** Must throw and log appropriate exceptions; callers handle recovery
- **Resource Cleanup:** All resources (connections, file handles) must be properly disposed

### Configuration Contracts

#### Database Configuration
```csharp
public class PersistenceOptions
{
    public string DatabasePath { get; set; } = "daiv3.db";
    public int ConnectionPoolSize { get; set; } = 10;
    public bool EnsureMigrationsRun { get; set; } = true;
}
```

#### Hardware Detection Configuration
```csharp
public class HardwareDetectionOptions
{
    public bool EnableNpuPreference { get; set; } = true;
    public bool EnableGpuFallback { get; set; } = true;
    public string ExecutionProvider { get; set; } = "Auto";
}
```

### Dependency Injection Registration
```csharp
services.AddOptions<PersistenceOptions>();
services.AddScoped<IDatabaseFactory, SqliteDatabaseFactory>();
services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
services.AddScoped<IHardwareDetectionProvider, WindowsHardwareDetectionProvider>();
```

### Testing Strategy
- Unit Tests: Repository behavior, migrations, hardware detection
- No external dependencies beyond SQLite and file system
- Tests should use in-memory SQLite for isolation

---

## LAYER 2: KNOWLEDGE LAYER

### Purpose
Implements document processing, embeddings generation, semantic indexing, and vector-based search. Depends only on Persistence Layer.

### Responsibilities
- Document extraction and text processing (PDF, DOCX, HTML, Markdown, code)
- Text chunking and normalization
- Embedding generation via ONNX Runtime with hardware acceleration
- Two-tier vector indexing (topic and chunk levels)
- Semantic search and similarity computation
- Knowledge base management and synchronization

### Projects in This Layer
| Project | Purpose |
|---------|---------|
| `Daiv3.Knowledge` | Core knowledge management abstractions and coordination |
| `Daiv3.Knowledge.DocProc` | Document extraction and chunking |
| `Daiv3.Knowledge.Embedding` | Embedding generation with ONNX Runtime |

### Key Interfaces

#### IDocumentProcessor
```csharp
namespace Daiv3.Knowledge.DocProc.Interfaces;

public interface IDocumentProcessor
{
    Task<ProcessedDocument> ProcessAsync(string filePath, CancellationToken ct = default);
    bool SupportsFormat(string extension);
}

public class ProcessedDocument
{
    public string Content { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
```

#### IEmbeddingService
```csharp
namespace Daiv3.Knowledge.Embedding.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, int dimensionality, CancellationToken ct = default);
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts, 
        int dimensionality, 
        CancellationToken ct = default);
    
    string ModelName { get; }
    int DefaultDimensions { get; }
}
```

#### ITextChunker
```csharp
namespace Daiv3.Knowledge.DocProc.Interfaces;

public interface ITextChunker
{
    List<TextChunk> ChunkText(string text, int targetTokenCount = 400, int overlapTokens = 50);
}

public class TextChunk
{
    public string Content { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
}
```

#### IVectorSimilarityService
```csharp
namespace Daiv3.Knowledge.Embedding.Interfaces;

public interface IVectorSimilarityService
{
    float ComputeCosineSimilarity(float[] vector1, float[] vector2);
    List<(int Index, float Similarity)> ComputeBatchSimilarities(
        float[] queryVector, 
        List<float[]> documentVectors, 
        int topK = 10);
}
```

#### IKnowledgeIndex
```csharp
namespace Daiv3.Knowledge.Interfaces;

public interface IKnowledgeIndex
{
    Task<Guid> AddDocumentAsync(
        string filePath, 
        string content, 
        Dictionary<string, string> metadata, 
        CancellationToken ct = default);
    
    Task<List<(Guid DocumentId, float Score)>> SearchTier1Async(
        string query, 
        int topK = 10, 
        CancellationToken ct = default);
    
    Task<List<(Guid ChunkId, float Score)>> SearchTier2Async(
        List<Guid> documentCandidates, 
        string query, 
        int topK = 20, 
        CancellationToken ct = default);
    
    Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default);
}
```

### Constraints
- **Persistence Dependency Only:** May only depend on Persistence Layer
- **No Upstream Dependencies:** Must not reference Model Execution, Orchestration, or Presentation layers
- **Async I/O:** All database and file operations must be async
- **Hardware Utilization:** Must respect hardware detection from Persistence Layer for acceleration selection
- **Error Handling:** Must provide detailed error messages for embedding failures, corruption, etc.

### Configuration Contracts

#### Embedding Configuration
```csharp
public class EmbeddingOptions
{
    public string ModelName { get; set; } = "all-MiniLM-L6-v2";
    public int Tier1Dimensions { get; set; } = 384;
    public int Tier2Dimensions { get; set; } = 768;
    public string ExecutionProvider { get; set; } = "Auto";
    public int BatchSize { get; set; } = 32;
}
```

#### Document Processing Configuration
```csharp
public class DocumentProcessingOptions
{
    public int ChunkTokenCount { get; set; } = 400;
    public int ChunkOverlapTokens { get; set; } = 50;
    public List<string> SupportedExtensions { get; set; } = new()
    {
        ".pdf", ".docx", ".html", ".md", ".txt", ".cs", ".java", ".py"
    };
}
```

### Dependency Injection Registration
```csharp
services.AddOptions<EmbeddingOptions>();
services.AddOptions<DocumentProcessingOptions>();
services.AddScoped<IDocumentProcessor, MultiFormatDocumentProcessor>();
services.AddScoped<IEmbeddingService, OnnxEmbeddingService>();
services.AddScoped<IVectorSimilarityService, CpuVectorSimilarityService>();
services.AddScoped<IKnowledgeIndex, TwoTierKnowledgeIndex>();
```

### Testing Strategy
- Unit Tests: Document processing, chunking, similarity computation
- Integration Tests: End-to-end document indexing and retrieval
- Embedding tests should mock or use small models for speed
- Vector similarity tests validate mathematical correctness

---

## LAYER 3: MODEL EXECUTION LAYER

### Purpose
Manages model loading, task queuing, request routing between local and online providers, and execution orchestration. Depends on Knowledge and Persistence layers.

### Responsibilities
- Model lifecycle management (download, load, unload, cache management)
- Request queuing with priority-based scheduling
- Model selection and switching logic
- Online provider routing and rate limiting
- Budget and quota enforcement
- Inference execution (local or remote)

### Projects in This Layer
| Project | Purpose |
|---------|---------|
| `Daiv3.ModelExecution` | Queue management, model selection, execution orchestration |
| `Daiv3.FoundryLocal.Management` | Foundry Local integration and model management |
| `Daiv3.FoundryLocal.Bridge` | Bridge between DAIv3 and Foundry Local services |
| `Daiv3.OnlineProviders.Abstractions` | Online provider interfaces and contracts |
| `Daiv3.OnlineProviders.OpenAI` | OpenAI API integration |
| `Daiv3.OnlineProviders.AzureOpenAI` | Azure OpenAI API integration |
| `Daiv3.OnlineProviders.Anthropic` | Anthropic API integration |

### Key Interfaces

#### IModelQueue
```csharp
namespace Daiv3.ModelExecution.Interfaces;

public interface IModelQueue
{
    Task<Guid> EnqueueAsync(
        ExecutionRequest request, 
        ExecutionPriority priority = ExecutionPriority.Normal, 
        CancellationToken ct = default);
    
    Task<ExecutionResult> ProcessAsync(Guid requestId, CancellationToken ct = default);
    
    Task<ExecutionRequestStatus> GetStatusAsync(Guid requestId, CancellationToken ct = default);
}

public enum ExecutionPriority
{
    Immediate = 0,  // P0: Preempt everything
    Normal = 1,     // P1: Batch before switching
    Background = 2  // P2: Drain before switching
}

public class ExecutionRequest
{
    public Guid Id { get; set; }
    public string TaskType { get; set; }  // "chat", "search", "summarize", "code"
    public string Content { get; set; }
    public Dictionary<string, string> Context { get; set; }
}

public class ExecutionResult
{
    public Guid RequestId { get; set; }
    public string Content { get; set; }
    public ExecutionStatus Status { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

public enum ExecutionStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
```

#### IModelManagementService
```csharp
namespace Daiv3.ModelExecution.Interfaces;

public interface IModelManagementService
{
    Task<List<AvailableModel>> ListAvailableModelsAsync(CancellationToken ct = default);
    
    Task DownloadModelAsync(
        string modelId, 
        IProgress<DownloadProgress> progress = null, 
        CancellationToken ct = default);
    
    Task<List<string>> GetCachedModelsAsync(CancellationToken ct = default);
    
    Task DeleteModelAsync(string modelId, CancellationToken ct = default);
    
    Task<T> LoadModelAsync<T>(string modelId, CancellationToken ct = default) where T : class;
    
    Task UnloadModelAsync(string modelId, CancellationToken ct = default);
}

public class AvailableModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Version Version { get; set; }
    public List<DeviceType> SupportedDevices { get; set; }
    public long SizeBytes { get; set; }
}

public class DownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => (double)BytesDownloaded / TotalBytes;
}

public enum DeviceType
{
    Cpu,
    Gpu,
    Npu
}
```

#### IModelSelector
```csharp
namespace Daiv3.ModelExecution.Interfaces;

public interface IModelSelector
{
    string SelectModelForTask(
        string taskType, 
        Dictionary<string, string> context, 
        UserPreferences preferences);
    
    ExecutionPriority AssignPriority(string taskType);
}
```

#### IOnlineProvider
```csharp
namespace Daiv3.OnlineProviders.Abstractions;

public interface IOnlineProvider
{
    string ProviderName { get; }
    
    Task<string> GenerateAsync(
        string prompt, 
        OnlineInferenceOptions options, 
        CancellationToken ct = default);
    
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    
    decimal GetEstimatedCost(int inputTokens, int outputTokens);
}

public class OnlineInferenceOptions
{
    public string Model { get; set; }
    public int MaxTokens { get; set; } = 2048;
    public decimal Temperature { get; set; } = 0.7m;
    public List<string> SystemPrompts { get; set; }
}
```

### Constraints
- **Layered Dependencies:** May depend on Persistence and Knowledge layers only
- **No Presentation Dependencies:** Must not reference Presentation Layer components
- **Queue Determinism:** All queue operations must be deterministic and observable for testing
- **Graceful Degradation:** Must handle model load failures and fallback appropriately
- **Budget Enforcement:** Must respect token budgets and cost constraints
- **Async Execution:** Must support async model loading and inference

### Configuration Contracts

#### Model Execution Configuration
```csharp
public class ModelExecutionOptions
{
    public int MaxConcurrentRequests { get; set; } = 4;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public string PreferredLocalModel { get; set; } = "mistral-7b-instruct";
    public string IntentClassificationModel { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";
}
```

#### Online Provider Configuration
```csharp
public class OnlineProviderConfig
{
    public Dictionary<string, ProviderSettings> Providers { get; set; }
    public ProviderBudget Budget { get; set; }
}

public class ProviderSettings
{
    public string ApiKey { get; set; }
    public string Endpoint { get; set; }
    public decimal MaxMonthlyBudget { get; set; }
    public bool Enabled { get; set; }
}

public class ProviderBudget
{
    public decimal TotalMonthlyBudget { get; set; }
    public Dictionary<string, decimal> ProviderLimits { get; set; }
    public DateTimeOffset ResetDate { get; set; }
}
```

### Dependency Injection Registration
```csharp
services.AddOptions<ModelExecutionOptions>();
services.AddOptions<OnlineProviderConfig>();
services.AddScoped<IModelQueue, PriorityModelQueue>();
services.AddScoped<IModelManagementService, FoundryLocalManagementService>();
services.AddScoped<IModelSelector, TaskBasedModelSelector>();
services.AddScoped<IOnlineProvider, OpenAiProvider>();
services.AddScoped<IOnlineProvider, AzureOpenAiProvider>();
services.AddScoped<IOnlineProvider, AnthropicProvider>();
```

### Testing Strategy
- Unit Tests: Queue priority logic, model selection, budget enforcement
- Integration Tests: Queue with actual model management, online provider routing
- Mock online providers to avoid API costs during testing
- Test priority preemption and batching logic

---

## LAYER 4: ORCHESTRATION LAYER

### Purpose
Coordinates system-wide operations, resolves user intents, manages agents and skills, and orchestrates complex multi-step tasks. Depends on Model Execution, Knowledge, and Persistence layers.

### Responsibilities
- Task orchestration and workflow management
- Intent recognition and task decomposition
- Agent lifecycle and communication
- Skill registry and execution
- Session management
- Knowledge back-propagation coordination

### Projects in This Layer
| Project | Purpose |
|---------|---------|
| `Daiv3.Orchestration` | Core task orchestration and intent resolution |
| `Daiv3.Scheduler` | Task scheduling and recurring execution |

### Key Interfaces

#### ITaskOrchestrator
```csharp
namespace Daiv3.Orchestration.Interfaces;

public interface ITaskOrchestrator
{
    Task<OrchestrationResult> ExecuteAsync(
        UserRequest request, 
        CancellationToken ct = default);
    
    Task<List<ResolvedTask>> ResolveIntentAsync(
        string userInput, 
        CancellationToken ct = default);
}

public class UserRequest
{
    public string Input { get; set; }
    public Guid ProjectId { get; set; }
    public Dictionary<string, string> Context { get; set; }
}

public class ResolvedTask
{
    public string TaskType { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
    public int ExecutionOrder { get; set; }
    public List<Guid> Dependencies { get; set; }
}

public class OrchestrationResult
{
    public Guid SessionId { get; set; }
    public List<ExecutionResult> TaskResults { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}
```

#### IIntentResolver
```csharp
namespace Daiv3.Orchestration.Interfaces;

public interface IIntentResolver
{
    Task<Intent> ResolveAsync(
        string userInput, 
        Dictionary<string, string> context, 
        CancellationToken ct = default);
}

public class Intent
{
    public string Type { get; set; }  // "chat", "search", "create", "analyze", etc.
    public Dictionary<string, string> Entities { get; set; }
    public decimal Confidence { get; set; }
}
```

#### IAgentManager
```csharp
namespace Daiv3.Orchestration.Interfaces;

public interface IAgentManager
{
    Task<Agent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default);
    Task<Agent> GetAgentAsync(Guid agentId, CancellationToken ct = default);
    Task<List<Agent>> ListAgentsAsync(Guid? projectId = null, CancellationToken ct = default);
    Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}

public class AgentDefinition
{
    public string Name { get; set; }
    public string Purpose { get; set; }
    public List<string> EnabledSkills { get; set; }
    public Dictionary<string, string> Config { get; set; }
}

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Purpose { get; set; }
    public List<string> EnabledSkills { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

#### ISkillRegistry
```csharp
namespace Daiv3.Orchestration.Interfaces;

public interface ISkillRegistry
{
    void RegisterSkill(ISkill skill);
    ISkill ResolveSkill(string skillName);
    List<SkillMetadata> ListSkills();
}

public interface ISkill
{
    string Name { get; }
    string Description { get; }
    Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default);
}

public class SkillMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<ParameterMetadata> Parameters { get; set; }
}
```

### Constraints
- **Unidirectional Dependencies:** May only depend on Model Execution, Knowledge, and Persistence layers
- **No Presentation Coupling:** Must not be aware of specific UI implementations
- **Stateless Execution:** Tasks should be independently executable
- **Dependency Resolution:** All task dependencies must be resolved before queuing
- **Observable Execution:** All operations must be traceable for debugging

### Configuration Contracts

#### Orchestration Configuration
```csharp
public class OrchestrationOptions
{
    public int MaxConcurrentTasks { get; set; } = 4;
    public int TaskTimeoutSeconds { get; set; } = 600;
    public string DefaultIntentModel { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";
    public bool EnableTaskDependencyValidation { get; set; } = true;
}
```

### Dependency Injection Registration
```csharp
services.AddOptions<OrchestrationOptions>();
services.AddScoped<ITaskOrchestrator, TaskOrchestrator>();
services.AddScoped<IIntentResolver, IntentResolver>();
services.AddScoped<IAgentManager, AgentManager>();
services.AddScoped<ISkillRegistry, SkillRegistry>();
services.AddHostedService<TaskSchedulerService>();
```

### Testing Strategy
- Unit Tests: Intent resolution, task dependency resolution, agent management
- Integration Tests: End-to-end task orchestration with mocked model execution
- Task graph validation tests
- Isolated agent execution tests

---

## LAYER 5: PRESENTATION LAYER

### Purpose
Provides user-facing interfaces (UI and API). Depends on all lower layers.

### Responsibilities
- User interface rendering (CLI and MAUI)
- User input validation and translation to lower layer requests
- Response formatting and presentation
- Session and navigation management
- Configuration UI
- Status and progress reporting

### Projects in This Layer
| Project | Purpose |
|---------|---------|
| `Daiv3.App.Cli` | Command-line interface |
| `Daiv3.App.Maui` | Cross-platform graphical interface (WinUI 3 on Windows) |
| `Daiv3.Api` | REST API (optional, for future integrations) |

### Key Interfaces

#### ICommandHandler<T>
```csharp
namespace Daiv3.App.Cli.Interfaces;

public interface ICommandHandler<in TCommand>
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}
```

#### IUserSession
```csharp
namespace Daiv3.Presentation.Interfaces;

public interface IUserSession
{
    Guid SessionId { get; }
    Guid CurrentProjectId { get; set; }
    Dictionary<string, object> Context { get; set; }
}
```

### Constraints
- **API Consumers Only:** May only consume interfaces from lower layers
- **No Persistence Access:** Must not access database directly; use repositories through lower layers
- **Validation:** Must validate all user input before passing to orchestration
- **Error Formatting:** Must provide user-friendly error messages
- **Async All Operations:** All operations that interact with lower layers must be async

### Configuration Contracts

#### UI Configuration
```csharp
public class UserInterfaceOptions
{
    public string Theme { get; set; } = "Auto";
    public bool VerboseLogging { get; set; } = false;
    public int ProgressUpdateIntervalMs { get; set; } = 500;
}
```

### Dependency Injection Registration
```csharp
services.AddOptions<UserInterfaceOptions>();
services.AddScoped<IUserSession, UserSession>();
services.AddScoped(typeof(ICommandHandler<>), typeof(CommandHandler<>));
```

### Testing Strategy
- Unit Tests: Input validation, command handling, response formatting
- CLI integration tests with mocked orchestration
- MAUI component tests in isolation
- No direct database access in tests

---

## Dependency Rules (ARCH-CON-001)

### Strict Unidirectionality
```
Presentation     → Orchestration, Model Execution, Knowledge, Persistence
           ↓
Orchestration    → Model Execution, Knowledge, Persistence
           ↓
Model Execution  → Knowledge, Persistence
           ↓
Knowledge        → Persistence
           ↓
Persistence      (no upward dependencies)
```

### Violation Detection
- **Compile-Time:** Namespace restrictions and assembly references prevent violations
- **Runtime:** Dependency injection containers must only register valid dependencies
- **Test-Time:** Architecture tests verify no bidirectional references exist

---

## Integration Points

### Presentation → Orchestration
```csharp
// User submits a request via CLI/UI
var result = await taskOrchestrator.ExecuteAsync(
    new UserRequest { Input = userInput, ProjectId = projectId },
    cancellationToken);
```

### Orchestration → Model Execution
```csharp
// Orchestration queues a task for execution
var requestId = await modelQueue.EnqueueAsync(
    new ExecutionRequest { TaskType = "chat", Content = content },
    ExecutionPriority.Normal,
    cancellationToken);
```

### Model Execution → Knowledge
```csharp
// Model execution searches knowledge base
var results = await knowledgeIndex.SearchTier1Async(query, topK: 10);
```

### Knowledge → Persistence
```csharp
// Knowledge layer uses repositories
var documents = await documentRepository.GetAllAsync();
```

---

## Configuration and Usage

### Default Startup Configuration

All layers should be registered in DI at application startup:

```csharp
var services = new ServiceCollection();

// Persistence Layer
services.AddPersistenceLayer(config["Persistence"]);

// Knowledge Layer
services.AddKnowledgeLayer(config["Knowledge"]);

// Model Execution Layer
services.AddModelExecutionLayer(config["ModelExecution"]);

// Orchestration Layer
services.AddOrchestrationLayer(config["Orchestration"]);

// Presentation Layer
services.AddPresentationLayer(config["Presentation"]);
```

### Per-Layer Extension Methods

Each layer should provide an `Add*Layer()` extension method to register its dependencies:

```csharp
public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistenceLayer(
        this IServiceCollection services, 
        IConfiguration config)
    {
        services.Configure<PersistenceOptions>(config);
        services.AddScoped<IDatabaseFactory, SqliteDatabaseFactory>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        return services;
    }
}
```

---

## Testing Boundaries

### Each Layer Can Be Tested In Isolation

**Example: Testing Orchestration Layer Independently**

```csharp
[Fact]
public async Task ExecuteAsync_WithValidRequest_ReturnsOrchestrationResult()
{
    // Arrange: Mock dependencies from lower layers
    var mockModelQueue = new Mock<IModelQueue>();
    var mockKnowledgeIndex = new Mock<IKnowledgeIndex>();
    var orchestrator = new TaskOrchestrator(mockModelQueue.Object, mockKnowledgeIndex.Object);
    
    // Act
    var result = await orchestrator.ExecuteAsync(
        new UserRequest { Input = "summarize this document" },
        CancellationToken.None);
    
    // Assert
    Assert.True(result.Success);
    mockModelQueue.Verify(x => x.EnqueueAsync(It.IsAny<ExecutionRequest>(), It.IsAny<ExecutionPriority>(), It.IsAny<CancellationToken>()));
}
```

### Integration Tests Validate Cross-Layer Contracts

```csharp
[Fact]
public async Task FullPipeline_DocumentToSearch_ReturnsRelevantResults()
{
    // Arrange: Real Database, Knowledge, and Search services
    using var app = new WebApplicationFactory<Program>();
    var knowledgeIndex = app.Services.GetRequiredService<IKnowledgeIndex>();
    
    // Act: Add document and search
    var docId = await knowledgeIndex.AddDocumentAsync("test.txt", "content", new());
    var results = await knowledgeIndex.SearchTier1Async("search term");
    
    // Assert
    Assert.NotEmpty(results);
}
```

---

## Documenting Your Interfaces

When implementing a new interface in a layer, document:

1. **Purpose:** What problem does it solve?
2. **Inputs/Outputs:** What data contracts must be honored?
3. **Async Behavior:** Is it async? How are cancellations handled?
4. **Error Handling:** What exceptions can it throw?
5. **Resource Cleanup:** What resources must be disposed?
6. **Performance Expectations:** Latency targets, throughput limits?

Example:

```csharp
/// <summary>
/// Generates embeddings for text using ONNX Runtime with hardware acceleration.
/// </summary>
/// <remarks>
/// - Uses NPU if available, falls back to GPU, then CPU
/// - All embeddings are float32 vectors
/// - Batch operations are preferred for efficiency
/// - See: <see cref="EmbeddingOptions"/> for configuration
/// </remarks>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a single embedding vector.
    /// </summary>
    /// <param name="text">Text to embed (non-empty)</param>
    /// <param name="dimensionality">Target vector dimensions (384 or 768)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Float32 embedding vector</returns>
    /// <exception cref="ArgumentException">If text is empty or dimensionality is unsupported</exception>
    /// <exception cref="InvalidOperationException">If ONNX model fails to load</exception>
    Task<float[]> GenerateEmbeddingAsync(string text, int dimensionality, CancellationToken ct = default);
}
```

---

## References

- **System Architecture Spec:** [03-System-Architecture.md](03-System-Architecture.md)
- **Design Document:** [Daiv3_Design_Document.md](../Daiv3_Design_Document.md)
- **Implementation Plan:** [Implementation-Plan.md](../Implementation-Plan.md)

---

**Version:** 1.0  
**Date:** February 23, 2026  
**Status:** Complete - ARCH-REQ-001 Implementation Foundation
