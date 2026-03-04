# CT-REQ-012

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The system SHALL provide a Background Service Inspector displaying currently running tasks, their lifecycle state, resource usage, and capability to cancel or stop long-running tasks.

## Detailed Scope

### Running Tasks List

#### Task Information Display
Each running task SHALL show:
- **Task Name/ID:** Display task identifier and human-readable name
- **Status:** Running, pending, paused, error, cancelling
- **Start Time:** When task began (e.g., "Started 5 minutes ago")
- **Elapsed Time:** Running duration (e.g., "5m 23s")
- **Progress:** For long tasks with known progress (e.g., "step 3/10", "75% complete")
- **Current Operation:** Brief description of what's executing (e.g., "Processing document 45/100")
- **Agent/Service:** Which agent or background service is running the task
- **Priority:** Task priority (critical/high/normal/low)

#### Task Resource Usage
- **CPU %:** This task's CPU utilization
- **Memory:** RAM used by this task (MB)
- **Thread Count:** Number of threads active for this task
- **Estimated Completion:** ETA if known (e.g., "~8 minutes remaining")
- **Token Usage:** Tokens consumed so far (for model tasks)

### Task Lifecycle & State Machine

#### Task States
- 🔵 **Queued** (blue) - Waiting to start
- 🟢 **Running** (green) - Currently executing
- 🟡 **Paused** (yellow) - Paused, can be resumed
- ⏸️ **Blocked** (grey) - Waiting for external input or resource
- 🔄 **Cancelling** (orange) - Cancellation requested, waiting to clean up
- ❌ **Failed** (red) - Task failed with error
- ✅ **Completed** (grey) - Task finished successfully

#### Task Lifecycle Buttons/Actions
- **Pause:** Only available for running tasks
- **Resume:** Only available for paused tasks
- **Cancel:** Available for queued/running/paused tasks
- **Retry:** Available for failed tasks
- **View Details:** Show full task context, logs, error messages
- **View Logs:** Real-time log output for this task in separate panel

### Filtering & Sorting

#### Quick Filters
- **By Status:** Show running, paused, failed, completed
- **By Service:** Show only tasks from specific agent/service
- **By Priority:** Show critical, high, normal, low
- **Show Only:** Long-running (>5 min), resource-heavy (>50% CPU), errors

#### Sorting Options
- **By Progress:** Most progress first
- **By Elapsed Time:** Longest running first
- **By Resource Usage:** Highest CPU first
- **By Priority:** Critical first
- **By Start Time:** Most recent first

### Cancellation & Cleanup

#### Cancellation Mechanism
- **Cancel Button:** Graceful shutdown via CancellationToken
- **Confirmation Dialog:** "Cancel this task? Any in-progress work will be rolled back."
- **Force Kill Option:** (Advanced) If graceful cancellation times out (>30 sec)
- **Resource Cleanup:** Wait for task to release resources, close files, DB connections

#### Cleanup Verification
- **Pre-Cancellation Checks:**
  - Show what will be affected (e.g., "This will delete 5 temporary files")
  - Ask for confirmation if data loss possible
  - Log cancellation reason for audit trail

- **Post-Cancellation Verification:**
  - Verify all threads terminated
  - Verify no file locks remain
  - Verify database connections released
  - Log cleanup completion status

### Task Details Panel

#### On Double-Click Task
- **Task Metadata:**
  - Full task ID and name
  - Description/purpose
  - Created timestamp
  - Estimated duration
  - Assigned agent

- **Execution Context:**
  - Project associated with task (if any)
  - Request parameters (what inputs were provided)
  - Execution environment (local/online/mixed)

- **Resource Metrics:**
  - CPU % over time (sparkline chart)
  - Memory over time (sparkline)
  - Current thread count + max threads
  - I/O operations (read/write counts)

- **Progress Details:**
  - Progress bar with % and description
  - Breakdown of completed/pending subprocesses
  - Milestone checkpoints (if applicable)
  - Estimated time remaining

- **Log Output:**
  - Real-time console output from task
  - Tail of last 500 lines
  - Filter by log level (info, warning, error)
  - Search within logs

- **Error Information (if failed):**
  - Error type and message
  - Stack trace (if available)
  - Suggested remediation
  - Link to documentation

### Task Aggregation & Statistics

#### Summary Statistics
- **Total Running Tasks:** Count by status
- **Resource Summary:** Total CPU %, total memory, total threads
- **Oldest Task:** How long the longest-running task has been running
- **Recent Completions:** Tasks completed in last hour (quick view)
- **Failure Rate:** % of tasks that fail (trend)

#### Activity Timeline
- **Historical View:** Tasks completed/failed in last 24 hours
- **Peak Activity:** When most tasks were running
- **Common Failures:** Most frequently failing task types
- **Slowest Tasks:** Which task types take longest

### Responsive Design
- **Desktop:** Full list + detail panel side-by-side
- **Tablet:** Stacked, detail panel slides up
- **Mobile:** List only, tap task to open detail in modal

## Implementation Plan

### Data Source
- **Background Service:** Monitor task lifecycle and metrics
- **Orchestration Layer:** Query active agent tasks
- **Model Queue:** Running model executions
- **System.Diagnostics:** Process-level CPU/memory/thread info
- **Logging:** Access task logs from ILogger

### Data Contracts
```csharp
public record BackgroundTask(
    string TaskId,
    string Name,
    string Description,
    TaskStatus Status,
    DateTime StartTime,
    TimeSpan ElapsedTime,
    double ProgressPercent,
    string CurrentOperation,
    string? AgentName,
    int Priority,
    TaskMetrics Metrics
);

public record TaskMetrics(
    double CpuPercent,
    long MemoryBytes,
    int ThreadCount,
    TimeSpan? EstimatedRemaining,
    long TokensUsed
);

public enum TaskStatus { Queued, Running, Paused, Blocked, Cancelling, Failed, Completed }
```

### MAUI Implementation
- **ServiceInspectorPage:** New page in App Shell
- **TaskListView:** Displaying all running tasks
- **TaskDetailPanel:** Flyout or modal for task details
- **TaskMetricsChart:** Sparkline charts for CPU/memory trends
- **LogViewer:** Real-time log output display

### CLI Implementation
- `daiv3 dashboard tasks` - List all running tasks
- `daiv3 dashboard tasks --details <task-id>` - Show full task details
- `daiv3 dashboard tasks cancel <task-id>` - Cancel a task (with confirmation)
- `daiv3 dashboard tasks logs <task-id>` - Show task logs
- `daiv3 dashboard tasks stats` - Summary statistics

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "Background Service Inspector" brainstorming concept
- Supports "Terminal management, ensure terminal is cleaned up" requirement
- Essential for resource cleanup and preventing file/connection leaks
- Enables monitoring of distributed background work (Topic Area 1)

## Testing Plan
- Unit tests: TaskInspectorViewModel with mock task data
- Integration tests: Real background tasks with lifecycle transitions
- UI tests: List rendering, filtering, sorting, detail panel
- Performance: Display 50+ running tasks with <1 sec refresh
- Cancellation: Verify CancellationToken propagation, resource cleanup
- Log streaming: Real-time log output without blocking UI
- Error scenarios: Failed tasks, cancellation timeouts, missing logs

## Usage and Operational Notes
- Service Inspector available to developers and ops folks
- Refreshes automatically every 1-2 seconds (configurable)
- Clicking task shows live details that update in real-time
- Cancel operation shows confirmation with impact summary
- All cancellations logged with reason for audit trail
- Completed tasks retained for 1 hour for post-analysis (configurable)
- Failed tasks retained for 24 hours for debugging
- Archive old tasks for historical analysis

## Configuration

### Settings Example
```json
{
  "BackgroundTasks": {
    "Inspector": {
      "Enabled": true,
      "RefreshIntervalSeconds": 2,
      "LogLineLimit": 500,
      "RetentionHours": {
        "Completed": 1,
        "Failed": 24
      },
      "CancellationTimeoutSeconds": 30
    }
  }
}
```

## Dependencies
- KLC-REQ-011 (MAUI framework)
- ARCH-REQ-003 (task orchestrator for task lifecycle)
- AST-REQ-001 (agent execution model)
- CT-NFR-001 (async/dispatch for real-time updates)
- Logging infrastructure (ILogger<T>)

## Related Requirements
- CT-REQ-003 (dashboard foundation)
- CT-REQ-006 (agent activity; complements with task-level view)
- CT-REQ-010 (resource metrics; includes process-level CPU/memory from this)
- ARCH-ACC-002 (orchestration testability; needed for task inspection)
