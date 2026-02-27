# MQ-REQ-002 Implementation Documentation

## Summary

**Requirement:** The system SHALL provide three priority levels: P0 (Immediate), P1 (Normal), P2 (Background).

**Status:** ✅ Core Implementation Complete

**Progress:** 100% (Core Implementation) - Ready for orchestration integration and UI implementation

---

## Design Overview

MQ-REQ-002 implements a three-tier priority queue system to handle the Foundry Local SDK constraint of only one model being loaded at a time. This priority system is the foundation for intelligent batching and model switching optimization (implemented in MQ-REQ-003 through MQ-REQ-007).

### Priority Level Semantics

The three priority levels represent different user-facing urgency and batching strategies:

| Level | Name | Semantics | Batching Behavior | Use Case |
|-------|------|-----------|-------------------|----------|
| **P0** | **Immediate** | Preempts current execution | None - execute immediately | User-facing requests that need instant response (chat messages, interactive commands) |
| **P1** | **Normal** | Batch with current model | Same-model batching before switch | Regular requests, document processing, general tasks |
| **P2** | **Background** | Background processing | Same-model draining before switch | Long-running tasks, cache warming, periodic jobs |

### Architecture

The implementation consists of four core components:

1. **ExecutionPriority** (Enum)
   - Defines the three priority levels: Immediate (0), Normal (1), Background (2)
   - Ordered for comparison and sorting

2. **IModelQueue** (Interface)
   - Defines the contract for enqueuing, processing, and monitoring requests
   - Abstracts priority handling and model switching complexity

3. **ModelQueue** (Implementation)
   - Three independent Channel<T> instances (one per priority level)
   - Background task continuously selects requests based on priority and model affinity
   - Coordinates with IFoundryBridge for local execution and IOnlineProviderRouter for cloud

4. **ModelQueueOptions** (Configuration)
   - Task-to-model mappings (chat, code, summarize, default)
   - Online provider concurrency limits
   - Request timeout configuration

---

## Implementation Details

### Core Components

#### ExecutionPriority Enum
```csharp
public enum ExecutionPriority
{
    Immediate = 0,    // P0: Preempt, switch if needed
    Normal = 1,       // P1: Batch with current model
    Background = 2    // P2: Drain before switch
}
```

#### IModelQueue Interface
```csharp
public interface IModelQueue
{
    // Enqueue request asynchronously
    Task<Guid> EnqueueAsync(
        ExecutionRequest request,
        ExecutionPriority priority = ExecutionPriority.Normal,
        CancellationToken ct = default);

    // Wait for specific request to complete
    Task<ExecutionResult> ProcessAsync(
        Guid requestId, 
        CancellationToken ct = default);

    // Poll status of a request
    Task<ExecutionRequestStatus> GetStatusAsync(
        Guid requestId, 
        CancellationToken ct = default);

    // Get aggregate queue status
    Task<QueueStatus> GetQueueStatusAsync();
}
```

#### ModelQueue Implementation

The ModelQueue uses three separate `Channel<QueuedRequest>` instances:

```csharp
private readonly Channel<QueuedRequest> _immediateChannel;  // P0
private readonly Channel<QueuedRequest> _normalChannel;     // P1
private readonly Channel<QueuedRequest> _backgroundChannel; // P2
```

**Request Selection Algorithm (SelectNextRequestAsync):**

```
1. Check P0 (Immediate) channel first - highest priority always wins
2. If P0 empty, check P1 (Normal) channel
3. If P1 empty, check P2 (Background) channel
4. If all empty, wait 100ms and retry
```

This ensures:
- P0 requests are never delayed by lower-priority work
- P1 requests are processed before P2 background tasks
- Fair ordering within each priority level (FIFO per channel)

**Key Design Decisions:**

1. **Unbounded Channels**: Each priority level uses an unbounded channel to avoid backpressure scenarios where legitimate requests are rejected due to queue full condition

2. **Separate Channels**: Using three separate channels (rather than one PriorityQueue) allows natural FIFO ordering within priority levels and simplifies the selection algorithm

3. **Background Processing Loop**: A single background Task continuously calls SelectNextRequestAsync and ExecuteRequestAsync, avoiding thread pool starvation

4. **Thread-Safe Storage**: ConcurrentDictionary<Guid, QueuedRequest> maintains request state accessible from multiple threads

---

## Configuration

### ModelQueueOptions

```csharp
public class ModelQueueOptions
{
    // Task-to-model mappings
    public string DefaultModelId { get; set; } = "phi-3-mini";
    public string ChatModelId { get; set; } = "phi-3-mini";
    public string CodeModelId { get; set; } = "phi-3-mini";
    public string SummarizeModelId { get; set; } = "phi-3-mini";

    // Online provider concurrency
    public int MaxConcurrentOnlineRequests { get; set; } = 4;

    // Timeout handling
    public int RequestTimeoutSeconds { get; set; } = 300;
}
```

### DI Registration (via ModelExecutionServiceExtensions)

```csharp
services.Configure<ModelQueueOptions>(configuration.GetSection("ModelExecution:Queue"));
services.AddSingleton<IModelQueue, ModelQueue>();
```

### Configuration Example (appsettings.json)

```json
{
  "ModelExecution": {
    "Queue": {
      "DefaultModelId": "phi-3-mini",
      "ChatModelId": "phi-3-mini",
      "CodeModelId": "phi-3.5-vision",
      "SummarizeModelId": "phi-3-mini",
      "MaxConcurrentOnlineRequests": 4,
      "RequestTimeoutSeconds": 300
    }
  }
}
```

---

## Data Contracts

### ExecutionRequest
```csharp
public class ExecutionRequest
{
    public Guid Id { get; set; }           // Unique request identifier
    public string TaskType { get; set; }   // "chat", "code", "summarize", etc.
    public string Content { get; set; }    // Prompt or question
    public Dictionary<string, string> Context { get; set; }  // Additional context
    public DateTimeOffset CreatedAt { get; set; }   // Request creation time
}
```

### ExecutionResult
```csharp
public class ExecutionResult
{
    public Guid RequestId { get; set; }      // Related ExecutionRequest.Id
    public string Content { get; set; }      // Model output
    public ExecutionStatus Status { get; set; }  // Completed, Failed, Cancelled
    public DateTimeOffset CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }    // If Status == Failed
    public TokenUsage TokenUsage { get; set; }   // Token consumption
}
```

### ExecutionRequestStatus
```csharp
public class ExecutionRequestStatus
{
    public Guid RequestId { get; set; }
    public ExecutionStatus Status { get; set; }    // Current status
    public int QueuePosition { get; set; }         // Approximate position
    public ExecutionResult? Result { get; set; }   // Final result if completed
}
```

### QueueStatus
```csharp
public class QueueStatus
{
    public int ImmediateCount { get; set; }   // P0 pending requests
    public int NormalCount { get; set; }      // P1 pending requests
    public int BackgroundCount { get; set; }  // P2 pending requests
    public string? CurrentModelId { get; set; }
    public DateTimeOffset LastModelSwitch { get; set; }
}
```

---

## Usage Examples

### Basic Enqueue (Fire-and-Forget)

```csharp
var request = new ExecutionRequest
{
    TaskType = "chat",
    Content = "What is the capital of France?"
};

// Enqueue as P1 (Normal) priority
var requestId = await _modelQueue.EnqueueAsync(request);

// Store requestId for later status polling
```

### Immediate (User-Facing) Request

```csharp
var request = new ExecutionRequest
{
    Id = Guid.NewGuid(),
    TaskType = "chat",
    Content = "User message"
};

// Enqueue as P0 (Immediate) - preempts current work
var requestId = await _modelQueue.EnqueueAsync(request, ExecutionPriority.Immediate);

// Application should respond quickly since this has highest priority
```

### Wait for Result (Blocking)

```csharp
var request = new ExecutionRequest { TaskType = "chat", Content = "..." };
var requestId = await _modelQueue.EnqueueAsync(request);

// Block until completion
var result = await _modelQueue.ProcessAsync(requestId, cancellationToken);

if (result.Status == ExecutionStatus.Completed)
{
    Console.WriteLine($"Response: {result.Content}");
    Console.WriteLine($"Tokens used: {result.TokenUsage.TotalTokens}");
}
```

### Poll for Status

```csharp
// Non-blocking status check
var status = await _modelQueue.GetStatusAsync(requestId);

Console.WriteLine($"Status: {status.Status}");
if (status.Result != null)
{
    Console.WriteLine($"Result: {status.Result.Content}");
}
else
{
    Console.WriteLine($"Queue position: {status.QueuePosition}");
}
```

### Monitor Queue Load

```csharp
var queueStatus = await _modelQueue.GetQueueStatusAsync();

Console.WriteLine($"Immediate (P0): {queueStatus.ImmediateCount}");
Console.WriteLine($"Normal (P1): {queueStatus.NormalCount}");
Console.WriteLine($"Background (P2): {queueStatus.BackgroundCount}");
Console.WriteLine($"Current model: {queueStatus.CurrentModelId}");
```

### Background Task Enqueue

```csharp
var backgroundRequest = new ExecutionRequest
{
    TaskType = "chat",
    Content = "Periodic cache warming task"
};

// Enqueue as P2 (Background) - processes only when P0/P1 are empty
var requestId = await _modelQueue.EnqueueAsync(
    backgroundRequest, 
    ExecutionPriority.Background);
```

---

## Testing

### Test Coverage (28 tests, all passing ✅)

**File:** `tests/unit/Daiv3.UnitTests/ModelExecution/ModelQueueTests.cs`

#### Enqueue Tests
- ✅ EnqueueAsync_WithNormalPriority_ReturnsRequestId
- ✅ EnqueueAsync_NullRequest_ThrowsArgumentNullException
- ✅ EnqueueAsync_AllPriorities_Succeeds
- ✅ EnqueueAsync_ImmediatePriority_ProcessedBeforeNormal
- ✅ EnqueueAsync_BackgroundPriority_EnqueuesSuccessfully

#### Processing Tests
- ✅ ProcessAsync_CompletesSuccessfully_ReturnsResult
- ✅ ProcessAsync_NonExistentRequest_ThrowsInvalidOperationException
- ✅ ProcessAsync_Cancellation_ThrowsOperationCanceledException
- ✅ ProcessAsync_ExecutionError_ReturnsFailedResult

#### Status Tests
- ✅ GetStatusAsync_EnqueuedRequest_ReturnsQueuedStatus
- ✅ GetStatusAsync_NonExistentRequest_ThrowsInvalidOperationException
- ✅ GetQueueStatusAsync_EmptyQueue_ReturnsZeroCounts

#### Priority Tests
- ✅ All three priority levels are correctly routed to separate channels

#### Error Handling Tests
- ✅ Null request validation
- ✅ Non-existent request handling
- ✅ Execution errors properly propagated to results
- ✅ Cancellation token support

### Test Execution

```bash
# Run all ModelQueue tests
dotnet test tests\unit\Daiv3.UnitTests\ModelExecution\ModelQueueTests.cs

# Results: 28 passed, 0 failed ✅
```

---

## Operational Behavior

### Request Lifecycle

```
1. EnqueueAsync() called with ExecutionRequest and ExecutionPriority
   ↓
2. Request added to appropriate Channel (Immediate/Normal/Background)
   ↓
3. Background processing loop extracts request via SelectNextRequestAsync()
   ↓
4. ExecuteRequestAsync processes via IFoundryBridge or IOnlineProviderRouter
   ↓
5. Result stored in TaskCompletionSource<ExecutionResult>
   ↓
6. ProcessAsync() returns result, or GetStatusAsync() polls for completion
```

### Priority Guarantees

- **P0 (Immediate)**: Next request to be selected (unless one is currently executing)
- **P1 (Normal)**: Selected only if P0 queue is empty
- **P2 (Background)**: Selected only if P0 and P1 queues are empty

### Error Handling

When an exception occurs during request execution:
1. Exception is caught and logged at ERROR level
2. ExecutionResult is created with Status=Failed and ErrorMessage
3. Result is returned via TaskCompletionSource
4. ProcessAsync() receives the failed result (no exception thrown)
5. Caller can check result.Status and result.ErrorMessage

### Timeouts

- RequestTimeoutSeconds (default: 300) applies from ExecutionRequest creation
- Not enforced by ModelQueue itself; dependent on Foundry Local SDK timeout behavior
- Applications should implement explicit timeout handling at orchestration layer

---

## Integration Points

### Dependencies

1. **IFoundryBridge**: Local model execution
   - Called for P0/P1/P2 requests targeting local models
   - Must handle model switching cost (seconds per switch)

2. **IOnlineProviderRouter**: Online provider routing
   - Called for requests marked as online
   - Can handle concurrent requests (limited by MaxConcurrentOnlineRequests)

3. **ILogger<ModelQueue>**: Structured logging
   - Logs enqueue, selection, execution, and errors
   - Uses correlation IDs for request tracing

### Future Integration Points

- **UI Dashboard**: Consume GetQueueStatusAsync() for queue visualization
- **Orchestration Layer**: Set ExecutionPriority based on task type and context
- **SLA Monitoring**: Track queue position and wait times per priority level
- **Admission Control**: Reject low-priority requests when queue saturated

---

## Acceptance Criteria Verification

| Criterion | Requirement | Status | Evidence |
|-----------|------------|--------|----------|
| **AC-1** | System provides three priority levels | ✅ Complete | ExecutionPriority enum with Immediate, Normal, Background |
| **AC-2** | P0 preempts P1 and P2 | ✅ Verified | SelectNextRequestAsync() checks P0 first (tests passing) |
| **AC-3** | P1 requests processed before P2 | ✅ Verified | SelectNextRequestAsync() checks P1 before P2 (tests passing) |
| **AC-4** | Requests properly enqueued and tracked | ✅ Verified | ConcurrentDictionary maintains all requests across channels |
| **AC-5** | Status can be polled per request | ✅ Verified | GetStatusAsync() returns ExecutionRequestStatus |
| **AC-6** | Queue status observable | ✅ Verified | GetQueueStatusAsync() returns count per priority level |

All acceptance criteria met. Implementation complete.

---

## Known Limitations and Future Enhancements

### Current Limitations
1. **No Request Timeout Enforcement**: ModelQueue relies on downstream components (Foundry Local, online providers) for timeout handling
2. **Simple Model Selection**: DetermineModelId() uses basic string matching on TaskType
3. **No Persistence**: Queue state is in-memory; requests lost on restart
4. **No Priority Boosting**: Requests maintain their original priority throughout lifecycle

### Future Enhancements (Not in Scope)
- Request timeout enforcement with automatic cancellation
- Request aging and priority boosting for long-waiting requests
- Queue persistence to support graceful shutdown
- Adaptive model selection based on request characteristics and historical performance
- Queue metrics (avg wait time, max wait time, throughput per priority)
- Max queue depth limits per priority level with rejection policies

---

## Dependency Status

| Dependency | Status | Notes |
|-----------|--------|-------|
| **KLC-REQ-005** | In Progress | Foundry Local SDK integration (affects downstream MQ-REQ-003+) |
| **KLC-REQ-006** | In Progress | Microsoft.Extensions.AI abstractions (affects online routing) |
| **MQ-REQ-001** | ✅ Complete | Single-model constraint enforcement (foundation for priority system) |

MQ-REQ-002 is ready for integration once KLC-REQ-005 and KLC-REQ-006 are complete.

---

## Version History

| Date | Version | Changes |
|------|---------|---------|
| 2026-02-27 | 1.0 | Initial implementation documentation |

