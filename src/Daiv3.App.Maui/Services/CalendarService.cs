using Daiv3.App.Maui.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Scheduler;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Service for collecting calendar and reminder data.
/// Implements CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public class CalendarService : ICalendarService
{
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<ProjectTask> _taskRepository;
    private readonly IScheduler _scheduler;
    private readonly ILogger<CalendarService> _logger;
    private readonly ConcurrentDictionary<string, ReminderItem> _reminders;

    public CalendarService(
        IRepository<Project> projectRepository,
        IRepository<ProjectTask> taskRepository,
        IScheduler scheduler,
        ILogger<CalendarService> logger)
    {
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reminders = new ConcurrentDictionary<string, ReminderItem>();
    }

    public async Task<CalendarData> CollectCalendarDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Collecting calendar data");

        var scheduledItems = await GetScheduledItemsAsync(
            DateTime.UtcNow.Date.AddMonths(-1),
            DateTime.UtcNow.Date.AddMonths(3),
            cancellationToken);

        var upcomingDeadlines = await GetUpcomingDeadlinesAsync(30, cancellationToken);
        var activeReminders = await GetActiveRemindersAsync(false, cancellationToken);
        var summary = await GetSummaryAsync(cancellationToken);

        // Generate automatic reminders for approaching deadlines
        await GenerateDeadlineRemindersAsync(upcomingDeadlines, cancellationToken);

        return new CalendarData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            ScheduledItems = scheduledItems,
            UpcomingDeadlines = upcomingDeadlines,
            ActiveReminders = await GetActiveRemindersAsync(false, cancellationToken),
            Summary = summary
        };
    }

    public async Task<List<ScheduledItem>> GetScheduledItemsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ScheduledItem>();

        // Collect projects with deadlines
        var projects = await _projectRepository.GetAllAsync(cancellationToken);
        foreach (var project in projects)
        {
            if (project.Deadline.HasValue)
            {
                var deadline = DateTimeOffset.FromUnixTimeSeconds(project.Deadline.Value).UtcDateTime;
                if (deadline >= startDate && deadline <= endDate)
                {
                    items.Add(new ScheduledItem
                    {
                        ItemId = project.ProjectId,
                        Name = project.Name,
                        Type = ItemType.Project,
                        StartDate = DateTimeOffset.FromUnixTimeSeconds(project.CreatedAt).UtcDateTime,
                        Deadline = deadline,
                        ProgressPercent = project.ProgressPercent,
                        AssignedAgent = project.AssignedAgent,
                        Status = CalculateDeadlineStatus(deadline, project.ProgressPercent),
                        Priority = project.Priority,
                        IsMilestone = project.ProgressPercent >= 100.0
                    });
                }
            }
        }

        // Collect tasks with deadlines
        var tasks = await _taskRepository.GetAllAsync(cancellationToken);
        foreach (var task in tasks)
        {
            if (task.ScheduledAt.HasValue)
            {
                var scheduledTime = DateTimeOffset.FromUnixTimeSeconds(task.ScheduledAt.Value).UtcDateTime;
                if (scheduledTime >= startDate && scheduledTime <= endDate)
                {
                    var dependencies = await ParseTaskDependenciesAsync(task.DependenciesJson, cancellationToken);

                    items.Add(new ScheduledItem
                    {
                        ItemId = task.TaskId,
                        Name = task.Title,
                        Type = ItemType.Task,
                        StartDate = task.LastRunAt.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(task.LastRunAt.Value).UtcDateTime
                            : null,
                        Deadline = scheduledTime,
                        ProgressPercent = task.Status == "Completed" || task.Status == "Done" ? 100.0 : 0.0,
                        Status = CalculateDeadlineStatus(scheduledTime, task.Status == "Completed" || task.Status == "Done" ? 100.0 : 0.0),
                        Priority = task.Priority,
                        ProjectId = task.ProjectId,
                        Dependencies = dependencies,
                        IsMilestone = task.Status == "Completed" || task.Status == "Done"
                    });
                }
            }
        }

        return items.OrderBy(i => i.Deadline).ToList();
    }

    public async Task<List<DeadlineItem>> GetUpcomingDeadlinesAsync(
        int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var deadlines = new List<DeadlineItem>();
        var now = DateTime.UtcNow;
        var futureDate = now.AddDays(daysAhead);

        // Projects with upcoming deadlines
        var projects = await _projectRepository.GetAllAsync(cancellationToken);
        foreach (var project in projects)
        {
            if (project.Deadline.HasValue)
            {
                var deadline = DateTimeOffset.FromUnixTimeSeconds(project.Deadline.Value).UtcDateTime;
                if (deadline >= now && deadline <= futureDate && project.Status != "Completed")
                {
                    var daysRemaining = (int)(deadline - now).TotalDays;
                    deadlines.Add(new DeadlineItem
                    {
                        ItemId = project.ProjectId,
                        Name = project.Name,
                        Type = ItemType.Project,
                        Deadline = deadline,
                        DaysRemaining = daysRemaining,
                        Status = CalculateDeadlineStatus(deadline, project.ProgressPercent),
                        AssignedAgent = project.AssignedAgent,
                        ProgressPercent = project.ProgressPercent,
                        Priority = project.Priority,
                        IsUrgent = daysRemaining < 2
                    });
                }
            }
        }

        // Tasks with upcoming deadlines
        var tasks = await _taskRepository.GetAllAsync(cancellationToken);
        foreach (var task in tasks)
        {
            if (task.ScheduledAt.HasValue && task.Status != "Completed" && task.Status != "Done")
            {
                var deadline = DateTimeOffset.FromUnixTimeSeconds(task.ScheduledAt.Value).UtcDateTime;
                if (deadline >= now && deadline <= futureDate)
                {
                    var daysRemaining = (int)(deadline - now).TotalDays;
                    deadlines.Add(new DeadlineItem
                    {
                        ItemId = task.TaskId,
                        Name = task.Title,
                        Type = ItemType.Task,
                        Deadline = deadline,
                        DaysRemaining = daysRemaining,
                        Status = CalculateDeadlineStatus(deadline, 0.0),
                        ProgressPercent = 0.0,
                        Priority = task.Priority,
                        IsUrgent = daysRemaining < 2
                    });
                }
            }
        }

        return deadlines.OrderBy(d => d.Deadline).ToList();
    }

    public Task<List<ReminderItem>> GetActiveRemindersAsync(
        bool includeRead = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeReminders = _reminders.Values
            .Where(r => (includeRead || !r.IsRead) &&
                       (!r.SnoozedUntil.HasValue || r.SnoozedUntil.Value <= now))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return Task.FromResult(activeReminders);
    }

    public Task MarkReminderAsReadAsync(string reminderId, CancellationToken cancellationToken = default)
    {
        if (_reminders.TryGetValue(reminderId, out var reminder))
        {
            reminder.IsRead = true;
            _logger.LogInformation("Marked reminder {ReminderId} as read", reminderId);
        }

        return Task.CompletedTask;
    }

    public Task SnoozeReminderAsync(
        string reminderId,
        DateTime snoozeUntil,
        CancellationToken cancellationToken = default)
    {
        if (_reminders.TryGetValue(reminderId, out var reminder))
        {
            reminder.SnoozedUntil = snoozeUntil;
            _logger.LogInformation("Snoozed reminder {ReminderId} until {SnoozeUntil}", reminderId, snoozeUntil);
        }

        return Task.CompletedTask;
    }

    public async Task<CalendarSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var upcomingDeadlines = await GetUpcomingDeadlinesAsync(7, cancellationToken);
        var allDeadlines = await GetUpcomingDeadlinesAsync(30, cancellationToken);

        // Count overdue items (deadline < now)
        var projects = await _projectRepository.GetAllAsync(cancellationToken);
        var tasks = await _taskRepository.GetAllAsync(cancellationToken);
        
        var overdueCount = 0;
        foreach (var project in projects)
        {
            if (project.Deadline.HasValue && project.Status != "Completed")
            {
                var deadline = DateTimeOffset.FromUnixTimeSeconds(project.Deadline.Value).UtcDateTime;
                if (deadline < now)
                {
                    overdueCount++;
                }
            }
        }
        
        foreach (var task in tasks)
        {
            if (task.ScheduledAt.HasValue && task.Status != "Completed" && task.Status != "Done")
            {
                var deadline = DateTimeOffset.FromUnixTimeSeconds(task.ScheduledAt.Value).UtcDateTime;
                if (deadline < now)
                {
                    overdueCount++;
                }
            }
        }

        var summary = new CalendarSummary
        {
            UpcomingCount = upcomingDeadlines.Count,
            OverdueCount = overdueCount,
            AtRiskCount = upcomingDeadlines.Count(d => d.Status == DeadlineStatus.AtRisk),
            MilestoneCount = allDeadlines.Count(d => d.Type == ItemType.Project && d.ProgressPercent >= 90.0),
            AvailableAgents = new List<string>() // TODO: Implement agent capacity tracking
        };

        return summary;
    }

    public async Task<List<TaskDependency>> GetTaskDependenciesAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task == null)
        {
            return new List<TaskDependency>();
        }

        return await ParseTaskDependenciesAsync(task.DependenciesJson, cancellationToken);
    }

    private async Task<List<TaskDependency>> ParseTaskDependenciesAsync(
        string? dependenciesJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dependenciesJson))
        {
            return new List<TaskDependency>();
        }

        try
        {
            var dependencyIds = JsonSerializer.Deserialize<List<string>>(dependenciesJson) ?? new List<string>();
            var dependencies = new List<TaskDependency>();

            foreach (var depId in dependencyIds)
            {
                var depTask = await _taskRepository.GetByIdAsync(depId, cancellationToken);
                if (depTask != null)
                {
                    dependencies.Add(new TaskDependency
                    {
                        DependentTaskId = depId,
                        DependentTaskName = depTask.Title,
                        Prerequisite = depTask.Status,
                        IsMet = depTask.Status == "Completed" || depTask.Status == "Done"
                    });
                }
            }

            return dependencies;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse task dependencies JSON: {Json}", dependenciesJson);
            return new List<TaskDependency>();
        }
    }

    private static DeadlineStatus CalculateDeadlineStatus(DateTime deadline, double progressPercent)
    {
        var now = DateTime.UtcNow;

        if (deadline < now && progressPercent < 100.0)
        {
            return DeadlineStatus.Overdue;
        }

        var totalTime = (deadline - now.AddDays(-90)).TotalDays; // Assume 90-day typical project
        var elapsedTime = (now - now.AddDays(-90)).TotalDays;
        var timeUsedPercent = (elapsedTime / totalTime) * 100.0;

        // At risk if time used exceeds progress by >20%
        if (timeUsedPercent > progressPercent + 20.0)
        {
            return DeadlineStatus.AtRisk;
        }

        return DeadlineStatus.OnTrack;
    }

    private async Task GenerateDeadlineRemindersAsync(
        List<DeadlineItem> upcomingDeadlines,
        CancellationToken cancellationToken)
    {
        foreach (var deadline in upcomingDeadlines)
        {
            // Generate reminder if deadline is approaching (< 3 days)
            if (deadline.DaysRemaining <= 3 && deadline.DaysRemaining > 0)
            {
                var reminderId = $"deadline-{deadline.ItemId}";
                if (!_reminders.ContainsKey(reminderId))
                {
                    _reminders.TryAdd(reminderId, new ReminderItem
                    {
                        ReminderId = reminderId,
                        ItemId = deadline.ItemId,
                        ItemName = deadline.Name,
                        Type = ReminderType.DeadlineApproaching,
                        CreatedAt = DateTime.UtcNow,
                        Message = $"{deadline.Type} '{deadline.Name}' deadline in {deadline.DaysRemaining} day(s)",
                        IsRead = false
                    });
                }
            }

            // Generate overdue reminder
            if (deadline.Status == DeadlineStatus.Overdue)
            {
                var reminderId = $"overdue-{deadline.ItemId}";
                if (!_reminders.ContainsKey(reminderId))
                {
                    _reminders.TryAdd(reminderId, new ReminderItem
                    {
                        ReminderId = reminderId,
                        ItemId = deadline.ItemId,
                        ItemName = deadline.Name,
                        Type = ReminderType.Overdue,
                        CreatedAt = DateTime.UtcNow,
                        Message = $"{deadline.Type} '{deadline.Name}' is overdue",
                        IsRead = false
                    });
                }
            }
        }
    }
}
