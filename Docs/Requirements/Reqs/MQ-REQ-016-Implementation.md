# MQ-REQ-016 Implementation Documentation

## Summary

**Requirement:** The system SHALL execute online tasks concurrently across different providers.

**Status:** ✅ COMPLETE  
**Progress:** 100%

**Outcome:** `OnlineProviderRouter` now supports explicit batch execution with configuration-driven parallel dispatch and provider-scoped concurrency controls.

---

## Implementation

### Owning Component
- `OnlineProviderRouter` in `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`
- Interface contract in `src/Daiv3.ModelExecution/Interfaces/IOnlineProviderRouter.cs`
- Configuration in `src/Daiv3.ModelExecution/TaskToModelMappingConfiguration.cs`

### Core Behavior Added

#### 1. New Batch Execution API
Added to `IOnlineProviderRouter`:

```csharp
Task<IReadOnlyList<ExecutionResult>> ExecuteBatchAsync(
    IReadOnlyList<ExecutionRequest> requests,
    CancellationToken ct = default);
```

Behavior:
- Accepts a list of online execution requests.
- Returns one result per request in the same order as input.
- Uses router configuration to decide parallel vs sequential dispatch.

#### 2. Parallel Dispatch Logic (MQ-REQ-016)
Implemented in `OnlineProviderRouter.ExecuteBatchAsync(...)`:

- If `AllowParallelProviderExecution = true` and request count > 1:
  - Dispatches all requests concurrently with `Task.WhenAll(...)`.
- If disabled or only one request:
  - Executes sequentially using existing `ExecuteAsync(...)` path.

This keeps existing request-level behavior intact while adding explicit batch-level concurrency support.

#### 3. Provider-Scoped Concurrency Control
Replaced single global limiter pattern with per-provider limiters:

- Added `_providerConcurrencyLimiters` keyed by provider name.
- Added `GetProviderConcurrencyLimiter(providerName)` to lazily initialize new providers.
- Execution path acquires/releases the provider-specific `SemaphoreSlim` around provider calls.

Result:
- Requests for different providers can proceed in parallel.
- Requests for the same provider respect `MaxConcurrentRequestsPerProvider`.

#### 4. Shared Execution Helper
Refactored duplicate execution logic into:

```csharp
ExecuteThroughProviderAsync(request, providerName, confirmed, ct)
```

This helper is now used by both:
- `ExecuteAsync(...)`
- `ExecuteWithConfirmationAsync(...)`

And preserves existing behavior for:
- token budget checks
- context minimization (MQ-REQ-015)
- confirmation workflow (MQ-REQ-014)
- offline queueing integration (MQ-REQ-013)

#### 5. Token Usage Thread Safety
Added lock-protected access for token usage state:
- `GetTokenUsageAsync(...)`
- `IsProviderWithinDailyBudget(...)`
- `CheckTokenBudgetAsync(...)`
- `UpdateTokenUsage(...)`

This prevents race conditions when multiple requests execute in parallel.

---

## Testing

### Unit Tests Added

**File:** `tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterParallelExecutionTests.cs`

Tests:
1. `ExecuteBatchAsync_NullRequests_ThrowsArgumentNullException`
2. `ExecuteBatchAsync_EmptyRequests_ReturnsEmptyResults`
3. `ExecuteBatchAsync_ParallelEnabled_ExecutesDifferentProvidersConcurrently`
4. `ExecuteBatchAsync_ParallelDisabled_ExecutesSequentially`

### Validation Run
Executed targeted OnlineProviderRouter tests:
- `OnlineProviderRouterTests.cs`
- `OnlineProviderRouterSmartRoutingTests.cs`
- `OnlineProviderRouterOfflineQueueingTests.cs`
- `OnlineProviderRouterConfirmationTests.cs`
- `OnlineProviderRouterContextMinimizationTests.cs`
- `OnlineProviderRouterParallelExecutionTests.cs`

**Result:** ✅ 132 passed, 0 failed

---

## Design Decisions

### 1. Explicit Batch API Instead of Implicit Global Parallelism
Added `ExecuteBatchAsync(...)` to make concurrent behavior explicit and testable while preserving existing `ExecuteAsync(...)` contract.

### 2. Per-Provider Limiters
Provider-scoped semaphores align with current and upcoming requirements:
- MQ-REQ-016: cross-provider concurrency
- MQ-REQ-017: per-provider rate/concurrency controls

### 3. Preserve Existing Behavior by Reuse
Execution internals were refactored into a shared helper so existing logic stays consistent across normal and confirmed execution paths.

### 4. Keep Provider Call Stub Intact
Provider calls remain stubbed (`Task.Delay`) until Microsoft.Extensions.AI integration is completed; this requirement focuses on routing/concurrency behavior, not provider SDK integration.

---

## Operational Notes

### Configuration
Under `TaskToModelMappingConfiguration`:
- `AllowParallelProviderExecution` (default `true`)
- `MaxConcurrentRequestsPerProvider` (default `10`)

### Invocation Pattern
Use `ExecuteBatchAsync(...)` when multiple online requests should be executed as a batch and potentially in parallel across providers.

### Current Constraints
- Concurrency behavior is validated at routing layer with provider-call stubs.
- End-to-end throughput depends on future real provider client integrations.

---

## Files Changed

### Source
- `src/Daiv3.ModelExecution/Interfaces/IOnlineProviderRouter.cs`
- `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`

### Tests
- `tests/unit/Daiv3.UnitTests/ModelExecution/OnlineProviderRouterParallelExecutionTests.cs`

### Documentation
- `Docs/Requirements/Reqs/MQ-REQ-016.md`
- `Docs/Requirements/Reqs/MQ-REQ-016-Implementation.md`
- `Docs/Requirements/Master-Implementation-Tracker.md`
