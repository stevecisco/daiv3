# MQ-REQ-005

**Status:** ✅ COMPLETE | **Implementation:** [MQ-REQ-005-Implementation.md](MQ-REQ-005-Implementation.md)

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
If P2 requests exist for the current model, the queue SHALL drain them before switching.

## Rationale
Background work (document indexing, scheduled tasks, reasoning pipelines) that is already queued for the current model should complete before a model switch. This extends affinity-based batching to P2 work, maximizing utilization of the current model before the expensive switch operation. P2 requests do not have user-impact latency requirements, so they can wait for model draining without degrading perceived responsiveness.

## Implementation Summary

**Implementation complete:** Model affinity batching for P2 (Background) requests now follows the same pattern as P1 batching (MQ-REQ-004).

### Key Features
- **Intelligent lookahead:** Scans up to 10 P2 requests for current model matches
- **Automatic batching:** Drains all matching P2 requests before switching models
- **Priority preservation:** P0 and P1 requests still preempt P2 work
- **Zero configuration:** Works automatically with sensible defaults

### Testing
- **5 unit tests passing** - Full coverage of batching, edge cases, and priority interactions
- **All acceptance criteria verified** - Model affinity, lookahead limits, priority preemption
- **Integration ready** - Works seamlessly with existing P0/P1 processing

### Key Artifacts
- Implementation: [ModelQueue.cs](../../../src/Daiv3.ModelExecution/ModelQueue.cs) - `SelectNextRequestAsync()` method
- Tests: [ModelQueueTests.cs](../../../tests/unit/Daiv3.UnitTests/ModelExecution/ModelQueueTests.cs) - "MQ-REQ-005 Tests" section
- Documentation: [MQ-REQ-005-Implementation.md](MQ-REQ-005-Implementation.md)

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- **MQ-REQ-004** - P1 model affinity batching (establishes pattern)
- **MQ-REQ-006** - Model switching strategy (uses P2 batching results)
