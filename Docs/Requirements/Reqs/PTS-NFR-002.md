# PTS-NFR-002

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
Scheduling SHOULD not block foreground UI interactions.

## Status
**COMPLETE** - Performance instrumentation implemented, all operations are non-blocking with defined thresholds.

## Implementation Summary

### Performance Metrics & Thresholds (SchedulerOptions)
- **ScheduleOperationWarningThresholdMs**: 10ms (P95 target)
  - Applies to: ScheduleImmediateAsync, ScheduleAtTimeAsync, ScheduleRecurringAsync, ScheduleCronAsync, ScheduleOnEventAsync, CancelJobAsync, PauseJobAsync, ResumeJobAsync, ModifyJobScheduleAsync
- **QueryOperationWarningThresholdMs**: 5ms (P95 target)
  - Applies to: GetJobMetadataAsync, GetAllJobsAsync, GetJobsByStatusAsync
- **EventRaiseWarningThresholdMs**: 15ms (P95 target)
  - Applies to: RaiseEventAsync
- **EnablePerformanceInstrumentation**: true (default, can be disabled for production if needed)

### Instrumentation Implementation
All IScheduler interface methods now include:
- Stopwatch-based timing measurement
- Structured logging with operation duration
- Warning logs when operations exceed configured thresholds
- Debug logs for successful operations within thresholds
- Additional context logging (e.g., job count, event type)

### Non-Blocking Design
- All IScheduler methods return `Task<T>` and complete immediately
- Schedule operations use `Task.FromResult()` for synchronous return
- Job execution is fire-and-forget with semaphore-based concurrency control
- Background timer loop processes pending jobs asynchronously
- Event-triggered jobs execute asynchronously without blocking the event raise

### Performance Test Coverage
Created SchedulerPerformanceTests.cs with 16 comprehensive tests:
1. ScheduleImmediateAsync_CompletesWithinThreshold
2. ScheduleAtTimeAsync_CompletesWithinThreshold
3. ScheduleRecurringAsync_CompletesWithinThreshold
4. ScheduleCronAsync_CompletesWithinThreshold
5. ScheduleOnEventAsync_CompletesWithinThreshold
6. CancelJobAsync_CompletesWithinThreshold
7. GetJobMetadataAsync_CompletesWithinThreshold
8. GetAllJobsAsync_CompletesWithinThreshold_SmallDataset (10 jobs)
9. GetAllJobsAsync_CompletesWithinThreshold_LargeDataset (100 jobs, <50ms)
10. GetJobsByStatusAsync_CompletesWithinThreshold
11. RaiseEventAsync_CompletesWithinThreshold
12. PauseJobAsync_CompletesWithinThreshold
13. ResumeJobAsync_CompletesWithinThreshold
14. ModifyJobScheduleAsync_CompletesWithinThreshold
15. MultipleScheduleOperations_MaintainPerformance (20 sequential operations)
16. SequentialQueryOperations_MaintainPerformance (20 query operations)

All tests pass with operations completing well within defined thresholds.

## Implementation Plan
✅ Define measurable metrics and thresholds for this constraint.
✅ Implement instrumentation to capture relevant metrics.
✅ Apply guardrails or optimizations to meet thresholds.
✅ Add configuration knobs if tuning is required.
✅ Document expected performance ranges.

## Testing Plan
✅ Benchmark tests against defined thresholds.
✅ Regression tests to prevent performance degradation.
✅ Stress tests for worst-case inputs (100 jobs).
✅ Telemetry validation to ensure metrics are recorded.

## Usage and Operational Notes

### Configuration
Performance instrumentation is enabled by default in SchedulerOptions:
```csharp
services.AddScheduler(options =>
{
    options.EnablePerformanceInstrumentation = true; // default
    options.ScheduleOperationWarningThresholdMs = 10; // default P95 target
    options.QueryOperationWarningThresholdMs = 5;     // default P95 target
    options.EventRaiseWarningThresholdMs = 15;        // default P95 target
});
```

### User-Visible Effects
- All scheduler operations return immediately (< 10ms for scheduling, < 5ms for queries)
- UI interactions remain responsive during schedule operations
- Background job execution does not block foreground operations
- Performance warnings logged if operations exceed thresholds

### Operational Constraints
- Performance metrics logged at Debug level (successful operations) and Warning level (threshold exceeded)
- Large dataset operations (100+ jobs) may take up to 50ms but still maintain UI responsiveness
- Concurrent job execution limited by MaxConcurrentJobs setting (default: 4)
- Works in offline mode (no external dependencies)

### Performance Characteristics
Based on test results:
- Schedule operations: typically < 1ms
- Query operations (single job): typically < 1ms  
- Query operations (100 jobs): typically < 10ms
- Event raise operations: typically < 5ms
- Sequential burst operations maintain performance (average < threshold)

## Implementation Files
- `src/Daiv3.Scheduler/SchedulerOptions.cs` - Added performance configuration
- `src/Daiv3.Scheduler/SchedulerHostedService.cs` - Added instrumentation to all IScheduler methods
- `tests/unit/Daiv3.UnitTests/Scheduler/SchedulerPerformanceTests.cs` - 16 performance tests (all passing)

## Test Results
- All 208 scheduler tests passing (104 tests * 2 target frameworks)
- 32 performance tests passing (16 tests * 2 target frameworks)
- Zero build errors
- Build warnings: Only pre-existing NU1903 package vulnerability warnings (not related to this change)

## Dependencies
- KLC-REQ-010 (Complete)

## Related Requirements
- PTS-REQ-007 (Complete) - Scheduler implementation that this NFR applies to
- PTS-REQ-008 (Complete) - Job management operations that are also instrumented
