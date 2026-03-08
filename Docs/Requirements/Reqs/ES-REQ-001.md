# ES-REQ-001

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHALL process user requests using local models by default.

## Acceptance Criteria

| Criterion | Description | Test Method |
|-----------|-------------|------------|
| AC-1: Local-First Default | Requests with regular task types (chat, code, etc.) route to local models by default without explicit configuration | Unit test: `Enqueue_PreferLocalModelsDefault_RoutesToLocal` |
| AC-2: Local-Only Task Types | Requests with task types marked as "local-only" (Search, PrivacySensitive) always route to local, never online | Unit test: `Enqueue_LocalOnlyTaskType_RoutesToLocal` |
| AC-3: Online-Only Task Types | Requests with task types marked as "online-only" always route to online providers, never local | Unit test: `Enqueue_OnlineOnlyTaskType_RoutesToOnline` |
| AC-4: Configuration Respected | Configuration option `PreferLocalModelsDefault` controls routing when PreferLocalModelsDefault=false, routes online by default | Unit test: `Enqueue_PreferLocalModelsDefaultFalse_RoutesToOnline` |
| AC-5: Fallback to Online | When no local models are available, system gracefully falls back to online providers | Unit test: `Enqueue_NoAvailableLocalModels_RoutesToOnline` |
| AC-6: Clear Logging | Routing decisions are logged with sufficient detail to understand why each request was routed locally or online | Production code: `ShouldRouteOnline()` method logging |

## Implementation Details

### Overview
ES-REQ-001 implements a local-first routing decision system that prioritizes privacy, latency, and offline capability. The system evaluates requests at enqueue time in the ModelQueue to determine whether each request should execute locally (via Foundry Local) or online (via OnlineProviderRouter).

### Architecture

**Component Ownership:** `Daiv3.ModelExecution.ModelQueue`

**Decision Point:** `ModelQueue.ShouldRouteOnline(ExecutionRequest)` (private method called during ProcessQueueAsync)

**Configuration:** `LocalFirstRouteOptions` (new configuration class)

### Configuration Schema

```csharp
public class LocalFirstRouteOptions
{
    // Default: true - Implements local-first principle
    public bool PreferLocalModelsDefault { get; set; } = true;

    // Default: ["Search", "PrivacySensitive", "OfflineOnly"]
    public List<string> LocalOnlyTaskTypes { get; set; };

    // Default: [] (empty)
    public List<string> OnlineOnlyTaskTypes { get; set; };

    // Default: ["phi-3-mini", "phi-3", "phi-4", "mistral-7b", "llama-2-7b"]
    public List<string> AvailableLocalModels { get; set; };

    // Default: false
    public bool RouteOnlineOnQueueBacklog { get; set; };

    // Default: 10 - Backlog threshold for online routing
    public int QueueBacklogThreshold { get; set; } = 10;

    // Default: false
    public bool FailIfLocalUnavailable { get; set; };

    // Default: "Information"
    public string RoutingDecisionLogLevel { get; set; } = "Information";
}
```

### Configuration in appsettings.json

```json
{
  "LocalFirstRouting": {
    "PreferLocalModelsDefault": true,
    "LocalOnlyTaskTypes": ["Search", "PrivacySensitive", "OfflineOnly"],
    "OnlineOnlyTaskTypes": [],
    "AvailableLocalModels": ["phi-3-mini", "phi-3", "phi-4"],
    "RouteOnlineOnQueueBacklog": false,
    "QueueBacklogThreshold": 10,
    "FailIfLocalUnavailable": false,
    "RoutingDecisionLogLevel": "Information"
  }
}
```

### Routing Decision Tree

```
ShouldRouteOnline(request):
1. Check LocalOnlyTaskTypes:
   - If request.TaskType in LocalOnlyTaskTypes:
     Log(Debug): "Task type '{taskType}' is local-only"
     return false (use local)

2. Check OnlineOnlyTaskTypes:
   - If request.TaskType in OnlineOnlyTaskTypes:
     Log(Debug): "Task type '{taskType}' is online-only"
     return true (use online)

3. Check PreferLocalModelsDefault:
   - If PreferLocalModelsDefault == false:
     Log(Debug): "PreferLocalModelsDefault=false, routing to online"
     return true (use online)

4. Check AvailableLocalModels:
   - If AvailableLocalModels is empty:
     Log(Warning): "No local models configured, routing to online"
     return true (use online)

5. Check Queue Backlog (optional):
   - If RouteOnlineOnQueueBacklog == true:
     - If GetCurrentQueueDepth() >= QueueBacklogThreshold:
       Log(Information): "Local queue backlog ({depth} >= {threshold}), routing to online"
       return true (use online for latency relief)

6. Default (implements ES-REQ-001):
   Log(Debug): "Routing to local model (local-first default)"
   return false (use local)
```

### Integration Points

1. **DI Registration** (ModelExecutionServiceExtensions):
   - `AddOptions<LocalFirstRouteOptions>().Bind(configuration.GetSection("LocalFirstRouting"))`
   - ModelQueue constructor: `IOptions<LocalFirstRouteOptions> localFirstOptions` parameter

2. **Request Flow**:
   ```
   User Request
   → TaskOrchestrator.ExecuteAsync()
   → IntentResolver (classifies TaskType)
   → ModelQueue.EnqueueAsync()
   → ModelQueue.ProcessQueueAsync()
   → ModelQueue.ShouldRouteOnline(request)  ← DECISION POINT
     ├─ true  → IOnlineProviderRouter.ExecuteAsync()
     └─ false → IFoundryBridge.ExecuteAsync()
   ```

## Usage and Operational Notes

### User Perspective

**Default Behavior:** All requests use local models by default (offline-first, privacy-first).

**Examples:**
- `daiv3 chat "What's the weather?"` → Routes to local model (default)
- Search operations within knowledge base → Route to local (Search is LocalOnlyTaskType)
- Translation request with OnlineTranslation task type → Routes to online (if configured)

### Operational Constraints

1. **Offline Mode:** When system is offline (verified by NetworkConnectivityService), online routing requests fail or queue for retry
2. **Local Model Availability:** If no local models are configured or available, system falls back to online
3. **Performance:** Local-first routing may result in higher latency if local models are CPU-only. Use backlog-based routing to improve responsiveness
4. **Privacy:** LocalOnlyTaskTypes ensure certain operations never leave the device

### Configuration Examples

**Example 1: Strict Local-First (Default)**
```json
{
  "LocalFirstRouting": {
    "PreferLocalModelsDefault": true,
    "FailIfLocalUnavailable": true
  }
}
```
Effect: All requests route to local. Fails if local unavailable.

**Example 2: Balanced (Local + Online Fallback)**
```json
{
  "LocalFirstRouting": {
    "PreferLocalModelsDefault": true,
    "FailIfLocalUnavailable": false
  }
}
```
Effect: Requests prefer local, but gracefully fall back to online if needed.

**Example 3: Enhanced User Experience (With Backlog Relief)**
```json
{
  "LocalFirstRouting": {
    "PreferLocalModelsDefault": true,
    "RouteOnlineOnQueueBacklog": true,
    "QueueBacklogThreshold": 5
  }
}
```
Effect: Normal requests route to local, but if queue > 5 items, new requests go online for faster response.

## Testing

### Unit Tests (8 tests, all passing)

**Location:** `tests/unit/Daiv3.ModelExecution.Tests/ModelQueueLocalFirstRoutingTests.cs`

| Test | Purpose | Expected Result |
|------|---------|-----------------|
| `Enqueue_LocalOnlyTaskType_RoutesToLocal` | Verify LocalOnlyTaskTypes route to local | Request executed via FoundryBridge |
| `Enqueue_OnlineOnlyTaskType_RoutesToOnline` | Verify OnlineOnlyTaskTypes route to online | Request executed via OnlineProviderRouter |
| `Enqueue_PreferLocalModelsDefault_RoutesToLocal` | Verify local-first default behavior | Request executed locally (default) |
| `Enqueue_PreferLocalModelsDefaultFalse_RoutesToOnline` | Verify configuration to prefer online | Request executed online when configured |
| `Enqueue_NoAvailableLocalModels_RoutesToOnline` | Verify fallback when no local models | Request executed online as fallback |
| `Enqueue_LocalFirstOptions_ContainsSearchAsLocalOnly` | Verify Search is local-only by default | Assertion passes |
| `Enqueue_LocalFirstOptions_ContainsPhiModels` | Verify phi models in available list | Assertion passes |
| `Enqueue_LocalFirstOptions_DefaultIsLocalFirst` | Verify PreferLocalModelsDefault=true | Assertion passes |

### Integration Tests

**Future Scope:** Integration tests with full orchestration pipeline (TaskOrchestrator → IntentResolver → ModelQueue → routing).

**CLI Validation:** 
- `daiv3 queue status` shows routing decisions in queue metrics
- `daiv3 dashboard queue` displays which requests routed to local vs. online

## CLI Commands (Validation)

### Monitor Routing Decisions
```bash
# View current queue with routing information
daiv3 queue status

# Output:
# Total Local Executions: 45
# Total Online Executions: 3
# Current Model: phi-3-mini
# Queue Items: [ID, TaskType, Routed: Local/Online, ...]
```

### Dashboard Visibility
```bash
# View queue dashboard with routing breakdown
daiv3 dashboard queue

# Shows:
# - Top 3 queued items with routing destination
# - Local vs. Online execution split
# - Queue metrics
```

### Test Local-First Behavior
```bash
# Enqueue a search request (local-only)
daiv3 chat "Search my documents for budget reports"

# Verify in logs:
# [Information] Request {ID} (task=Search): Routing to local model (local-first default)
```

## Build and Compilation

- **Project:** `src/Daiv3.ModelExecution/`
- **Test Project:** `tests/unit/Daiv3.ModelExecution.Tests/`
- **Build Status:** ✅ Compiles without errors
- **Test Status:** ✅ 268/268 tests passing (includes 8 new local-first routing tests)
- **Warnings:** 0 (after cleanup of unused variables)

## Dependencies

- **ARCH-REQ-001:** System architecture with distinct layers (satisfied)
- **KLC-REQ-001:** ONNX Runtime for local inference (satisfied)
- **MQ-REQ-001:** Model Queue with lifecycle management (satisfied)
- **CT-REQ-003:** Transparency infrastructure (satisfied)
- **KM-REQ-001:** File system watching and document ingestion (satisfied)
- **LM-REQ-001:** LLM integration layer (satisfied)
- **AST-REQ-006:** Async/await patterns (satisfied)

## Related Requirements

- **ES-REQ-002:** Configurable online fallback (related, not blocking)
- **ES-REQ-003:** Self-contained, no external servers (related, not blocking)
- **ES-ACC-001:** Offline operation acceptance criteria (depends on this)

## Implementation Plan

- ✅ Identify owning component (ModelQueue)
- ✅ Define configuration (LocalFirstRouteOptions)
- ✅ Implement core logic (ShouldRouteOnline)
- ✅ Add integration points (DI registration, app configuration)
- ✅ Add unit tests (8 tests)
- ✅ Document usage and configuration
- ⏭️ Integration tests (future)
- ⏭️ MAUI UI for configuration (future)
