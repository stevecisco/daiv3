# PTS-REQ-007

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
The scheduler SHALL support one-time, cron-based, and event-triggered tasks.

## Implementation Status
**Status:** ✅ Complete  
**Completed:** February 28, 2026

### Implementation Summary
Implemented comprehensive scheduler support for three scheduling types:

1. **Cron-based scheduling:** Jobs can be scheduled using standard 5-field cron expressions (minute, hour, day, month, dayOfWeek)
2. **Event-triggered scheduling:** Jobs can be registered to execute when specific application events occur
3. **One-time scheduling:** Jobs can be scheduled to run at a specific time (already existed)

### Components Implemented

#### Core Scheduler Components
- **`CronExpression` class** (`Daiv3.Scheduler/CronExpression.cs`)
  - Parses standard 5-field cron expressions
  - Supports wildcards (`*`), ranges (`1-5`), lists (`1,3,5`), and steps (`*/15`, `0-30/5`)
  - Calculates next occurrence times
  - Validates cron expressions

- **`ISchedulerEvent` interface** (`Daiv3.Scheduler/ISchedulerEvent.cs`)
  - Defines event structure for event-triggered jobs
  - Includes event type, timestamp, and metadata

- **Updated `ScheduleType` enum** (`Daiv3.Scheduler/ScheduleType.cs`)
  - Added `Cron` type for cron-based scheduling
  - Added `EventTriggered` type for event-based scheduling

#### Scheduler Service Extensions
- **`IScheduler.ScheduleCronAsync()`** - Schedule jobs with cron expressions
- **`IScheduler.ScheduleOnEventAsync()`** - Register jobs to run on events
- **`IScheduler.RaiseEventAsync()`** - Trigger event-based jobs
- **`SchedulerHostedService`** updates:
  - Automatic rescheduling of cron jobs after execution
  - Event registry and dispatch system
  - Enhanced job metadata tracking

#### CLI Commands
- `schedule list [--status]` - List all scheduled jobs
- `schedule cron --name <name> --expression <cron>` - Schedule cron job
- `schedule once --name <name> --time <datetime>` - Schedule one-time job
- `schedule on-event --name <name> --event-type <type>` - Schedule event-triggered job
- `schedule cancel --id <jobid>` - Cancel a scheduled job
- `schedule info --id <jobid>` - Show detailed job information

### Testing
- **Unit tests:** 70 tests covering cron parsing, scheduling, and event triggering
  - `CronExpressionTests.cs` - 20+ tests for cron expression parsing and evaluation
  - `SchedulerHostedServiceTests.cs` - Updated with cron and event-triggered job tests
- **Test coverage:** All scheduling types, edge cases, and error conditions
- **All tests passing:** ✅

### Cron Expression Support
Supports standard 5-field format: `minute hour day month dayOfWeek`

**Examples:**
- `0 0 * * *` - Daily at midnight
- `0 12 * * 1-5` - Weekdays at noon
- `*/15 * * * *` - Every 15 minutes
- `0 9,17 * * *` - At 9 AM and 5 PM daily
- `0 0 1 * *` - First day of every month

**Field Ranges:**
- minute: 0-59
- hour: 0-23
- day: 1-31
- month: 1-12
- dayOfWeek: 0-6 (0=Sunday)

## Implementation Plan
- ✅ Identified owning component (Daiv3.Scheduler)
- ✅ Defined data contracts (CronExpression, ISchedulerEvent, ScheduleType enum)
- ✅ Implemented core logic with error handling and logging
- ✅ Added CLI integration
- ✅ Documented configuration and operational behavior

## Testing Plan
- ✅ Unit tests for cron parsing, scheduling, and event triggering
- ✅ Tests for edge cases (invalid expressions, boundary conditions, etc.)
- ✅ Negative tests for failure modes and error messages
- ✅ Manual verification via CLI commands

## Usage and Operational Notes

### Cron-based Scheduling
```bash
# Schedule a job to run daily at midnight
daiv3-cli schedule cron --name "daily-backup" --expression "0 0 * * *"

# Schedule a job to run every 15 minutes
daiv3-cli schedule cron --name "frequent-check" --expression "*/15 * * * *"

# Schedule a job for weekdays at noon
daiv3-cli schedule cron --name "weekday-report" --expression "0 12 * * 1-5"
```

### Event-triggered Scheduling
```bash
# Register a job to run when a specific event occurs
daiv3-cli schedule on-event --name "file-processor" --event-type "filesystem.file_created"

# In application code, raise the event:
await scheduler.RaiseEventAsync(new SchedulerEvent
{
    EventType = "filesystem.file_created",
    OccurredAtUtc = DateTime.UtcNow,
    Metadata = new Dictionary<string, object> { { "filepath", "/path/to/file" } }
});
```

### Job Management
```bash
# List all scheduled jobs
daiv3-cli schedule list

# Filter by status
daiv3-cli schedule list --status pending

# Get detailed job information
daiv3-cli schedule info --id job_20260228120000_000001

# Cancel a job
daiv3-cli schedule cancel --id job_20260228120000_000001
```

### Operational Constraints
- Cron jobs are automatically rescheduled after each execution
- Event-triggered jobs remain pending and can execute multiple times
- Jobs respect the configured concurrency limits
- All times are in UTC
- Job execution timeout: 300 seconds (configurable)
- Maximum concurrent jobs: 5 (configurable)

## Dependencies
- KLC-REQ-010 (Basic scheduler implementation)

## Related Requirements
- PTS-REQ-008 (Schedule management UI)
- PTS-ACC-002 (Pause/resume scheduled tasks)
- WFC-REQ-008 (Scheduled refetch intervals)

## Future Enhancements
- Persistence of scheduled jobs to database for restart recovery
- Advanced cron features (e.g., last day of month, nth weekday)
- Job retry policies with exponential backoff
- Job execution history and audit logging
- Schedule conflicts detection
- Time zone support for cron expressions
