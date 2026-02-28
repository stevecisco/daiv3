# MQ-REQ-016

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL execute online tasks concurrently across different providers.

## Implementation Plan
- ✅ Extended `IOnlineProviderRouter` with `ExecuteBatchAsync(IReadOnlyList<ExecutionRequest>, CancellationToken)` for explicit batch execution.
- ✅ Implemented `OnlineProviderRouter.ExecuteBatchAsync(...)` with configuration-driven behavior:
	- Parallel dispatch using `Task.WhenAll(...)` when `AllowParallelProviderExecution = true`
	- Sequential execution in input order when `AllowParallelProviderExecution = false`
- ✅ Added provider-specific `SemaphoreSlim` limiters in `OnlineProviderRouter` to enforce per-provider concurrency limits while allowing cross-provider parallelism.
- ✅ Refactored execution path to shared helper (`ExecuteThroughProviderAsync`) used by both `ExecuteAsync(...)` and `ExecuteWithConfirmationAsync(...)`.
- ✅ Added thread safety for token usage reads/updates via lock-protected access.

## Testing Plan
- ✅ Added unit tests in `OnlineProviderRouterParallelExecutionTests.cs`:
	- Null request list throws `ArgumentNullException`
	- Empty request list returns empty result
	- Parallel mode executes faster for requests routed to different providers
	- Sequential mode executes requests in non-parallel timing window
- ✅ Ran targeted OnlineProviderRouter test suite:
	- `OnlineProviderRouterTests.cs`
	- `OnlineProviderRouterSmartRoutingTests.cs`
	- `OnlineProviderRouterOfflineQueueingTests.cs`
	- `OnlineProviderRouterConfirmationTests.cs`
	- `OnlineProviderRouterContextMinimizationTests.cs`
	- `OnlineProviderRouterParallelExecutionTests.cs`
- ✅ Result: **132 passed, 0 failed**

## Usage and Operational Notes
**How to invoke:**
- Use `IOnlineProviderRouter.ExecuteBatchAsync(...)` to submit multiple online requests as a batch.

**Configuration:**
- `TaskToModelMappingConfiguration.AllowParallelProviderExecution` controls batch dispatch mode.
	- `true` (default): batch requests are dispatched concurrently.
	- `false`: batch requests run sequentially.
- `TaskToModelMappingConfiguration.MaxConcurrentRequestsPerProvider` bounds in-flight requests per provider.

**Operational behavior:**
- Requests routed to different providers can execute concurrently.
- Provider-level limits still apply to prevent overloading any single provider.
- Existing confirmation, offline queueing, and context minimization behaviors remain unchanged.

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- MQ-REQ-012 (provider routing)
- MQ-REQ-017 (per-provider rate limiting)

## Implementation Status
✅ **COMPLETE** - Online provider router now supports explicit batch execution with concurrent dispatch across providers and provider-scoped concurrency control.

See [MQ-REQ-016-Implementation.md](MQ-REQ-016-Implementation.md) for detailed implementation notes.
