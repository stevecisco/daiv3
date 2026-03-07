# CT-REQ-008

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The dashboard SHALL display scheduled jobs and results.

## Status
**Complete** - Implemented 2026-03-06

## Implementation Summary

### Architecture Overview
CT-REQ-008 extends the dashboard system (CT-REQ-003) with scheduled jobs visibility via integration with the job scheduler (PTS-REQ-007).

**Key Components:**
- **DashboardService** - Collects scheduled jobs metadata via `IScheduler.GetAllJobsAsync()`
- **ScheduledJobsStatus** - Aggregates job counts by status with computed properties
- **ScheduledJobSummary** - Transforms `ScheduledJobMetadata` to UI-friendly format with description fields
- **DashboardViewModel** - MVVM properties for MAUI data binding
- **CLI Dashboard Command** - `dashboard scheduled` subcommand for text-based job visualization

### Data Models

#### ScheduledJobsStatus
Located in `src/Daiv3.App.Maui/Models/DashboardData.cs`

Properties:
- `bool HasScheduledJobs` - True if any jobs registered
- `int TotalJobs` - Count of all jobs
- `int PendingJobs` - Count with status Pending
- `int RunningJobs` - Count with status Running
- `int ScheduledJobs` - Count with status Scheduled (future execution)
- `int CompletedJobs` - Count with status Completed
- `int FailedJobs` - Count with status Failed
- `int CancelledJobs` - Count with status Cancelled
- `int PausedJobs` - Count with status Paused
- `IReadOnlyList<ScheduledJobSummary> Jobs` - Full job list
- `ScheduledJobSummary? NextJob` - **Computed** - Earliest scheduled job (null if none)
- `bool HasErrors` - **Computed** - True if any job has failed
- `int ActiveJobsCount` - **Computed** - Sum of pending + running + scheduled counts

#### ScheduledJobSummary
Located in `src/Daiv3.App.Maui/Models/DashboardData.cs`

Properties:
- `string JobId` - Unique job identifier
- `string JobName` - Human-readable name
- `ScheduledJobStatus Status` - Current status (enum)
- `ScheduleType ScheduleType` - Scheduling type (OneTime/Recurring/Cron/EventTriggered)
- `string? CronExpression` - Cron schedule string (if ScheduleType == Cron)
- `TimeSpan? Interval` - Recurring interval (if ScheduleType == Recurring)
- `string? EventName` - Triggering event (if ScheduleType == EventTriggered)
- `DateTimeOffset? NextRunTime` - Next scheduled execution
- `DateTimeOffset? LastRunTime` - Last completed execution
- `int ExecutionCount` - Total runs
- `TimeSpan? AverageDuration` - Mean execution time
- `TimeSpan? LastDuration` - Duration of most recent run
- `string? ErrorMessage` - Last error description (if Status == Failed)
- `int Priority` - Execution priority (0 = highest)
- `DateTimeOffset CreatedAt` - Job creation timestamp
- `string NextRunDescription` - **Computed** - User-friendly time-until-next (e.g., "in 5 minutes", "Never")
- `string ScheduleDescription` - **Computed** - Human-readable schedule (e.g., "Every 15 minutes", "Cron: 0 3 * * *")

### Service Layer Integration

#### DashboardService.CollectScheduledJobsStatusAsync()
Located in `src/Daiv3.App.Maui/Services/DashboardService.cs`

**Method:**
```csharp
private async Task<ScheduledJobsStatus> CollectScheduledJobsStatusAsync(CancellationToken ct = default)
```

**Logic:**
1. Returns empty `ScheduledJobsStatus` if `IScheduler` is null (scheduler not registered)
2. Calls `IScheduler.GetAllJobsAsync()` for metadata list
3. Returns empty status if no jobs exist
4. Groups jobs by status with `Dictionary<ScheduledJobStatus, int>` for counting
5. Maps each `ScheduledJobMetadata` to `ScheduledJobSummary` via property transfer
6. Populates status counts from dictionary (using `GetValueOrDefault(status, 0)`)
7. Handles exceptions gracefully - logs error and returns empty status

**Constructor:** Updated to accept `IScheduler? scheduler` as 8th parameter (optional for backward compatibility)

### MAUI ViewModel Integration

#### DashboardViewModel
Located in `src/Daiv3.App.Maui/ViewModels/DashboardViewModel.cs`

**New Properties:**
- `bool HasScheduledJobs` - Data binding for jobs visibility
- `int TotalJobs` - Total job count display
- `int PendingJobs`, `int RunningJobs`, `int ScheduledJobs`, `int CompletedJobs`, `int FailedJobs` - Status-specific counts
- `ObservableCollection<ScheduledJobSummary> ScheduledJobs` - Job list for `CollectionView` binding
- `ScheduledJobSummary? NextJob` - Next scheduled job card
- `string ActiveJobsText` - **Computed** - Formats active count as "X active" or "No active jobs"

**UpdateUIFromDashboardData():** Updated to populate 9 scheduled jobs fields from `DashboardData.ScheduledJobs`

### CLI Implementation

#### `dashboard scheduled` Command
Located in `src/Daiv3.App.Cli/Program.cs`

**Usage:**
```
daiv3 dashboard scheduled
```

**Output Sections:**
1. **Status Summary** - 7 status counts (Pending/Running/Scheduled/Completed/Failed/Cancelled/Paused)
2. **Next Scheduled Job** - Highlighted with job name, type, time-until-run
3. **Jobs by Status** - Grouped display with:
   - Job name and ID
   - Schedule type with details (cron expression, interval, or event name)
   - Execution metrics (count, average duration, last duration)
   - Next run time (for scheduled jobs)
   - Error messages (for failed jobs, rendered in red)
4. **No Jobs State** - Friendly message when scheduler is empty
5. **Scheduler Not Available** - Warning when `IScheduler` not registered

**Color Coding:**
- Error messages: `ConsoleColor.Red`
- Section headers: `ConsoleColor.Cyan`
- Summary labels: `ConsoleColor.Gray`

### MAUI UI (Deferred)
Dashboard XAML section for scheduled jobs intentionally deferred. Data models and ViewModel bindings are complete; UI implementation can be added independently.

## Testing Implementation

### Unit Tests
Located in `tests/unit/Daiv3.App.Maui.Tests/DashboardServiceTests.cs`

**Model Tests (13 tests):**
1. `ScheduledJobsStatus_NextJob_ReturnsEarliestScheduledJob` - Verify NextJob selection logic
2. `ScheduledJobsStatus_NextJob_ReturnsNull_WhenNoScheduledJobs` - Empty state handling
3. `ScheduledJobsStatus_NextJob_ReturnsNull_WhenJobsListEmpty` - Null safety
4. `ScheduledJobsStatus_HasErrors_ReturnsTrue_WhenFailedJobsExist` - Error detection
5. `ScheduledJobsStatus_HasErrors_ReturnsFalse_WhenNoFailedJobs` - No false positives
6. `ScheduledJobsStatus_ActiveJobsCount_SumsPendingRunningScheduled` - Aggregation logic
7. `ScheduledJobsStatus_ActiveJobsCount_ReturnsZero_WhenNoActiveJobs` - Zero case
8. `ScheduledJobSummary_NextRunDescription_FormatsTimeUntilRun` - "in X minutes" formatting
9. `ScheduledJobSummary_NextRunDescription_ReturnsNever_WhenNextRunTimeNull` - Null handling
10. `ScheduledJobSummary_ScheduleDescription_FormatsRecurring` - "Every X minutes" formatting
11. `ScheduledJobSummary_ScheduleDescription_FormatsCron` - "Cron: expression" formatting
12. `ScheduledJobSummary_ScheduleDescription_FormatsOneTime` - "One-time" label
13. `ScheduledJobSummary_ScheduleDescription_FormatsEventTriggered` - "On event: name" formatting

**Service Integration Tests (6 tests):**
1. `CollectDashboardDataAsync_IncludesScheduledJobsStatus_WhenSchedulerAvailable` - Integration with IScheduler
2. `CollectScheduledJobsStatusAsync_ReturnsEmptyStatus_WhenSchedulerNull` - Null scheduler handling
3. `CollectScheduledJobsStatusAsync_ReturnsEmptyStatus_WhenNoJobsExist` - Empty jobs list
4. `CollectScheduledJobsStatusAsync_MapsJobMetadataCorrectly` - Property transfer validation
5. `CollectScheduledJobsStatusAsync_CountsJobsByStatus` - Status grouping logic
6. `CollectScheduledJobsStatusAsync_HandlesSchedulerExceptions` - Error resilience

**Test Coverage:** 19 tests covering data model computed properties, service integration, error handling, and null safety.

**Test Results:** All 19 tests passed (verified 2026-03-06)

## Usage and Operational Notes

### CLI Usage
```powershell
# Display scheduled jobs dashboard
daiv3 dashboard scheduled

# Example output:
# Scheduled Jobs Status
# =====================
# Total Jobs: 5
# Pending: 1 | Running: 0 | Scheduled: 3 | Completed: 18 | Failed: 1
#
# Next Scheduled Job
# ==================
# "Daily Knowledge Sync" (Recurring: Every 1 day)
# Runs in 2 hours 15 minutes
#
# Pending Jobs (1)
# ===============
# - Web Crawl Queue Processor (ID: crawl-processor-001)
#   Type: EventTriggered (On event: CrawlJobQueued)
#   Executions: 0 | Created: 2026-03-05 10:30:00 AM
```

### MAUI UI Integration (Pending)
When XAML is implemented, `DashboardViewModel` properties will bind to:
- Status badge counts (`TotalJobs`, `ActiveJobsText`, `FailedJobs`)
- Next job card (`NextJob.JobName`, `NextJob.NextRunDescription`)
- Jobs list `CollectionView` (`ScheduledJobs` collection)
- Error alert visibility (`HasJobErrors` boolean)

### Operational Notes
- **Scheduler dependency:** Requires `IScheduler` registration in DI container
- **Graceful degradation:** Returns empty `ScheduledJobsStatus` if scheduler unavailable
- **Real-time updates:** Call `DashboardViewModel.RefreshCommand` to update job status
- **Performance:** Job list retrieval via `IScheduler.GetAllJobsAsync()` - O(n) complexity
- **Error visibility:** Failed jobs highlighted in CLI (red text) and UI (error badges)

## Dependencies
- **CT-REQ-003** - Dashboard foundation (DashboardService, DashboardViewModel, DashboardData)
- **PTS-REQ-007** - Job scheduler system (`IScheduler`, `ScheduledJobMetadata`, status enums)

## Related Requirements
- **CT-REQ-005** - Indexing progress dashboard (parallel dashboard feature)
- **CT-REQ-006** - Model execution history dashboard (parallel dashboard feature)
