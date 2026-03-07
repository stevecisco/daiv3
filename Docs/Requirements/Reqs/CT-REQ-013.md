# CT-REQ-013

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement (Phase 6 MVP Scope)
The system SHALL provide a Time Tracking Dashboard displaying per-agent, per-project, and per-task time usage with hierarchical rollups for visibility into work distribution and agent utilization. Cost attribution and profitability analysis are deferred to Phase 7+.

## Detailed Scope

### Time Tracking Fundamentals

#### Time Hierarchy
Time tracking displays a 4-level hierarchy:
1. **Project Level:** Total time spent on project (sum of all tasks)
2. **Task Level:** Time spent on specific task (sum of all sub-tasks)
3. **Sub-Task Level:** Time spent on specific work unit
4. **Agent Level:** Time per agent (which agents worked on this task)

#### Time Metrics per Level
- **Elapsed Time:** Actual time spent (wall clock)
- **Billable Hours:** Subset of elapsed time eligible for billing (excludes breaks, errors)
- **Utilization Rate:** % of time actually productive vs. waiting/blocked
- **Cost per Hour:** Agent's hourly cost (e.g., $15/hr for ML model, $50/hr for data scientist)
- **Estimated vs. Actual:** How estimated time compared to actual (for learning)

### Dashboard Views

#### 1. Hierarchical Time View (Primary)
- **Tree View by Project:**
  - Project name
  - Total project time
  - Direct child count (tasks/sub-tasks)
  - Expand to see all child tasks
  - Expand sub-tasks to see component work

- **Per-Level Aggregation:**
  - Project total (all descendants)
  - Task total (all sub-tasks)
  - Sub-task final (no children)
  - Agent time breakdown per node

#### 2. Per-Agent Time View
- **List of Agents:** Each agent with summary
  - Total time this agent has worked
  - Active projects (with % of agent time per project)
  - Utilization rate (time productive / total time)
  - Current task (if working now)
  - Average task duration (learning metric)

- **Agent Drill-Down:**
  - Click agent to see all work assignments
  - Projects this agent has worked on
  - Total time per project
  - Task list with time per task
  - Availability (busy/idle/offline)

#### 3. Per-Project Time View
- **Ranked by Total Time:** Projects sorted by total time invested
- **Time Breakdown:**
  - Total project time
  - Time per task (ordered by time)
  - % of time per agent (pie chart or stacked bar)
  - Overruns: Tasks that exceeded estimated time
  
- **Project Drill-Down:**
  - Click project to see full breakdown
  - Task list with elapsed vs. estimated
  - Agent timeline (Gantt showing when each agent worked)
  - Critical path (longest dependency chain)

#### 4. Timeline View (Gantt-Style)
- **Horizontal Timeline:** Project timeline across calendar
- **Stacked Bars:** One bar per project
- **Color by Agent:** Different color for each agent's work
- **Duration:** Visual bar length shows elapsed time
- **Hover Detail:** Show start/end dates, total time

#### 5. Summary Dashboard
- **Key Metrics Cards:**
  - Total time tracked this period (week/month)
  - Time by agent (top 5 agents, bar chart)
  - Time by project (top 5 projects, bar chart)
  - Average task duration
  - On-time delivery rate (tasks within estimated time)
  - Idle time (agents waiting for work, % of total)

- **Trend Charts:**
  - Time tracking trend (past 4 weeks, line chart)
  - Agent utilization trend
  - Project burn-down (remaining work vs. time)

### Time Tracking Configuration

#### Time Periods
- **Filtering by Date Range:**
  - Quick options: This week, this month, last month, custom range
  - Calendar date picker for start/end
  - Relative options: Last 7 days, last 30 days, this quarter

#### Time Categories
- **Work Type:** Code development, design, testing, review, documentation, meetings, admin
- **Status:** Productive (work advancing), idle (waiting), error (recovery), personal (non-billable)
- **Project Assignment:** Which project this time belongs to
- **Agent:** Which agent performed the work

### Detailed Time Reports

#### Task Time Breakdown
On clicking a task:
- **Estimated vs. Actual:** Show both in side-by-side
- **Variance:** +/- overage with % (e.g., "+5 hours = +33% over estimate")
- **Agent Time Breakdown:** Time per agent who worked on task
- **Sub-Task Details:** If task has sub-tasks, show breakdown
- **Comments/Notes:** Notes about why overruns occurred

#### Agent Performance Metrics
On clicking an agent:
- **Total Time Worked:** Across all projects
- **Projects Assigned:** Count and list
- **Task Completion Rate:** Tasks completed / tasks assigned
- **Average Task Duration:** Learning metric for planning
- **Utilization %:** Time productive vs. idle/blocked
- **Peak Hours:** When agent typically works

### Responsive Design
- **Desktop:** Tree + detail panel side-by-side
- **Tablet:** Stacked with collapsible sections
- **Mobile:** Mobile-optimized list with expandable details

## Implementation Plan (MVP Phase 6)

### Data Collection
- **Background Activity Tracking:** Log start/end time for each task
- **Agent Manager:** Track active agent per task
- **Project Association:** Log which project each task belongs to
- **Estimated Time:** Store estimated vs. actual from project definitions

### Data Contracts
```csharp
public record TimeEntry(
    string TaskId,
    string ProjectId,
    string AgentName,
    DateTime StartTime,
    DateTime EndTime,
    TimeSpan ElapsedTime,
    TimeSpan? EstimatedTime,
    string WorkType,
    string Status
);

public record TimeMetrics(
    TimeSpan TotalTime,
    TimeSpan BillableTime,
    double UtilizationPercent,
    TimeSpan? EstimatedRemaining
);
```

### MAUI Implementation
- **TimeTrackingPage:** New page in App Shell
- **TimeTreeView:** Hierarchical display with expand/collapse
- **TimeMetricsChart:** Charts for trend and breakdown views
- **TimeDetailPanel:** Detailed breakdown on selection

### CLI Implementation
- `daiv3 dashboard time` - Summary time metrics
- `daiv3 dashboard time --project <project-id>` - Time for specific project
- `daiv3 dashboard time --agent <agent-name>` - Time for specific agent
- `daiv3 dashboard time --csv` - Export time data as CSV for spreadsheet

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "Time Tracking View" brainstorming concept
- Supports business model of treating agents as business entities with P&L
- Enables capacity planning and agent utilization analysis (Topic Area 7)
- Prerequisite for cost attribution and profitability (Phase 7, Topic Area 11)

## Testing Plan
- Unit tests: TimeTrackingViewModel with mock time entries
- Integration tests: Real time entries in database
- Accuracy: Verify elapsed time calculations
- UI tests: Tree expansion, filtering, sorting, detail panel
- Performance: Display 10,000+ time entries with <2 sec load
- Export: CSV generation accuracy

## Usage and Operational Notes
- Time tracking starts automatically when task starts, ends when task completes
- Manual time entry option for off-system work (if needed)
- Time view refreshes every 10 seconds (less critical than real-time dashboards)
- Export to CSV for analysis in spreadsheets (e.g., pivot tables)
- Default time period: Current calendar week
- Time metrics retained for historical analysis (configurable retention: 2 years default)

## Configuration

### Settings Example
```json
{
  "TimeTracking": {
    "Enabled": true,
    "DefaultPeriod": "week",
    "UtilizationThreshold": 0.8,
    "RetentionDays": 730,
    "WorkTypeCategories": [
      "Code Development",
      "Design",
      "Testing",
      "Review",
      "Documentation",
      "Meetings",
      "Admin"
    ]
  }
}
```

## Phase 6 MVP Scope
- ✅ Time entry collection and storage (backend)
- ✅ Hierarchical time view (UI)
- ✅ Per-agent time breakdown (UI)
- ✅ Per-project time breakdown (UI)
- ✅ Summary metrics and trends (UI)
- ✅ CSV export (CLI/UI)
- ❌ Cost attribution (Phase 7)
- ❌ Billing/invoice generation (Phase 7)
- ❌ Profitability analysis (Phase 7)

## Dependencies
- KLC-REQ-011 (MAUI framework)
- ARCH-REQ-003 (task orchestrator, active task tracking)
- PTS-REQ-001 (project definition)
- LM-REQ-006 (learning from time patterns, future)
- CT-NFR-001 (async/dispatch for dashboard updates)

## Related Requirements
- CT-REQ-011 (project dashboard; complements with time metrics)
- CT-REQ-006 (agent activity; complements with time tracking per agent)
- CT-REQ-013-Phase7 (cost attribution, profitability - future enhancement)

---

## Implementation Status
**Status:** ✅ COMPLETE (Phase 6 MVP scope)
**Completion Date:** March 6, 2026

## What Was Implemented
- Added `DashboardData.TimeTracking` contract with hierarchical models in `src/Daiv3.App.Maui/Models/DashboardData.cs`:
  - `TimeTrackingStatus`
  - `TimeEntry`
  - `ProjectTimeSummary`
  - `TaskTimeSummary`
  - `AgentTimeSummary`
- Implemented `DashboardService.CollectTimeTrackingStatusAsync()` in `src/Daiv3.App.Maui/Services/DashboardService.cs`:
  - Collects time entries from scheduler execution metadata and active agent executions.
  - Produces per-project and per-task rollups plus per-agent utilization rollups.
  - Computes summary metrics (total tracked, billable, utilization, average task duration, on-time rate).
- Extended `DashboardViewModel` in `src/Daiv3.App.Maui/ViewModels/DashboardViewModel.cs` with CT-REQ-013 properties:
  - `HasTimeEntries`, `TimeProjects`, `TimeAgents`
  - `TimeUtilizationPercent`, `TimeOnTimeDeliveryRate`
  - `TotalTrackedTimeText`, `TotalBillableTimeText`, `AverageTaskDurationText`
- Added a new MAUI dashboard section in `src/Daiv3.App.Maui/Pages/DashboardPage.xaml`:
  - Summary cards (tracked time, billable time, utilization)
  - Project hierarchy rollup list
  - Agent rollup list with utilization
- Added CLI command support in `src/Daiv3.App.Cli/Program.cs`:
  - `daiv3 dashboard time`
  - `daiv3 dashboard time --project <id-or-name>`
  - `daiv3 dashboard time --agent <name>`
  - `daiv3 dashboard time --csv`

## Validation
- `dotnet test tests/unit/Daiv3.App.Maui.Tests/Daiv3.App.Maui.Tests.csproj --nologo --verbosity minimal`
  - Result: 173 passed, 0 failed
- `dotnet test tests/unit/Daiv3.App.Cli.Tests/Daiv3.App.Cli.Tests.csproj --nologo --verbosity minimal`
  - Result: 16 passed, 0 failed

## Test Traceability
- `GetDashboardDataAsync_WithNoTimeSources_ShouldReturnEmptyTimeTracking`
  - Validates empty/default time tracking behavior.
- `GetDashboardDataAsync_WithSchedulerExecutionData_ShouldPopulateTimeTrackingRollups`
  - Validates scheduler-driven entry collection and project/agent hierarchy rollups.
- `TimeTrackingStatus_WithEntries_ShouldComputeSummaryMetrics`
  - Validates aggregate metrics calculations.
- `TimeEntry_IsOverrun_ShouldBeTrueWhenElapsedExceedsEstimate`
  - Validates overrun classification behavior.
- `TimeTrackingProperties_WhenSet_ShouldUpdateValues`
- `TimeTrackingCollections_WhenSet_ShouldUpdateValues`
  - Validates CT-REQ-013 ViewModel bindable properties.
