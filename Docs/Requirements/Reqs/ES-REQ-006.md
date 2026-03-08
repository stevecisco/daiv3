# ES-REQ-006

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHALL maintain a model request queue that batches tasks by model affinity.

## Implementation Status
**Status:** Complete  
**Date Completed:** 2026-03-08

## Architecture

### Core Design
ES-REQ-006 is implemented by `ModelQueue` in the Model Execution layer and is delivered through three complementary queue behaviors:

1. **P1 model-affinity batching (MQ-REQ-004):**
- Prefer pending P1 work that targets the currently loaded model.
- Scan with bounded lookahead (`maxLookahead = 10`) to avoid starvation and excessive dequeue latency.

2. **P2 model-affinity batching (MQ-REQ-005):**
- Apply the same affinity strategy to background work.
- Drain by model affinity before switching to reduce switch churn under steady workloads.

3. **Dominant model selection when no affinity match (MQ-REQ-006):**
- If no pending requests match the current model, select the model with the largest pending P1 workload.
- Equivalent dominant-model behavior is also applied to P2 to keep model switching costs low in background pipelines.

This aligns with the executive summary objective: minimize expensive model load/unload cycles while preserving priority behavior.

### Owning Components
- `src/Daiv3.ModelExecution/ModelQueue.cs`
	- `SelectNextRequestAsync()` implements P1/P2 affinity batching and dominant-model selection.
	- Priority order remains `P0 > P1 > P2`, with P0 always preemptive.

### Configuration and Defaults
Queue batching behavior is enabled by default and controlled by existing queue options:
- `DominantP1SelectionWindowMs`
- `DominantP2SelectionWindowMs`

No additional ES-specific configuration was required.

## Testing Plan

### Unit Coverage (existing MQ requirements)
- `tests/unit/Daiv3.ModelExecution.Tests/ModelQueueTests.cs`
	- P1 affinity batching tests (MQ-REQ-004)
	- P2 affinity batching tests (MQ-REQ-005)
	- Dominant model selection tests (MQ-REQ-006)

### Requirement Traceability Coverage (added for ES)
- `tests/unit/Daiv3.ModelExecution.Tests/ModelQueueExecutiveSummaryTests.cs`
	- `ES_REQ_006_NormalPriority_BatchesByModelAffinity`
	- `ES_REQ_006_WhenCurrentModelHasNoPendingRequests_SelectsDominantPendingModel`

These tests directly map ES-REQ-006 to executable validation, independent of lower-level MQ requirement docs.

### Validation Run (2026-03-08)
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **2348 total, 0 failed, 2333 passed, 15 skipped**
- `dotnet test tests/unit/Daiv3.ModelExecution.Tests/Daiv3.ModelExecution.Tests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ModelQueueExecutiveSummaryTests"`
	- Result: **2 total, 0 failed**

## Usage and Operational Notes
- Model-affinity batching is transparent to users; no UI toggle is required.
- User-visible effect: reduced model thrashing and improved throughput under mixed workloads.
- Priority semantics are preserved:
	- P0 remains immediate and can preempt lower-priority execution.
	- P1 and P2 are batched by model where possible.
- Works fully offline with local model execution.

## Dependencies
- ARCH-REQ-001
- CT-REQ-003
- KLC-REQ-001
- KM-REQ-001
- MQ-REQ-001
- LM-REQ-001
- AST-REQ-006

## Related Requirements
- MQ-REQ-004
- MQ-REQ-005
- MQ-REQ-006

