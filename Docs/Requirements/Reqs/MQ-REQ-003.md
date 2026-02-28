# MQ-REQ-003

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The queue manager SHALL execute P0 requests immediately, even if a model switch is required.

## Status

**✅ COMPLETE** - Full preemption support implemented and tested. See [MQ-REQ-003 Implementation Documentation](MQ-REQ-003-Implementation.md) for comprehensive design and usage details.

### Implementation Summary
- **28 unit tests passing** - Including 5 new preemption-specific tests
- **P0 preempts P1/P2** - Cancellation + requeue mechanism working correctly
- **Model switching** - P0 forces immediate model switch even during execution
- **No P0-to-P0 preemption** - Maintains FIFO ordering for same-priority requests
- **Ready for orchestration integration** - Fully implemented interface

### Key Artifacts
- **Implementation:** [ModelQueue.cs](../../src/Daiv3.ModelExecution/ModelQueue.cs) - Enhanced with preemption support
- **Tests:** [ModelQueueTests.cs](../../tests/unit/Daiv3.UnitTests/ModelExecution/ModelQueueTests.cs) - 28/28 passing
- **Documentation:** [MQ-REQ-003-Implementation.md](MQ-REQ-003-Implementation.md) - Complete implementation guide

## Implementation Plan

### ✅ Phase 1: Preemption Infrastructure (COMPLETE)
- [x] Add execution state tracking (_currentlyExecuting, _currentExecutionCts)
- [x] Add thread-safe state access (_executionStateLock)
- [x] Create CancellationTokenSource per execution
- [x] Track currently executing request and priority

### ✅ Phase 2: Preemption Logic (COMPLETE)
- [x] Check for preemptable requests when P0 arrives
- [x] Cancel in-progress P1/P2 requests via CancellationToken
- [x] Prevent P0-to-P0 preemption (FIFO ordering)
- [x] Add structured logging for preemption events

### ✅ Phase 3: Graceful Cancellation Handling (COMPLETE)
- [x] Catch OperationCanceledException in ExecuteRequestAsync
- [x] Distinguish preemption (requeue) vs user cancellation (fail)
- [x] Requeue preempted requests to appropriate channel
- [x] Clean up state in finally block

### ✅ Phase 4: Cancellation Token Propagation (COMPLETE)
- [x] Pass CancellationToken to IFoundryBridge.ExecuteAsync
- [x] Pass CancellationToken to IOnlineProviderRouter.ExecuteAsync
- [x] Ensure downstream components honor cancellation

### ✅ Phase 5: Testing & Verification (COMPLETE)
- [x] Test: P0 preempts P1 with requeue
- [x] Test: P0 preempts P2 with requeue  
- [x] Test: P0 does NOT preempt P0
- [x] Test: P0 with model switch
- [x] Verify all 28 tests pass

## Testing Plan

### ✅ Unit Tests (COMPLETE - 28/28 passing)

**Preemption Tests:**
1. ✅ `EnqueueAsync_P0PreemptsP1_P1IsRequeued` - Verifies P0 cancels P1 and P1 eventually completes
2. ✅ `EnqueueAsync_P0PreemptsP2_P2IsRequeued` - Verifies P0 cancels P2 and P2 eventually completes
3. ✅ `EnqueueAsync_P0DoesNotPreemptP0_BothComplete` - Verifies P0 does not preempt other P0 requests
4. ✅ `EnqueueAsync_P0WithModelSwitch_SwitchesImmediately` - Verifies P0 forces model switch
5. ✅ `ProcessAsync_ExecutionError_ReturnsFailedResult` - Verifies error handling remains intact

**Existing Tests (23):**
- All priority queue tests from MQ-REQ-002 continue to pass
- Enqueue, ProcessAsync, GetStatusAsync functionality verified
- Error handling and cancellation scenarios covered

### Integration Tests (Future)
- [ ] P0 interrupts real Foundry Local inference (requires SDK integration)
- [ ] P0 interrupts online provider calls (requires provider integration)
- [ ] Measure preemption latency (<10ms target)
- [ ] Stress test: Queue thrashing under rapid P0 arrivals

### Manual Testing (Future)
- [ ] CLI: Send P0 chat message while P2 indexing runs
- [ ] MAUI: Click chat send while background analytics running
- [ ] Dashboard: Verify queue metrics show preemption events

## Usage and Operational Notes

### How Preemption Works

1. **User Action Triggers P0:** User sends chat message, clicks button, or issues CLI command
2. **ModelQueue Checks Execution State:** If P1/P2 request is currently executing, cancel it
3. **Cancellation Signal Sent:** CancellationToken.Cancel() interrupts inference
4. **P0 Executes Immediately:** Even if model switch required
5. **Preempted Request Requeued:** Returns to appropriate priority queue for retry
6. **Background Processing Continues:** Eventually processes the requeued request

### User-Visible Effects

- **Chat responsiveness:** Chat messages never wait for background tasks
- **No lost work:** Preempted requests retry automatically (transparent to user)
- **Model switching:** Users see prompt responses even when model must switch
- **Progress indicators:** UI may show cancelled/retrying states (future enhancement)

### Configuration

Uses ModelQueueOptions from MQ-REQ-002 (no new settings required):
- `DefaultModelId`, `ChatModelId`, `CodeModelId`, `SummarizeModelId`
- `MaxConcurrentOnlineRequests`, `RequestTimeoutSeconds`

### Operational Constraints

1. **Preemption Cost:** Preempted requests restart from scratch (no checkpoint/resume)
2. **P0 Serialization:** Multiple P0 requests execute serially (FIFO order)
3. **Retry Overhead:** Preempted requests incur full re-execution cost
4. **Online Provider Limits:** Some providers may not honor cancellation immediately

## Dependencies
- **MQ-REQ-002** - Three priority levels (Immediate, Normal, Background) ✅
- **KLC-REQ-005** - Foundry Local SDK integration (IFoundryBridge) ✅
- **KLC-REQ-006** - Online provider integration (IOnlineProviderRouter) ✅

## Related Requirements
- **MQ-REQ-004** - P1 batching logic (not affected by preemption)
- **MQ-REQ-005** - P2 draining logic (not affected by preemption)  
- **MQ-ACC-001** - Acceptance: P0 preempts P1/P2 ✅ VERIFIED
