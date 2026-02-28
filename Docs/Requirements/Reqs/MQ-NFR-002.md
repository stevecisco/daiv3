# MQ-NFR-002

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
Model switching SHOULD be minimized under steady workloads.

## Implementation Plan
- ✅ Extended queue scheduling to select the dominant model for P2 background workloads when the current model has no pending P2 matches.
- ✅ Added short coalescing window knob `ModelQueueOptions.DominantP2SelectionWindowMs` (default `20ms`) before dominant P2 selection to better capture steady-state bursts.
- ✅ Added switch telemetry counter `QueueMetrics.TotalModelSwitches` to make switch minimization measurable.
- ✅ Preserved existing priority and preemption behavior while improving switch efficiency for sustained mixed-model background traffic.

## Testing Plan
- ✅ Added unit test `P2Requests_NoRequestsForCurrentModel_SelectsModelWithMostPendingP2Work`.
- ✅ Added unit test `GetMetricsAsync_LocalModelSwitches_AreTracked`.
- ✅ Re-ran full `ModelQueueTests` suite after changes.
- ✅ Result: **66 passed, 0 failed** (targeted test file).

## Usage and Operational Notes
- Configure `DominantP2SelectionWindowMs` in `ModelQueue` settings to tune switch minimization vs immediate dispatch latency.
- Read `IModelQueue.GetMetricsAsync()` to monitor `TotalModelSwitches` and track switching behavior over time.
- No UI changes are required; optimization is internal to queue scheduling.

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None

## Implementation Status
✅ **COMPLETE** - Queue scheduling now minimizes model switches more aggressively for steady background workloads and exposes switch-count telemetry.

See [MQ-NFR-002-Implementation.md](MQ-NFR-002-Implementation.md) for detailed notes.
