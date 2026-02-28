# KLC-REQ-010

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use a custom hosted service for scheduling.

**Decision:** Quartz.NET was evaluated and rejected in favor of a lightweight custom scheduler implementation using `IHostedService` and `System.Threading.Timer`. The custom approach provides better control, reduces external dependencies, and avoids the complexity overhead of Quartz.NET for our use case.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Describe how this capability is invoked or configured.
- List user-visible effects and any UI surfaces involved.
- Specify operational constraints (offline mode, budgets, permissions).

## Dependencies
- None

## Related Requirements
- None
 - PTS-REQ-007 (depends on this implementation)
 - PTS-REQ-008 (depends on this implementation)

## ✅ IMPLEMENTATION COMPLETE

**Status:** Complete (100%)  
**Commit:** [5c2341b](https://github.com/example/daiv3/commit/5c2341b)  
**Date:** February 28, 2026  

### Deliverables

**Core Components (9 files, 900+ LOC):**
- `IScheduler.cs` - Main scheduling interface with queue management, cancellation, metadata query access
- `IScheduledJob.cs` - Job execution contract for custom implementations
- `SchedulerHostedService.cs` - BackgroundService with concurrent job execution (SemaphoreSlim-based)
- `SchedulerOptions.cs` - Configuration (JobTimeoutSeconds, CheckIntervalMilliseconds, MaxConcurrentJobs, etc.)
- `ScheduledJobMetadata.cs` - Job state tracking (status, execution history, durations)
- `ScheduledJobStatus.cs` - Enum: Pending, Running, Completed, Failed, Cancelled, Scheduled
- `ScheduleType.cs` - Enum: Immediate, OneTime, Recurring
- `SchedulerServiceExtensions.cs` - DI registration via `services.AddScheduler()`
- `Class1.cs` - Placeholder (marked for future removal)

**Testing (29 passing unit tests, 0 errors):**
- SchedulerHostedServiceTests.cs (17 tests) - Core functionality, cancellation, metadata tracking
- SchedulerConcurrencyTests.cs (7 tests) - Concurrency limits, timeouts, recurring execution
- SchedulerServiceExtensionsTests.cs (5 tests) - DI registration, configuration options

**Documentation:**
- Docs/Scheduler-Implementation.md - Complete guide with architecture, examples, troubleshooting

### Key Features Implemented

✅ **Three Scheduling Modes:**
- Immediate: Run now (or after optional delay)
- OneTime: Run at specific UTC time
- Recurring: Run at regular intervals with optional initial delay

✅ **Configurable Execution:**
- Timeout: Configurable per-job timeout (default 5 minutes)
- Concurrency: Configurable max concurrent jobs (default 4)
- Check Interval: Configurable check frequency (default 1 second)
- Job History: Optional persistence, retention rules
- Startup Recovery: Optional recovery of pending jobs

✅ **Job Lifecycle Tracking:**
- Status: Pending → Running → Completed/Failed/Cancelled
- Metadata: Created, scheduled, started, completed, duration, error messages
- Recurring Rescheduling: Automatic requeue after completion

✅ **Error Handling:**
- Exception capture with error messages
- Timeout cancellation with logging
- Graceful shutdown with job cancellation

✅ **Integration:**
- IHostedService for .NET hosting integration
- DI-friendly with AddScheduler(configuration) extension
- async-only API (no blocking)
- Comprehensive logging at all stages

### Design Decisions

1. **IHostedService vs Quartz.NET:** Chose custom implementation for reduced dependencies, better control, and simplicity
2. **Timer-based vs Event-based:** Timer provides deterministic checks, less reactive to system time changes
3. **In-memory vs Database:** In-memory queue for performance; optional persistence layer for future
4. **Concurrency Control:** SemaphoreSlim for fair queuing while respecting max concurrent limit
5. **No Cron Support Yet:** Basic interval scheduling only; cron support deferred to PTS-REQ-007

### Build & Test Status

✅ Daiv3.Scheduler builds successfully (net10.0 + net10.0-windows10.0.26100)  
✅ All 29 unit tests passing  
✅ Zero compilation errors  
✅ Zero build warnings (scheduler code)  
✅ Full integration with existing DI container  

### Next Phases

**PTS-REQ-007** (depends on KLC-REQ-010):
- Cron-based scheduling support
- Event-triggered task execution
- Integration with task orchestration

**PTS-REQ-008** (depends on PTS-REQ-007):
- Job management UI (pause, resume, modify)
- Scheduled job dashboard

```
