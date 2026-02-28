# MQ-NFR-001

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
Queue operations SHOULD be deterministic and observable.

## Implementation Plan
- ✅ Added queue-level observability metrics via `IModelQueue.GetMetricsAsync()`.
- ✅ Instrumented `ModelQueue` with deterministic runtime counters:
	- `TotalEnqueued`, `TotalDequeued`, `TotalCompleted`, `TotalFailed`, `TotalPreempted`
	- `TotalLocalExecutions`, `TotalOnlineExecutions`, `InFlightExecutions`
	- `AverageQueueWaitMs`, `AverageExecutionDurationMs`, `LastDequeuedAt`
- ✅ Added deterministic request sequencing metadata (`SequenceNumber`) at enqueue time for traceability.
- ✅ Added a configurable coalescing window (`ModelQueueOptions.DominantP1SelectionWindowMs`) to improve deterministic dominant-model selection under bursty P1 enqueue patterns.
- ✅ Added structured logs including request sequence IDs to improve operational diagnostics.

## Testing Plan
- ✅ Added/updated unit tests in `ModelQueueTests.cs` to validate observability:
	- `GetMetricsAsync_AfterSuccessfulExecution_ReturnsObservableCounters`
	- `GetMetricsAsync_WhenP0PreemptsP1_IncrementsPreemptionCounter`
- ✅ Re-ran full `ModelQueueTests` suite after instrumentation changes.
- ✅ Result: **66 passed, 0 failed** (targeted test file).

## Usage and Operational Notes
- Metrics are available programmatically from queue consumers through `IModelQueue.GetMetricsAsync()`.
- Queue status snapshots remain available via `GetQueueStatusAsync()` and are included in `QueueMetrics.QueueStatus`.
- Tune `DominantP1SelectionWindowMs` to balance burst coalescing determinism versus immediate dispatch latency.
- No UI changes are required; this is backend scheduling/telemetry behavior.

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None

## Implementation Status
✅ **COMPLETE** - Queue operations now expose deterministic, queryable metrics and include additional deterministic scheduling support for bursty P1 workloads.

See [MQ-NFR-001-Implementation.md](MQ-NFR-001-Implementation.md) for detailed notes.
