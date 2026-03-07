namespace Daiv3.App.Maui.Models;

/// <summary>
/// Aggregated calendar and reminder data for CT-REQ-014.
/// Provides upcoming events, deadlines, and reminders for project/task management.
/// </summary>
public class CalendarData
{
    /// <summary>
    /// Gets or sets the timestamp when this data was collected.
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets all scheduled items (projects, tasks, milestones) for calendar display.
    /// </summary>
    public List<ScheduledItem> ScheduledItems { get; set; } = new();

    /// <summary>
    /// Gets or sets upcoming deadlines in chronological order.
    /// </summary>
    public List<DeadlineItem> UpcomingDeadlines { get; set; } = new();

    /// <summary>
    /// Gets or sets active reminders for tasks and deadlines.
    /// </summary>
    public List<ReminderItem> ActiveReminders { get; set; } = new();

    /// <summary>
    /// Gets or sets summary statistics for the dashboard.
    /// </summary>
    public CalendarSummary Summary { get; set; } = new();
}

/// <summary>
/// Represents a scheduled item (project or task) with deadlines.
/// </summary>
public class ScheduledItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemType Type { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public double ProgressPercent { get; set; }
    public string? AssignedAgent { get; set; }
    public List<TaskDependency> Dependencies { get; set; } = new();
    public DeadlineStatus Status { get; set; }
    public int Priority { get; set; }
    public string? ProjectId { get; set; }
    public bool IsMilestone { get; set; }
}

/// <summary>
/// Represents a task dependency relationship.
/// </summary>
public class TaskDependency
{
    public string DependentTaskId { get; set; } = string.Empty;
    public string DependentTaskName { get; set; } = string.Empty;
    public string Prerequisite { get; set; } = string.Empty;
    public bool IsMet { get; set; }
}

/// <summary>
/// Represents an upcoming deadline with urgency indicators.
/// </summary>
public class DeadlineItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemType Type { get; set; }
    public DateTime Deadline { get; set; }
    public int DaysRemaining { get; set; }
    public DeadlineStatus Status { get; set; }
    public string? AssignedAgent { get; set; }
    public double ProgressPercent { get; set; }
    public int Priority { get; set; }
    public bool IsUrgent { get; set; } // < 2 days remaining
}

/// <summary>
/// Represents a reminder notification.
/// </summary>
public class ReminderItem
{
    public string ReminderId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public ReminderType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? SnoozedUntil { get; set; }
}

/// <summary>
/// Summary statistics for calendar dashboard.
/// </summary>
public class CalendarSummary
{
    public int UpcomingCount { get; set; } // Next 7 days
    public int OverdueCount { get; set; }
    public int AtRiskCount { get; set; } // >80% of time elapsed
    public int MilestoneCount { get; set; } // Next 30 days
    public List<string> AvailableAgents { get; set; } = new(); // Agents with < 80% capacity in next 3 days
}

/// <summary>
/// Type of calendar item.
/// </summary>
public enum ItemType
{
    Project,
    Task,
    Milestone,
    ScheduledJob
}

/// <summary>
/// Deadline status indicator.
/// </summary>
public enum DeadlineStatus
{
    OnTrack,
    AtRisk,
    Overdue
}

/// <summary>
/// Type of reminder notification.
/// </summary>
public enum ReminderType
{
    DeadlineApproaching, // N days before deadline
    Overdue,            // Past deadline, incomplete
    DependencyBlocking, // Task blocked by dependency
    TaskReady,          // Dependencies met, ready to start
    AgentAssignment,    // Notify agent of assignment
    MilestoneReached    // Milestone completed
}
