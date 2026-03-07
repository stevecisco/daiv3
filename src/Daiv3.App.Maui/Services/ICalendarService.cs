using Daiv3.App.Maui.Models;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Service for collecting calendar and reminder data.
/// Implements CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Collects all calendar data including scheduled items, deadlines, and reminders.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated calendar data.</returns>
    Task<CalendarData> CollectCalendarDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled items for a specific date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of scheduled items in the date range.</returns>
    Task<List<ScheduledItem>> GetScheduledItemsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming deadlines sorted chronologically.
    /// </summary>
    /// <param name="daysAhead">Number of days to look ahead (default: 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of deadline items.</returns>
    Task<List<DeadlineItem>> GetUpcomingDeadlinesAsync(
        int daysAhead = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active reminders for the current user.
    /// </summary>
    /// <param name="includeRead">Whether to include already-read reminders (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of reminder items.</returns>
    Task<List<ReminderItem>> GetActiveRemindersAsync(
        bool includeRead = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a reminder as read.
    /// </summary>
    /// <param name="reminderId">The reminder ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkReminderAsReadAsync(string reminderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Snoozes a reminder until a specified time.
    /// </summary>
    /// <param name="reminderId">The reminder ID.</param>
    /// <param name="snoozeUntil">The time to snooze until.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SnoozeReminderAsync(
        string reminderId,
        DateTime snoozeUntil,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets summary statistics for the calendar dashboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calendar summary statistics.</returns>
    Task<CalendarSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets task dependencies for a specific task.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of task dependencies.</returns>
    Task<List<TaskDependency>> GetTaskDependenciesAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
