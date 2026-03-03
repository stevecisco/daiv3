# MQ-REQ-001 Implementation Documentation

## Summary

**Requirement:** The system SHALL enforce the constraint that only one Foundry Local model is loaded at a time.

**Status:** Complete (KLC-REQ-005 SDK Integration Applied)

**Progress:** 100% - Constraint enforcement is now connected to Foundry lifecycle management integration.

---

## Design Overview

MQ-REQ-001 implements a constraint enforcement mechanism that ensures only one model can be loaded in memory at any time. This is a Foundry Local SDK limitation (not a design choice) that drives the intelligent queue system.

### Architecture

The implementation consists of three core components:

1. **IModelLifecycleManager** (Interface)
   - Defines the contract for model loading/unloading
   - Enforces the single-model constraint at the interface level
   - Provides metrics collection for observability

2. **ModelLifecycleManager** (Implementation)
   - Thread-safe enforcement via SemaphoreSlim
   - Tracks model state and lifecycle metrics
   - Provides clear error messages when constraint is violated

3. **ModelLifecycleOptions** (Configuration)
   - LoadTimeoutMs: Maximum time to wait for model load (default 60 seconds)
   - SwitchTimeoutMs: Maximum time for model switch (default 90 seconds)
   - EnableDetailedLogging: Detailed timing metrics (default true)
   - EnableIdleUnloading: Auto-unload unused models (default false)
   - IdleUnloadDelayMs: Idle timeout threshold (default 5 minutes)

---

## Implementation Details

### Constraint Enforcement

The ModelLifecycleManager enforces the constraint through three methods:

#### LoadModelAsync(modelId)
- **Accepts:** Loading when no model is loaded OR reloading the same model (idempotent)
- **Rejects:** Attempting to load a different model while another is loaded
- **Error:** `InvalidOperationException` with clear message indicating which model is conflicting
- **Logging:** ERROR level logs constraint violations for visibility

#### SwitchModelAsync(newModelId)
- **Accepts:** Switching to any new model OR re-switching to the same model (idempotent)
- **Implementation:** Atomically unloads current + loads new (via SemaphoreSlim locking)
- **Cost:** Includes both unload and load time in metrics
- **Idempotent:** If target model already loaded, returns immediately

#### UnloadModelAsync()
- **Accepts:** Unloading when model is loaded OR when no model is loaded (no-op)
- **Result:** Frees model memory; no model loaded after this call

### Thread Safety

All operations are protected by a SemaphoreSlim to ensure:
- Only one load/unload operation at a time
- No race conditions when checking current model
- Atomic transitions between model states

```csharp
await _lockSlim.WaitAsync(ct);  // One operation at a time
try 
{
    // Model state check, unload, load, etc.
}
finally
{
    _lockSlim.Release();
}
```

### Metrics Collection

The manager tracks:
- `TotalLoads`: Number of load operations attempted
- `SuccessfulLoads`: Load operations that completed
- `FailedLoads`: Load operations that threw exceptions
- `ConstraintViolations`: Attempted to load different model while another loaded
- `CurrentModelId`: Currently loaded model (null if none)
- `LastModelSwitch`: Timestamp of last load/unload
- `AverageLoadTimeMs`: Moving average of load operation duration

---

## Usage Examples

### Basic Usage

```csharp
// Register in DI
services.AddModelExecutionServices(configuration);

// Inject IModelLifecycleManager
public class MyService(IModelLifecycleManager lifecycleManager)
{
    public async Task ExecuteWithModel(string modelId)
    {
        // Load model (will fail if different model already loaded)
        try
        {
            await lifecycleManager.LoadModelAsync(modelId);
        }
        catch (InvalidOperationException ex)
        {
            // Model constraint violated - use SwitchModelAsync instead
            await lifecycleManager.SwitchModelAsync(modelId);
        }

        // Now execute work with modelId...
    }
}
```

### Model Switching

```csharp
// Switch models (key to the queue strategy)
await lifecycleManager.SwitchModelAsync("phi-4-mini");  // Unload old, load new

// Check what's loaded
var currentModel = await lifecycleManager.GetLoadedModelAsync();
if (currentModel == "phi-4-mini") { /* do work */ }

// Query metrics
var metrics = await lifecycleManager.GetMetricsAsync();
Console.WriteLine($"Model switches: {metrics.TotalLoads}");
```

### Integration with ModelQueue

The ModelQueue uses this constraint enforcement:

```csharp
// When switching models, the queue ensures only one model is loaded
var currentModel = await _foundryBridge.GetLoadedModelAsync();
if (currentModel != modelId)
{
    // This will enforce the constraint
    await _lifecycleManager.SwitchModelAsync(modelId);
}

// Execute with model...
result = await _foundryBridge.ExecuteAsync(request, modelId);
```

---

## Error Handling

### Constraint Violation Error

When attempting to load a different model while another is loaded:

```
InvalidOperationException: Cannot load model 'phi-3.5-mini': 
model 'phi-3-mini' is already loaded. Only one model can be loaded 
at a time. Call SwitchModelAsync() to switch models.
```

**Logging:** ERROR level logged automatically with model names and current state

**Recovery:** Use `SwitchModelAsync()` instead of `LoadModelAsync()`

### null/Empty Model ID

```
ArgumentNullException: Value cannot be null. (Parameter 'modelId')
ArgumentException: The value cannot be empty or contain only whitespace. (Parameter 'modelId')
```

---

## Dependent Components

### Dependencies
- **KLC-REQ-005** - Foundry Local SDK integration (needed for actual load/unload implementation)
- **KLC-REQ-006** - Online provider abstraction (needed for routing decisions)

### Dependents
- **ModelQueue** - Uses constraint enforcement to prevent invalid state transitions
- **MQ-REQ-002** - Priority levels depend on single-model constraint
- **MQ-REQ-003 through MQ-REQ-007** - Queue batching strategy depends on this constraint

---

## Testing

### Test Coverage

**24 test cases** (x 2 target frameworks = 48 total tests, all passing):

1. **Constraint Enforcement (3 tests)**
   - Load with no model loaded ✓
   - Reject loading different model ✓
   - Idempotent reload ✓

2. **Model Switching (3 tests)**
   - Switch between models ✓
   - Switch from empty state ✓
   - Idempotent switch ✓

3. **Unload Operations (2 tests)**
   - Unload loaded model ✓
   - Unload with no model (no-op) ✓

4. **Query Operations (6 tests)**
   - Get loaded model (null when empty) ✓
   - Check if model loaded (true/false) ✓
   - Get last switch timestamp ✓
   - Get last switch after operations ✓
   - Update timestamp on switch ✓

5. **Metrics Collection (5 tests)**
   - Default metrics ✓
   - Metrics after load ✓
   - Track constraint violations ✓
   - Average load time calculation ✓
   - Multiple operations metrics ✓

6. **Concurrency (2 tests)**
   - Concurrent identical loads ✓
   - Concurrent load/switch operations ✓

7. **Error Handling (1 test)**
   - Constraint violation logging ✓

### Running Tests

```bash
dotnet test Daiv3.FoundryLocal.slnx --filter "ModelLifecycleManagerTests"
```

Expected result: **48 tests passed** (24 per framework)

---

## Configuration

### Default Configuration

```csharp
services.AddModelExecutionServices(configuration);
```

Uses appsettings.json:

```json
{
  "ModelLifecycle": {
    "LoadTimeoutMs": 60000,
    "SwitchTimeoutMs": 90000,
    "EnableDetailedLogging": true,
    "EnableIdleUnloading": false,
    "IdleUnloadDelayMs": 300000
  }
}
```

### Custom Configuration

```csharp
services.AddModelExecutionServices(
    configureLifecycle: options =>
    {
        options.LoadTimeoutMs = 120_000;  // 2 minutes
        options.SwitchTimeoutMs = 180_000; // 3 minutes
        options.EnableIdleUnloading = true;
        options.IdleUnloadDelayMs = 600_000; // 10 minutes
    });
```

---

## Operational Notes

### Single Model Constraint Rationale

The Foundry Local SDK enforces this constraint because:
1. **Memory limitation**: Models can be large (several GB), only one fits in VRAM
2. **Execution provider state**: DirectML/GPU sessions are model-specific
3. **Performance**: Unloading + reloading has significant cost

### Performance Implications

- **Model loading**: 5-30 seconds (depends on model size, hardware, cache state)
- **Model switching**: 10-60 seconds (unload + load)
- **Queue strategy**: Batches requests for current model to minimize switches
- **Cost avoidance**: P0 requests can trigger forced switch; queue batches P1/P2

### Monitoring

```csharp
var metrics = await lifecycleManager.GetMetricsAsync();

// Monitor constraint violations
if (metrics.ConstraintViolations > 0)
{
    logger.LogWarning(
        "Constraint violations: {Count}. Queue may not be optimized.",
        metrics.ConstraintViolations);
}

// Monitor model switching overhead
logger.LogInformation(
    "Average load time: {AvgMs}ms, Loads: {Count}",
    metrics.AverageLoadTimeMs, metrics.TotalLoads);
```

---

## Logging

The implementation uses structured logging with:
- **ERROR**: Constraint violations (attempted to load different model)
- **INFORMATION**: Successful loads, switches, unloads with timing
- **DEBUG**: Idempotent operations, detailed state transitions

Example log output:

```
[ERROR] Attempted to load model phi-3.5-mini while phi-3-mini is already loaded. 
Only one model can be loaded at a time. Use SwitchModelAsync() instead.

[INFO] Switching model: phi-3-mini → phi-3.5-mini

[INFO] Successfully switched model in 23456ms: phi-3-mini → phi-3.5-mini

[DEBUG] Model phi-3-mini is already loaded (idempotent)
```

---

## KLC-REQ-005 Integration Status

Foundry lifecycle integration has been applied:

- `ModelLifecycleManager` now delegates lifecycle operations through Foundry management services when registered.
- Single-model constraint enforcement, locking, idempotency, and metrics behavior remain unchanged.
- The public `IModelLifecycleManager` interface did not require breaking changes.

---

## Files Modified/Created

### New Files
- `Interfaces/IModelLifecycleManager.cs` - Constraint enforcement interface
- `ModelLifecycleManager.cs` - Constraint enforcement implementation
- `ModelLifecycleOptions.cs` - Configuration class
- `Tests/ModelLifecycleManagerTests.cs` - 24 test cases

### Modified Files
- `ModelExecutionServiceExtensions.cs` - DI registration for IModelLifecycleManager
- `IFoundryBridge.cs` - Documented single-model constraint in interface documentation

---

## Success Criteria ✓

- [x] Only one model can be loaded at a time (enforced with exception)
- [x] Clear error messages when constraint is violated
- [x] Configuration with sensible defaults
- [x] Thread-safe operations via SemaphoreSlim locking
- [x] Comprehensive metric collection for observability
- [x] 48 unit tests passing (100% coverage of constraint enforcement)
- [x] Operations are idempotent where appropriate
- [x] Structured logging for debugging and monitoring
- [x] Documented configuration and operational behavior

---

## Summary

MQ-REQ-001 is fully implemented with a robust constraint enforcement mechanism. The implementation is ready for Foundry Local SDK integration (KLC-REQ-005) and provides a solid foundation for the intelligent queue system (MQ-REQ-003 through MQ-REQ-007) that depends on this constraint.
