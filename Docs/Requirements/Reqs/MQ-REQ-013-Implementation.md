# MQ-REQ-013 Implementation Documentation

## Summary

**Requirement:** The system SHALL queue online tasks when offline and mark them as pending.

**Status:** ✅ COMPLETE  
**Progress:** 100%

---

## Implementation

### Owning Components
- `OnlineProviderRouter` in `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`
- `NetworkConnectivityService` in `src/Daiv3.ModelExecution/NetworkConnectivityService.cs`
- `ModelQueueRepository` in `src/Daiv3.Persistence/Repositories/ModelQueueRepository.cs`

### Core Behavior Added

#### 1. Network Connectivity Detection
Created `INetworkConnectivityService` and `NetworkConnectivityService`:
- `IsOnlineAsync()`: Checks network availability via `NetworkInterface.GetIsNetworkAvailable()`
- `IsEndpointReachableAsync(endpoint)`: Validates endpoint reachability via HTTP HEAD request with 5-second timeout
- Automatically adds HTTPS prefix if not provided
- Registered as singleton with IHttpClientFactory support

#### 2. Persistent Queue Storage
Created `IModelQueueRepository` and `ModelQueueRepository`:
- `SavePendingRequestAsync()`: Persists offline requests to `model_queue` SQLite table with JSON serialization
- `GetPendingRequestsAsync()`: Retrieves queued requests ordered by priority and timestamp
- `UpdateRequestStatusAsync()`: Updates request status during retry processing
- `DeleteRequestAsync()`: Removes completed/failed requests from queue
- `GetPendingCountAsync()`: Returns count of pending requests

#### 3. Offline Queueing Logic
Enhanced `OnlineProviderRouter.ExecuteAsync()`:
1. Check online status via `INetworkConnectivityService.IsOnlineAsync()`
2. If offline:
   - Save request to persistent queue via `IModelQueueRepository.SavePendingRequestAsync()`
   - Return `ExecutionResult` with `ExecutionStatus.Pending` status
   - Include descriptive message: "Request queued - system is offline"
3. If online:
   - Proceed with normal provider routing and execution

#### 4. Retry Mechanism
Added `OnlineProviderRouter.RetryPendingRequestsAsync()`:
1. Retrieve all pending requests from queue
2. For each request:
   - Re-validate online status
   - If still offline, stop retry process
   - If online, execute request via normal provider routing
   - Update queue status to Completed or Failed based on outcome
   - Delete from queue after processing

### ExecutionStatus Enhancement
Added `Pending` value to `ExecutionStatus` enum:
```csharp
public enum ExecutionStatus
{
    Pending,     // New: for offline-queued requests
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
```

### Database Schema
Added `model_queue` table via `SchemaScripts.cs`:
```sql
CREATE TABLE IF NOT EXISTS model_queue (
    request_id TEXT PRIMARY KEY,
    model_id TEXT NOT NULL,
    priority INTEGER NOT NULL,
    status INTEGER NOT NULL,
    timestamp TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    metadata_json TEXT,
    error_message TEXT
)
```

### Compliance Mapping
- ✅ Detects offline status via network interface check
- ✅ Queues requests to persistent SQLite storage
- ✅ Marks requests with `ExecutionStatus.Pending` status
- ✅ Returns descriptive execution result when offline
- ✅ Provides retry mechanism to process pending requests when connectivity restored

---

## Testing

### Unit Tests Created

#### NetworkConnectivityServiceTests.cs (6 tests)
- `IsOnlineAsync_ReturnsBoolean`: Validates network detection returns bool
- `IsEndpointReachableAsync_ThrowsArgumentNullException_WhenEndpointIsNull`: Validates null parameter guard
- `IsEndpointReachableAsync_ThrowsArgumentException_WhenEndpointIsEmpty`: Validates empty parameter guard
- `IsEndpointReachableAsync_ReturnsFalse_OnTimeout`: Validates timeout handling with non-routable IP
- `IsEndpointReachableAsync_AddsHttpsPrefix_WhenNotProvided`: Validates automatic HTTPS prefix addition
- `IsEndpointReachableAsync_SupportsHttpsEndpoints`: Validates HTTPS endpoint support

#### OnlineProviderRouterOfflineQueueingTests.cs (8 tests)
- `ExecuteAsync_Online_ExecutesNormally`: Validates normal execution when online
- `ExecuteAsync_Online_NeverCallsRepository`: Confirms repository not used when online
- `ExecuteAsync_Offline_QueuesPendingRequest`: Validates queueing when offline
- `ExecuteAsync_Offline_ReturnsPendingStatus`: Confirms Pending status returned
- `RetryPendingRequestsAsync_NoRequests_DoesNothing`: Validates empty queue handling
- `RetryPendingRequestsAsync_HasRequests_StillOffline_DoesNotProcess`: Confirms no processing when still offline
- `RetryPendingRequestsAsync_HasRequests_Online_ProcessesAll`: Validates successful retry when online
- `RetryPendingRequestsAsync_HasRequests_Online_DeletesAfterProcessing`: Confirms queue cleanup after retry

### Validation Result
- ✅ All 14 tests passed (6 NetworkConnectivity + 8 OnlineProviderRouter)
- ✅ Tests run across both net10.0 and net10.0-windows10.0.26100 frameworks
- ✅ Total test runs: 26 (14 tests × 2 frameworks)
- ✅ **Test summary: total: 26, failed: 0, succeeded: 26, skipped: 0, duration: 8.3s**

---

## Dependency Injection

### ModelExecutionServiceExtensions.cs
```csharp
services.AddHttpClient("ConnectivityCheck");
services.AddSingleton<INetworkConnectivityService, NetworkConnectivityService>();
```

### PersistenceServiceExtensions.cs
```csharp
services.AddScoped<IModelQueueRepository, ModelQueueRepository>();
```

Note: `IModelQueueRepository` interface resides in `Daiv3.ModelExecution` to avoid circular dependency, implementation in `Daiv3.Persistence`.

---

## Design Decisions

### 1. Network Detection Strategy
- Primary check: `NetworkInterface.GetIsNetworkAvailable()` for fast local detection
- Secondary check: `IsEndpointReachableAsync()` available for specific endpoint validation
- Timeout: 5 seconds for HTTP reachability checks to prevent long hangs

### 2. Queue Persistence
- SQLite-based persistent queue ensures requests survive application restarts
- JSON serialization for `ExecutionRequest` payload maintains full request state
- Priority and timestamp ordering ensures FIFO within priority levels

### 3. Retry Mechanism
- Manual retry via `RetryPendingRequestsAsync()` provides explicit control
- Stops immediately if connectivity still unavailable (fail-fast behavior)
- Deletes requests after processing to prevent queue buildup

### 4. Optional Dependencies
- `INetworkConnectivityService` is optional in `OnlineProviderRouter` constructor
- `IModelQueueRepository` is optional in `OnlineProviderRouter` constructor
- If not provided, offline detection and queueing are skipped (graceful degradation)

---

## Related Requirements
- **MQ-REQ-014**: User confirmation based on configurable rules (next implementation phase)

---

## Notes

- The offline queueing is transparent to calling code - callers receive `ExecutionResult` with `Pending` status
- Retry mechanism requires explicit invocation (not automatic background retry)
- Future enhancement: Add automatic retry on connectivity restore event
- Future enhancement: Add maximum queue size limits and queue expiration policies
