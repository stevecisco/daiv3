# PTS-REQ-008

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
Users SHALL be able to view, pause, and modify scheduled jobs.

## Implementation Status
**Status:** ✅ Complete  
**Completed:** February 28, 2026

### Implementation Summary
Implemented comprehensive job management functionality allowing users to:
1. **Pause jobs:** Prevent scheduled jobs from executing without cancelling them
2. **Resume jobs:** Reactivate paused jobs to continue their schedule
3. **Modify jobs:** Change scheduling parameters (time, interval, cron expression, event type)

### Components Implemented

#### Core Scheduler Components
- **Added `Paused` status** to `ScheduledJobStatus` enum
  - Jobs in paused state will not execute
  - Paused jobs can be resumed to continue their schedule

- **`ScheduleModificationRequest` class** (`Daiv3.Scheduler/ScheduleModificationRequest.cs`)
  - Data contract for modifying job schedules
  - Supports modifying: time (one-time), interval (recurring), cron expression, event type

#### IScheduler Interface Extensions
- **`PauseJobAsync()`** - Pause a scheduled job
  - Returns false if job is running, completed, or cancelled
  - Paused jobs skip execution in CheckAndExecutePendingJobs
  - Paused event-triggered jobs skip execution on event raises

- **`ResumeJobAsync()`** - Resume a paused job
  - Returns false if job is not paused
  - Automatically sets appropriate status based on job type and schedule

- **`ModifyJobScheduleAsync()`** - Modify job scheduling parameters
  - Validates modifications based on job's ScheduleType
  - Returns false if job is running, completed, or cancelled
  - For cron jobs: validates new cron expression and calculates next run
  - For event-triggered jobs: updates event registry
  - For recurring jobs: reschedules based on new interval
  - For one-time jobs: updates scheduled time and status

#### Internal Implementation Updates
- Changed `ScheduledJobEntry` properties from `init` to `set`:
  - `IntervalSeconds`, `CronExpression`, `EventType` now mutable
  - Enables runtime modification of job schedules

- Updated `CheckAndExecutePendingJobs()` to skip paused jobs
- Updated `RaiseEventAsync()` to skip paused event-triggered jobs

#### CLI Commands
- `schedule pause --id <jobid>` - Pause a scheduled job
- `schedule resume --id <jobid>` - Resume a paused job
- `schedule modify --id <jobid>` - Modify job schedule parameters:
  - `--time <datetime>` - New time for one-time jobs
  - `--interval <seconds>` - New interval for recurring jobs
  - `--cron <expression>` - New cron expression for cron jobs
  - `--event-type <type>` - New event type for event-triggered jobs

### Testing
- **Unit tests:** 20 new tests added (102 total passing)
  - `PauseJobAsync_WithValidJobId_PausesJob`
  - `PauseJobAsync_WithRunningJob_ReturnsFalse`
  - `PauseJobAsync_WithCompletedJob_ReturnsFalse`
  - `PauseJobAsync_AlreadyPaused_ReturnsTrue`
  - `PausedJob_DoesNotExecute`
  - `ResumeJobAsync_WithPausedJob_ResumesJob`
  - `ResumeJobAsync_WithNonPausedJob_ReturnsFalse`
  - `ResumedJob_ExecutesCorrectly`
  - `ModifyJobScheduleAsync_OneTimeJob_UpdatesScheduledTime`
  - `ModifyJobScheduleAsync_RecurringJob_UpdatesInterval`
  - `ModifyJobScheduleAsync_CronJob_UpdatesCronExpression`
  - `ModifyJobScheduleAsync_EventTriggeredJob_UpdatesEventType`
  - `ModifyJobScheduleAsync_WithRunningJob_ReturnsFalse`
  - `ModifyJobScheduleAsync_WithInvalidCronExpression_ThrowsException`
  - `ModifyJobScheduleAsync_WithInvalidJobId_ReturnsFalse`
  - `PauseAndModify_WorksTogether`
  - `PausedEventTriggeredJob_DoesNotExecuteOnEvent`
  - `GetJobsByStatusAsync_FiltersPausedJobs`
- **Test coverage:** All pause/resume/modify scenarios, edge cases, and error conditions
- **All tests passing:** ✅ 102/102

## Implementation Plan
- ✅ Identified owning component (Daiv3.Scheduler)
- ✅ Defined data contracts (Paused status, ScheduleModificationRequest)
- ✅ Implemented core logic with error handling and logging
- ✅ Added CLI integration (pause, resume, modify commands)
- ✅ Documented configuration and operational behavior

## Testing Plan
- ✅ Unit tests for pause, resume, and modify operations
- ✅ Tests for edge cases (running jobs, completed jobs, invalid parameters)
- ✅ Tests for combined operations (pause+modify+resume)
- ✅ Negative tests for failure modes and error messages
- ✅ Manual verification via CLI commands

## Usage and Operational Notes

### Pausing Jobs
```bash
# Pause a scheduled job
daiv3-cli schedule pause --id job_20260228120000_000001

# The job will not execute while paused
# View paused jobs
daiv3-cli schedule list --status paused
```

### Resuming Jobs
```bash
# Resume a paused job
daiv3-cli schedule resume --id job_20260228120000_000001

# The job will now execute according to its schedule
```

### Modifying Job Schedules
```bash
# Modify a one-time job's scheduled time
daiv3-cli schedule modify --id job_20260228120000_000001 --time "2026-03-01T15:30:00Z"

# Modify a recurring job's interval
daiv3-cli schedule modify --id job_20260228120000_000002 --interval 300

# Modify a cron job's expression
daiv3-cli schedule modify --id job_20260228120000_000003 --cron "0 */2 * * *"

# Modify an event-triggered job's event type
daiv3-cli schedule modify --id job_20260228120000_000004 --event-type "filesystem.file_updated"
```

### Combined Workflow
```bash
# Pause a job, modify it, then resume
daiv3-cli schedule pause --id job_20260228120000_000001
daiv3-cli schedule modify --id job_20260228120000_000001 --time "2026-03-02T10:00:00Z"
daiv3-cli schedule resume --id job_20260228120000_000001
```

### Operational Constraints
- Cannot pause jobs that are:
  - Currently running
  - Already completed
  - Already cancelled
- Cannot resume jobs that are not paused
- Cannot modify jobs that are:
  - Currently running
  - Already completed
  - Already cancelled
  - Immediate-type jobs (schedule type not modifiable)
- Modification parameters must match job's schedule type:
  - OneTime jobs: require `--time`
  - Recurring jobs: require `--interval`
  - Cron jobs: require `--cron`
  - EventTriggered jobs: require `--event-type`
- All modification timestamps must be in UTC
- Cron expressions must be valid 5-field format
- Modifying event-triggered jobs updates the event registry

## Dependencies
- ✅ PTS-REQ-007 (Cron and event-triggered scheduling)

## Related Requirements
- PTS-REQ-007 (Scheduling implementation)
- PTS-ACC-002 (Pause/resume scheduled tasks - covered by this implementation)

## Future Enhancements
- Bulk pause/resume operations (pause all jobs, resume all jobs)
- Schedule modification history/audit log
- Job templates for creating new jobs based on existing ones
- Validation of modification conflicts (e.g., modifying cron to create impossible schedule)
- UI for visual job schedule modification
- Job cloning (create new job from existing job with modifications)
