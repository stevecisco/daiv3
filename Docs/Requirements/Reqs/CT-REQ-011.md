# CT-REQ-011

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement
The system SHALL provide a Project Master Dashboard displaying nested project hierarchies, project states, progress indicators, and multiple pivot views for project organization and status tracking.

## Detailed Scope

### Project Hierarchy Display

#### Tree View Representation
- **Expandable Project Tree:** Multi-level hierarchy (projects > sub-projects > tasks > sub-tasks)
- **Expand/Collapse:** Click disclosure triangle to expand project and view children
- **Visual Indicators:**
  - Project icon with status color badge (active: green, pending: blue, completed: grey, blocked: red)
  - Child count indicator (e.g., "3 tasks", "1 sub-project + 5 tasks")
  - Progress bar per project (inline, compact)
  - Duration/deadline indicator (e.g., "5d remaining")

#### Project State Indicators
- **Status Colors:**
  - 🟢 **Active** (green) - Currently executing or ready to start
  - 🔵 **Pending** (blue) - Waiting for dependencies or scheduled start
  - 🟡 **Blocked** (yellow) - Waiting for external input or resource
  - 🟢 **On-Track** (green) - Progress matches timeline
  - 🟠 **At-Risk** (orange) - Progress behind, approaching deadline
  - ⚫ **Completed** (grey) - All work finished
  - ❌ **Failed** (red) - Encountered errors, requires intervention

#### Progress Information per Project
- **Progress Bar:** Filled % (0-100%) with color gradient
- **Progress Metric:** "8/10 tasks complete" or "75% done"
- **Deadline:** "Due: March 15" with countdown if <7 days
- **Last Updated:** "Updated 2h ago"
- **Assigned Agent:** Agent name if assigned (e.g., "Developer Agent")

### Multiple Pivot Views

#### 1. Project Tree View (Default)
- **Hierarchy:** Projects > sub-projects > tasks
- **Sorting:** By creation date, custom order, or alphabetical
- **Quick Filters:** Show all, show active only, show at-risk only
- **Search:** Find projects by name, tag, assigned agent

#### 2. Priority View
- **List:** All projects sorted by priority (highest first)
- **Priority Badges:** P0 (critical), P1 (high), P2 (normal), P3 (low)
- **Metadata:** Deadline, assignee, progress, status
- **Quick Actions:** Drag to reprioritize, edit, view details

#### 3. By Status View
- **Columns:** Active, Pending, Blocked, Completed, At-Risk, Failed
- **Drag-Drop:** Manually move projects between columns (status change)
- **Count Badges:** Number of projects per status
- **Visual Grouping:** Status-based color coding

#### 4. By Assignment View
- **Grouped by Agent:** All agents listed with assigned projects
- **Unassigned Bin:** Projects with no agent assignment
- **Capacity Indicator:** Agent bar showing workload (# active projects, % of capacity)
- **Quick Assign:** Drag project to agent to reassign

#### 5. Timeline View
- **Gantt-Style Calendar:** Projects plotted on timeline
- **Start/End:** Visual bar showing project duration
- **Milestone Markers:** Key milestones within projects
- **Dependency Links:** Lines showing which projects block others
- **Hover Details:** Show project summary on hover

#### 6. Metrics View
- **Table Format:** Project name, status, progress, deadline, assignee, tokens used, cost estimate
- **Sortable Columns:** Click header to sort by any metric
- **Conditional Formatting:** Color rows by status, highlight at-risk
- **Aggregates:** Bottom row shows totals (total projects, avg progress, total cost)

### Project Detail Panel

#### On Selection (Click Project)
- **Project Summary:**
  - Full name, description
  - Owner/creator
  - Created date, deadline
  - Status and progress
  - Assigned agent(s)
  - Tags/categories

- **Statistics:**
  - Task count: total/complete/blocked/failed
  - Token usage: total, per task, per agent
  - Time tracking: elapsed, estimated remaining
  - Cost: API spend, compute cost, opportunity cost
  
- **Related Projects:**
  - Dependencies: projects this depends on
  - Dependent: projects depending on this
  - Related: tagged with same categories

- **Quick Actions:**
  - Edit project details
  - Change status/priority
  - Assign/reassign agent
  - View all tasks
  - View learnings/outcomes
  - Archive or delete

### Filtering & Search

#### Quick Filters
- **By Status:** Show only active, pending, blocked, completed, at-risk, failed
- **By Priority:** Show P0-only, P0-P1, all, P2-P3
- **By Agent:** Show projects assigned to specific agent or unassigned
- **By Date:** Show projects due this week, overdue, next 30 days
- **By Tag:** Filter by custom project tags/categories

#### Advanced Search
- **Text Search:** Project name, description, tags
- **Date Range:** Filter by creation date or deadline range
- **Progress Range:** Show projects 0-25% done, 25-50%, etc.
- **Cost Range:** Show projects under/over budget
- **Regex Support:** For advanced filtering (optional)

### Analytics & Insights

#### Dashboard Widgets
- **Total Project Count:** By status pie chart
- **Progress Average:** Across all projects (80% avg)
- **Overdue Count:** Projects past deadline (visual warning if >0)
- **Upcoming Deadlines:** Next 5 projects due (list with countdown)
- **Cost Breakdown:** Total spend, by project, by agent
- **Throughput:** Projects completed this week/month (trend)
- **Agent Capacity:** Utilization % per agent (simple bar chart)

### Responsive Design
- **Desktop (full width):** Tree view + detail panel side-by-side
- **Tablet:** Side-by-side with collapsible panels
- **Mobile:** Stack vertically, detail panel slides up on selection

## Implementation Plan

### Data Source
- **Projects Service:** Query from PTS-REQ-001 (project persistence)
- **Orchestration Layer:** For current agent assignments, task states
- **Learning Memory:** For outcomes and learnings per project
- **Queue Service:** For current task status within projects

### Data Contracts
```csharp
public record ProjectNode(
    string ProjectId,
    string Name,
    string Description,
    ProjectStatus Status,
    int Priority,
    double ProgressPercent,
    DateTime? Deadline,
    string? AssignedAgent,
    List<ProjectNode> SubProjects,
    List<ProjectTask> Tasks,
    DateTime CreatedDate,
    DateTime? CompletedDate,
    decimal? EstimatedCost,
    decimal? ActualCost
);

public enum ProjectStatus { Active, Pending, Blocked, OnTrack, AtRisk, Completed, Failed }
```

### MAUI Implementation
- **ProjectsPage:** Existing (ARCH-REQ-002 complete 90%)
- **ProjectMasterDashboard:** Enhance with tree view, multiple pivot tabs
- **ProjectTreeView:** Custom control for collapsible hierarchy
- **ProjectDetailPanel:** Flyout or side panel with full project details
- **ProjectViewModel:** Bind tree data, handle selection, filtering

### CLI Commands
- `daiv3 projects list` - Tabular project list with status
- `daiv3 projects tree` - Nested tree output
- `daiv3 projects by-status` - Grouped by status
- `daiv3 projects by-agent` - Grouped by assignment
- `daiv3 projects analytics` - Dashboard metrics (count, progress, cost, etc.)

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "Project Master Dashboard" brainstorming concept
- Multiple pivot views align with "Master Organizer" skill idea
- Supports "Treat each idea as microbusiness" philosophy with project-level P&L
- Deadline tracking supports time management in Topic Area 9

## Testing Plan
- Unit tests: ProjectTreeViewModel with mock hierarchy data
- Integration tests: Dashboard with real project database
- UI tests: Expand/collapse, sorting, filtering, view switching
- Performance: Display 100+ nested projects with <2 sec load time
- Responsiveness: Smooth rendering with 1000s of items (virtual scrolling if needed)
- Pivot view accuracy: Verify counts and grouping across all views
- Edit functionality: Verify status/priority changes persist

## Usage and Operational Notes
- Projects view loads with tree view by default
- Breadcrumb navigation shows current filter/view state
- Clicking project in any view shows detail panel (right side or flyout)
- Keyboard shortcuts: Ctrl+F for search, arrow keys for navigation, Enter to select
- Drag-drop to reorder projects within parent, change status, or reassign
- Projects marked as archived hidden by default (checkbox to show)
- Default to showing only active + pending projects (configurable)
- Refresh button or auto-refresh every 30 seconds (configurable)

## Configuration

### Settings Example
```json
{
  "Projects": {
    "Dashboard": {
      "DefaultView": "Tree",
      "SortBy": "CreatedDate",
      "ShowArchived": false,
      "RefreshIntervalSeconds": 30,
      "VirtualScrollingThreshold": 1000
    }
  }
}
```

## Dependencies
- KLC-REQ-011 (MAUI framework)
- PTS-REQ-001 (project persistence)
- PTS-REQ-007 (scheduling; for deadline info)
- ARCH-REQ-003 (agent manager for assignment data)
- LM-REQ-003 (learning memory for project outcomes)
- CT-NFR-001 (async/dispatch for real-time updates)

## Related Requirements
- ARCH-REQ-002 (Projects page; CT-REQ-011 enhances with master dashboard)
- CT-REQ-003 (dashboard foundation)
- CT-REQ-013 (time tracking; complements project view with time metrics)
- PTS-ACC-001 (project acceptance criteria)
