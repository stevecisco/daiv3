# MQ-REQ-007 Implementation Documentation

## Summary

**Requirement:** The model switch SHALL unload the current model and load the target model before execution.

**Status:** ✅ COMPLETE  
**Progress:** 100%

---

## Implementation

### Owning Component
- `ModelQueue` in `src/Daiv3.ModelExecution/ModelQueue.cs`

### Core Behavior Added
- Local execution path now uses `IModelLifecycleManager` before calling `IFoundryBridge.ExecuteAsync(...)`.
- For each local request:
  1. Resolve target model from request type.
  2. Read currently loaded model via `GetLoadedModelAsync()`.
  3. If model differs, call `SwitchModelAsync(targetModelId, ct)`.
  4. Update last-switch tracking using lifecycle manager timestamp.
  5. Execute request on the target model.

### Compliance Mapping
- `SwitchModelAsync()` encapsulates unload-then-load semantics in the lifecycle manager, satisfying explicit pre-execution switch behavior.

---

## Testing

### Unit Tests
- Added in `tests/unit/Daiv3.UnitTests/ModelExecution/ModelQueueTests.cs`:
  - `ExecuteRequestAsync_LocalRequest_SwitchesModelBeforeExecution`
  - `ExecuteRequestAsync_LocalRequest_SameModel_DoesNotSwitch`

### Validation Result
- Targeted test file run passed: **56 passed, 0 failed**.

---

## Notes

- Existing P0/P1/P2 queue semantics are preserved.
- The switch decision is now explicit and observable at queue execution time.
- Benchmark stability and threshold methodology reference: `Docs/Performance/CPU-Performance-Expectations.md` (see **Benchmark Methodology Note (Threshold Stability)**).
