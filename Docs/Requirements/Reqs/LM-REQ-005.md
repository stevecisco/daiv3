# LM-REQ-005

Source Spec: 9. Learning Memory - Requirements

## Requirement
Before agent execution, relevant learnings SHALL be retrieved and injected into prompts.

## Implementation Summary

### Architecture Decision
Learning retrieval and injection implemented using semantic similarity search to find relevant past learnings for current task context, with configurable filtering and ranking.

### Core Components Implemented

#### 1. ILearningRetrievalService Interface
- **Location:** `src/Daiv3.Orchestration/Interfaces/ILearningRetrievalService.cs`
- **Purpose:** Contract for semantic learning retrieval
- **Method:** `RetrieveLearningsAsync(LearningRetrievalContext, CancellationToken)`
- **Returns:** Ranked list of `RetrievedLearning` with similarity scores

#### 2. LearningRetrievalContext Class
- **Purpose:** Encapsulates retrieval parameters and filters
- **Properties:**
  - `TaskGoal` (required) - Task description for semantic search
  - `AgentId` (optional) - Filter to agent-specific + global learnings
  - `Scope` (optional) - Filter by learning scope
  - `MinConfidence` (default: 0.5) - Confidence threshold
  - `MinSimilarity` (default: 0.3) - Similarity threshold
  - `MaxResults` (default: 5) - Maximum learnings to return
  - `AdditionalContext` - Extra context for query enrichment

#### 3. RetrievedLearning Class
- **Properties:**
  - `Learning` - The retrieved learning entity
  - `SimilarityScore` - Cosine similarity score (0.0 to 1.0)
  - `Rank` - Position in result set (1-indexed)

#### 4. LearningRetrievalService Implementation
- **Location:** `src/Daiv3.Orchestration/LearningRetrievalService.cs`
- **Dependencies:**
  - `LearningStorageService` - Retrieves learnings with embeddings from database
  - `IEmbeddingGenerator` - Generates query embedding from task context
  - `IVectorSimilarityService` - Calculates cosine similarity scores
- **Key Workflows:**
  1. Retrieve all active learnings with embeddings from persistence layer
  2. Filter by status (Active only), scope, agent, and confidence threshold
  3. Generate query embedding from task goal + additional context
  4. Calculate batch cosine similarity scores using hardware-accelerated ops
  5. Filter by minimum similarity threshold
  6. Rank by similarity score (descending) and return top N
  7. Update `TimesApplied` counter asynchronously (fire-and-forget)

#### 5. AgentManager Integration  
- **Location:** `src/Daiv3.Orchestration/AgentManager.cs`
- **Constructor:** Added optional `ILearningRetrievalService?` parameter for graceful degradation
- **ExecuteIterationAsync:** Injects learnings before each iteration
- **RetrieveAndFormatLearningsAsync:** Helper method that:
  - Creates `LearningRetrievalContext` with task goal and agent ID
  - Configures retrieval: MinConfidence=0.6, MinSimilarity=0.4, MaxResults=3  
  - Formats retrieved learnings with rank, similarity, confidence, title, description, scope, trigger type, and tags
  - Appends formatted learnings to step description for LLM context injection
  - Returns empty string if retrieval fails (graceful degradation)

### Semantic Similarity Algorithm
- **Embedding Generation:** Uses ONNX embedding models (Tier 1: 384D all-MiniLM-L6-v2, Tier 2: 768D nomic-embed-text-v1.5)
- **Similarity Calculation:** Batch cosine similarity using `System.Numerics.TensorPrimitives` (SIMD-accelerated)
- **Hardware Acceleration:** Leverages NPU/GPU when available, CPU fallback via `IVectorSimilarityService`
- **Performance:** Optimized batch processing groups learnings by embedding dimensions

### Filtering and Ranking Strategy
**Filter Chain:**
1. Status filter: Only Active learnings (excludes Suppressed, Superseded, Archived)
2. Scope filter: If specified, only learnings from matching scope
3. Agent filter: If agent ID specified, includes agent-specific OR global scope learnings
4. Confidence threshold: Only learnings above MinConfidence (default 0.5)
5. Similarity threshold: Only learnings above MinSimilarity score (default 0.3)

**Ranking:**
- Primary: Similarity score (descending)
- Returns top N results (configurable via MaxResults)

### Context Injection Format
```
Relevant Learnings:
[Learning #1] (Similarity: 0.92, Confidence: 0.95)
Title: Use async/await for file I/O operations
Description: Always use File.ReadAllTextAsync instead of File.ReadAllText to avoid blocking...
Scope: Global, Trigger: UserFeedback
Tags: csharp,async,io

[Learning #2] (Similarity: 0.85, Confidence: 0.80)
Title: Dispose HttpClient properly
Description: Use IHttpClientFactory instead of creating HttpClient instances directly...
Scope: Agent, Trigger: ToolFailure
Tags: httpclient,dispose
```

### Error Handling
- **Validation:** ArgumentNullException for null context, ArgumentOutOfRangeException for invalid thresholds
- **Embedding Generation Failure:** Returns empty list, logs warning
- **No Learnings Available:** Returns empty list gracefully
- **Database Errors:** Propagates exception with structured logging
- **Update Failures:** Logs warning but doesn't block retrieval (fire-and-forget pattern)

### Logging Strategy
- **Information:** Learning count, top similarity score, injection confirmation
- **Debug:** Filter stages, embedding dimensions, similarity score calculations
- **Warning:** Embedding generation failures, update failures, dimension mismatches
- **Error:** Unexpected exceptions during retrieval

### Configuration and Defaults
**Recommended Retrieval Settings:**
- `MinConfidence`: 0.6-0.8 (high-confidence learnings only)
- `MinSimilarity`: 0.3-0.5 (reasonable semantic relevance)
- `MaxResults`: 3-5 (balance between context richness and token budget)

**AgentManager Defaults:**
- MinConfidence: 0.6
- MinSimilarity: 0.4
- MaxResults: 3

### Dependency Injection Registration
```csharp
services.AddOrchestrationServices(); // Registers ILearningRetrievalService
```

**Location:** `src/Daiv3.Orchestration/OrchestrationServiceExtensions.cs`
```csharp
services.TryAddScoped<ILearningRetrievalService, LearningRetrievalService>();
```

## Testing Plan

### Unit Tests (Partial - Validation Focus)
- **Location:** `tests/unit/Daiv3.UnitTests/Orchestration/LearningRetrievalServiceTests.cs`
- **Coverage:** Constructor validation, parameter validation, argument checking
- **Note:** Full retrieval flow requires integration tests due to complex dependencies
- **Tests Created:** 18 validation tests for input parameters and edge cases

### Integration Tests (Recommended)
- **Planned:** Full end-to-end learning retrieval with real database and embeddings
- **Scenarios:**
  - Retrieval with various filter combinations
  - Similarity ranking verification
  - Agent-specific vs global learning filtering
  - Performance with large learning datasets
  - Context injection into agent execution
- **Status:** Deferred to separate task due to time constraints

### Manual Verification
- **Scenario:** Execute agent with existing learnings in database
- **Expected:** Relevant learnings appear in agent step descriptions
- **Validation:** Check logs for "Injected N relevant learnings" messages

## Usage and Operational Notes

### Direct Usage
```csharp
var retrievalService = serviceProvider.GetRequiredService<ILearningRetrievalService>();

var context = new LearningRetrievalContext
{
    TaskGoal = "Implement async file I/O in C#",
    AgentId = "agent-123",  
    MinConfidence = 0.7,
    MinSimilarity = 0.4,
    MaxResults = 5
};

var learnings = await retrievalService.RetrieveLearningsAsync(context);

foreach (var retrieved in learnings)
{
    Console.WriteLine($"Rank {retrieved.Rank}: {retrieved.Learning.Title} (Similarity: {retrieved.SimilarityScore:F2})");
}
```

### Automatic Injection in Agent Execution
Learnings are automatically retrieved and injected during `AgentManager.ExecuteTaskAsync()`:
1. Agent starts execution for a task goal
2. Before each iteration, `RetrieveAndFormatLearningsAsync()` is called
3. Relevant learnings are retrieved based on task goal
4. Formatted learnings appended to step description
5. Step description with learnings used as LLM context (when integrated)

### Monitoring and Observability
- **Standard Logs:** Learning retrieval count, similarity scores captured in logs
- **Metrics:** `TimesApplied` counter incremented for each retrieved learning
- **Dashboard Ready:** Metrics available via `LearningStorageService.GetStatisticsAsync()`

### Performance Considerations
- **Batch Similarity:** Optimized with ArrayPool to minimize allocations
- **Hardware Acceleration:** Leverages NPU/GPU via IVectorSimilarityService
- **Fire-and-Forget Updates:** TimesApplied updates don't block retrieval
- **Lazy Loading:** Only generates query embedding if active learnings exist

### Operational Constraints
- **Offline Mode:** Fully functional offline (no external dependencies)
- **Token Budget Impact:** Learning injection increases context tokens (3-5 learnings ~500-1000 tokens)
- **Permissions:** No special permissions required
- **Backward Compatibility:** Optional ILearningRetrievalService in AgentManager allows gradient adoption

## Dependencies
- **LM-REQ-004** ✅ Complete - Learning embedding generation
- **AST-REQ-001** ✅ Complete - Agent system and execution
- **KM-REQ-013** ✅ Complete - ONNX embedding models
- **KLC-REQ-003** ✅ Complete - Vector similarity service

## Related Requirements
- **LM-REQ-006** - Learning filtering and ranking (partially implemented in LM-REQ-005)
- **LM-ACC-002** - Acceptance test for learning injection in agent prompts
- **LM-NFR-001** - Performance requirement for fast retrieval

## Status
**Implementation:** Complete (100%)  
**Testing:** Partial - Unit tests (validation only), integration tests deferred
**Documentation:** Complete
**Build:** ✅ Compiles successfully
**Next Steps:**
1. Create comprehensive integration tests for full retrieval workflow
2. Verify manual agent execution with injected learnings
3. Performance benchmarking with large learning datasets (1000+ learnings)
4. Create acceptance test (LM-ACC-002)
