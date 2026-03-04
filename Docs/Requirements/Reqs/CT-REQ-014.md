# CT-REQ-014

Source Spec: 11. Configuration & User Transparency - Requirements

## Requirement (Phase 6 MVP Scope)
The system SHALL provide a Calendar and Reminders Dashboard displaying upcoming deadlines, scheduled tasks, task dependencies, and task-related reminders for deadline-aware project management. Advanced scheduling and dependency graph visualization are deferred to Phase 7+.

## Detailed Scope

### Calendar View

#### Month/Week/Day Views
- **Calendar Grid:** Standard calendar display (month or week view)
- **Events on Dates:**
  - Project deadlines (color-coded by status: on-track green, at-risk orange, overdue red)
  - Task deadlines (smaller indicator)
  - Scheduled agent work (shows which agents assigned)
  - Milestones (star indicator)

- **Click Date:** Show all tasks/deadlines for that day in detail panel
- **Drag Event:** Move deadline to different date (if editable)
- **Color Coding:** Project = one color, task = different color, milestone = star

#### Upcoming Deadlines View
- **List Format:** All upcoming deadlines sorted chronologically
- **Deadline Information per Item:**
  - Item name (project or task)
  - Days until deadline (e.g., "2 days remaining")
  - Status (on-track, at-risk, overdue)
  - Owner/assigned agent
  - Progress toward completion
  - Priority level
  - Visual urgency (red if < 2 days away)

- **Quick Actions:**
  - Click to view details
  - Snooze reminder (postpone notification)
  - Mark complete (if done early)
  - Reassign or reprioritize

### Reminders & Notifications

#### Reminder Types
1. **Deadline Approaching:** Triggered N days before deadline
2. **Overdue:** Task is past deadline, still incomplete
3. **Dependency Blocking:** Task A is pending, blocks task B which starts soon
4. **Task Ready:** Dependencies completed, task ready to start
5. **Agent Assignment:** Notify agent of upcoming task assignment
6. **Milestone Reached:** Celebrate completed milestones

#### Reminder Delivery & Display
- **Toast Notifications:** Auto-dismiss pop-ups in UI corner (3 sec duration)
- **Badge Notifications:** Indicator on calendar showing reminder count
- **Inbox/Notification Center:** All unread reminders in persistent list
- **Email (Optional):** Send daily/weekly digest of upcoming deadlines
- **Snooze Options:** 1 hour, 1 day, 3 days, 1 week

#### Reminder Configuration per User
- **Default Reminder Timing:**
  - Deadline approaching: 3 days before (configurable)
  - Overdue: immediately when past deadline
  - Dependency blocking: immediately when detected
  - Task ready: immediately when dependencies met
  - Milestone: immediately on completion

- **Customization:** Adjust timing per project, disable reminders for low-priority tasks

### Task Dependencies & Scheduling

#### Dependency Visualization (MVP Basic)
- **Dependency Information:**
  - Show which tasks this task depends on (prerequisite tasks)
  - Show which tasks depend on this task (downstream tasks)
  - Dependency status: unmet, met, blocked waiting for this

- **Simple Dependency Display:**
  - List format in task detail: "Depends on: Task X, Task Y"
  - "Blocks: Task Z" (tasks waiting for this)
  - Visual indicator if dependency is blocking start
  - Link to dependent task (click to view)

#### Critical Path (Simple MVP Version)
- **Identify:** The longest chain of dependent tasks
- **Display Critical Path:** Show sequence of critical tasks and their deadlines
- **Time-to-Complete:** Estimated time if everything goes on schedule
- **At-Risk Tasks:** Mark critical path tasks that are at-risk of delay

### Task Scheduling

#### Assigned Schedule per Task
- **Start Date:** When task should start
- **Deadline:** When task must complete
- **Duration:** Estimated or scheduled duration
- **Assigned Agent(s):** Which agents will work on this
- **Buffer Time:** (Optional) Extra time built in for overruns

#### Agent Capacity View
- **Agent Calendar:** Individual agent's assigned tasks over time
- **Time Allocation:** % of time per task/project per day
- **Over-Allocation Detection:** Alert if agent assigned >100% capacity
- **Available Slots:** When agent has free time (for new assignments)

### Summary Dashboard
- **Next 7 Days:** Top 5 upcoming deadlines (card view with progress)
- **Overdue Count:** How many tasks are past deadline (red badge)
- **At-Risk Count:** How many tasks at >80% of deadline time used
- **Upcoming Milestones:** Next 3-5 milestones with dates
- **Agent Availability:** Which agents free in next 3 days (for new work)

### Responsive Design
- **Desktop:** Calendar + upcoming list side-by-side
- **Tablet:** Stacked with collapsible sections
- **Mobile:** Calendar in swipe-able month view, dedicated tab for upcoming

## Implementation Plan (MVP Phase 6)

### Data Source
- **Projects Service:** Project deadlines, milestones
- **Tasks Service:** Task deadlines, dependencies, assignments
- **Scheduler:** Scheduled start/end times
- **Learning Memory:** Optional: milestone celebrations from learnings

### Data Contracts
```csharp
public record ScheduledItem(
    string ItemId,
    string Name,
    string Type, // "Project" or "Task"
    DateTime StartDate,
    DateTime Deadline,
    double ProgressPercent,
    string? AssignedAgent,
    TaskDependency[] Dependencies,
    DeadlineStatus Status
);

public record TaskDependency(
    string DependentTaskId,
    string Prerequisites,
    bool IsMet
);

public enum DeadlineStatus { OnTrack, AtRisk, Overdue }
```

### MAUI Implementation
- **CalendarPage:** New page in App Shell
- **CalendarView:** Month/week view control
- **RemindersPanel:** Notification display
- **DependencyPanel:** Dependency display in task detail
- **CalendarViewModel:** Data binding and reminder logic

### CLI Implementation
- `daiv3 dashboard calendar` - Text calendar with upcoming deadlines
- `daiv3 dashboard reminders` - List all pending reminders
- `daiv3 dashboard reminders --dismiss <reminder-id>` - Mark reminder as read
- `daiv3 dashboard orphaned` - Tasks blocked by unmet dependencies

## Design Considerations (from Ideas-Organized-By-Topic Section 8)
- Provides foundation for "Calendar & Reminders" brainstorming concept
- Supports deadline-driven prioritization (Topic Area 6)
- Enables discipline around commitments and schedules (Topic Area 11)
- Helps prevent missing follow-ups and response deadlines (Topic Area 9)

## Testing Plan
- Unit tests: CalendarViewModel with mock scheduled items
- Integration tests: Real projects/tasks with deadlines
- UI tests: Month/week switching, clicking dates, reminder display
- Notification testing: Reminder triggers at correct times
- Dependency accuracy: Verify dependency status calculations
- Performance: Display calendar with 500+ items <2 sec load

## Usage and Operational Notes
- Calendar view defaults to current month
- Switching between month/week is quick toggle on page
- Reminder notifications appear in app corner (not intrusive)
- Desktop reminder notifications configurable (on/off)
- Reminders stack in notification center if multiple at once
- Calendar synchronized with project/task updates (real-time)
- Events draggable to change dates (if user has edit permission)

## Configuration

### Settings Example
```json
{
  "Calendar": {
    "Enabled": true,
    "DefaultView": "month",
    "Reminders": {
      "Enabled": true,
      "DeadlineApproachingDaysBefore": 3,
      "SnoozeDefaultMinutes": 60,
      "ShowToast": true,
      "ShowEmail": false
    },
    "DependencyDetection": true
  }
}
```

## Phase 6 MVP Scope
- ✅ Calendar view (month/week)
- ✅ Upcoming deadlines list
- ✅ Deadline reminders (toast notifications)
- ✅ Basic dependency tracking (list display)
- ✅ Task scheduling and assignments
- ✅ Agent capacity view (simple)
- ❌ Advanced dependency graph (Phase 7)
- ❌ Critical path analysis with rebalancing (Phase 7)
- ❌ Email digest delivery (Phase 7)
- ❌ Calendar sync with external calendars (Phase 7)

## Dependencies
- KLC-REQ-011 (MAUI framework)
- PTS-REQ-001 (project persistence with deadlines)
- PTS-REQ-007 (task scheduling with start/end dates)
- ARCH-REQ-003 (agent manager for assignments)
- CT-NFR-001 (async/dispatch for real-time updates)

## Related Requirements
- CT-REQ-011 (project dashboard; calendar complements with deadline view)
- CT-REQ-012 (service inspector; can show reminders for task-ready events)
- CT-REQ-013 (time tracking; calendar shows when work scheduled)
- CT-REQ-014-Phase7 (advanced scheduling, dependency graphs - future enhancement)
