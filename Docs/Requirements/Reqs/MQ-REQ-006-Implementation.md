# MQ-REQ-006 Implementation Documentation

## Summary

**Requirement:** If no requests exist for the current model, the queue SHALL select the model with the most pending P1 work.

**Status:** ✅ COMPLETE  
**Progress:** 100%

---

## Implementation

### Owning Component
- `ModelQueue` in `src/Daiv3.ModelExecution/ModelQueue.cs`

### Core Behavior Added
- Enhanced `SelectNextRequestAsync()` for P1 selection when the current model has no matching pending work.
- The queue now:
  1. Checks current model state through `IModelLifecycleManager`.
  2. Scans pending P1 requests.
  3. If no P1 request matches the current model, computes pending counts by target model.
  4. Selects the model with the highest pending P1 count.
  5. Requeues remaining requests to keep dominant-model requests near the front for batching.

### Determinism Rules
- Ties are broken by first-seen model order in the scanned queue segment.
- Selection remains deterministic for equivalent queue contents.

---

## Testing

### Unit Tests
- Added in `tests/unit/Daiv3.UnitTests/ModelExecution/ModelQueueTests.cs`:
  - `P1Requests_NoRequestsForCurrentModel_SelectsModelWithMostPendingP1Work`

### Validation Result
- Targeted test file run passed: **56 passed, 0 failed**.

---

## Notes

- This logic builds directly on MQ-REQ-004 and MQ-REQ-005 batching behavior.
- The implementation is designed to reduce unnecessary model switches under mixed P1 workloads.
- Benchmark stability and threshold methodology reference: `Docs/Performance/CPU-Performance-Expectations.md` (see **Benchmark Methodology Note (Threshold Stability)**).
