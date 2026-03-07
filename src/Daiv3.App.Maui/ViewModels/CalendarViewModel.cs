using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Calendar and Reminders page.
/// Implements CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public sealed partial class CalendarViewModel : BaseViewModel, IDisposable
{
    private readonly ICalendarService _calendarService;
    private readonly ILogger<CalendarViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;

    public CalendarViewModel(
        ICalendarService calendarService,
        ILogger<CalendarViewModel> logger)
    {
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Title = "Calendar";
        
        _scheduledItems = new ObservableCollection<ScheduledItemViewModel>();
        _upcomingDeadlines = new ObservableCollection<DeadlineItemViewModel>();
        _activeReminders = new ObservableCollection<ReminderItemViewModel>();
        _selectedView = CalendarView.Month;
        _selectedDate = DateTime.Today;
    }

    public IReadOnlyList<CalendarView> AvailableViews { get; } = new[]
    {
        CalendarView.Month,
        CalendarView.Week,
        CalendarView.Day
    };

    // Observable Collections
    private ObservableCollection<ScheduledItemViewModel> _scheduledItems;
    public ObservableCollection<ScheduledItemViewModel> ScheduledItems
    {
        get => _scheduledItems;
        set => SetProperty(ref _scheduledItems, value);
    }

    private ObservableCollection<DeadlineItemViewModel> _upcomingDeadlines;
    public ObservableCollection<DeadlineItemViewModel> UpcomingDeadlines
    {
        get => _upcomingDeadlines;
        set => SetProperty(ref _upcomingDeadlines, value);
    }

    private ObservableCollection<ReminderItemViewModel> _activeReminders;
    public ObservableCollection<ReminderItemViewModel> ActiveReminders
    {
        get => _activeReminders;
        set => SetProperty(ref _activeReminders, value);
    }

    // Summary Statistics
    private int _upcomingCount;
    public int UpcomingCount
    {
        get => _upcomingCount;
        set => SetProperty(ref _upcomingCount, value);
    }

    private int _overdueCount;
    public int OverdueCount
    {
        get => _overdueCount;
        set => SetProperty(ref _overdueCount, value);
    }

    private int _atRiskCount;
    public int AtRiskCount
    {
        get => _atRiskCount;
        set => SetProperty(ref _atRiskCount, value);
    }

    private int _milestoneCount;
    public int MilestoneCount
    {
        get => _milestoneCount;
        set => SetProperty(ref _milestoneCount, value);
    }

    private int _unreadReminderCount;
    public int UnreadReminderCount
    {
        get => _unreadReminderCount;
        set => SetProperty(ref _unreadReminderCount, value);
    }

    // View State
    private CalendarView _selectedView;
    public CalendarView SelectedView
    {
        get => _selectedView;
        set
        {
            if (SetProperty(ref _selectedView, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    private DateTime _selectedDate;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>
    /// Initialize the ViewModel and load data.
    /// </summary>
    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }

    /// <summary>
    /// Refresh calendar data.
    /// </summary>
    public async Task RefreshDataAsync()
    {
        if (IsRefreshing || _disposed)
        {
            return;
        }

        if (_refreshCts != null)
        {
            _refreshCts.Cancel();
            _refreshCts.Dispose();
        }

        _refreshCts = new CancellationTokenSource();

        try
        {
            IsRefreshing = true;
            IsBusy = true;

            _logger.LogInformation("Refreshing calendar data");

            var calendarData = await _calendarService.CollectCalendarDataAsync(_refreshCts.Token);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateScheduledItems(calendarData.ScheduledItems);
                UpdateUpcomingDeadlines(calendarData.UpcomingDeadlines);
                UpdateActiveReminders(calendarData.ActiveReminders);
                UpdateSummary(calendarData.Summary);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Calendar data refresh cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing calendar data");
        }
        finally
        {
            IsRefreshing = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigate to next month/week.
    /// </summary>
    public void NavigateNext()
    {
        SelectedDate = SelectedView switch
        {
            CalendarView.Month => SelectedDate.AddMonths(1),
            CalendarView.Week => SelectedDate.AddDays(7),
            CalendarView.Day => SelectedDate.AddDays(1),
            _ => SelectedDate
        };
    }

    /// <summary>
    /// Navigate to previous month/week.
    /// </summary>
    public void NavigatePrevious()
    {
        SelectedDate = SelectedView switch
        {
            CalendarView.Month => SelectedDate.AddMonths(-1),
            CalendarView.Week => SelectedDate.AddDays(-7),
            CalendarView.Day => SelectedDate.AddDays(-1),
            _ => SelectedDate
        };
    }

    /// <summary>
    /// Navigate to today.
    /// </summary>
    public void NavigateToday()
    {
        SelectedDate = DateTime.Today;
    }

    /// <summary>
    /// Mark a reminder as read.
    /// </summary>
    public async Task MarkReminderAsReadAsync(string reminderId)
    {
        try
        {
            await _calendarService.MarkReminderAsReadAsync(reminderId);
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reminder {ReminderId} as read", reminderId);
        }
    }

    /// <summary>
    /// Snooze a reminder.
    /// </summary>
    public async Task SnoozeReminderAsync(string reminderId, TimeSpan duration)
    {
        try
        {
            var snoozeUntil = DateTime.UtcNow.Add(duration);
            await _calendarService.SnoozeReminderAsync(reminderId, snoozeUntil);
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing reminder {ReminderId}", reminderId);
        }
    }

    private void UpdateScheduledItems(List<ScheduledItem> items)
    {
        ScheduledItems.Clear();

        // Filter items based on selected view and date
        var (startDate, endDate) = GetDateRange();
        var filteredItems = items.Where(i =>
            i.Deadline.HasValue &&
            i.Deadline.Value >= startDate &&
            i.Deadline.Value <= endDate);

        foreach (var item in filteredItems)
        {
            ScheduledItems.Add(new ScheduledItemViewModel(item));
        }
    }

    private void UpdateUpcomingDeadlines(List<DeadlineItem> items)
    {
        UpcomingDeadlines.Clear();
        foreach (var item in items)
        {
            UpcomingDeadlines.Add(new DeadlineItemViewModel(item));
        }
    }

    private void UpdateActiveReminders(List<ReminderItem> items)
    {
        ActiveReminders.Clear();
        foreach (var item in items)
        {
            ActiveReminders.Add(new ReminderItemViewModel(item));
        }
        UnreadReminderCount = items.Count(r => !r.IsRead);
    }

    private void UpdateSummary(CalendarSummary summary)
    {
        UpcomingCount = summary.UpcomingCount;
        OverdueCount = summary.OverdueCount;
        AtRiskCount = summary.AtRiskCount;
        MilestoneCount = summary.MilestoneCount;
    }

    private (DateTime startDate, DateTime endDate) GetDateRange()
    {
        return SelectedView switch
        {
            CalendarView.Month => (
                new DateTime(SelectedDate.Year, SelectedDate.Month, 1),
                new DateTime(SelectedDate.Year, SelectedDate.Month, 1).AddMonths(1).AddDays(-1)
            ),
            CalendarView.Week => (
                SelectedDate.AddDays(-(int)SelectedDate.DayOfWeek),
                SelectedDate.AddDays(6 - (int)SelectedDate.DayOfWeek)
            ),
            CalendarView.Day => (
                SelectedDate,
                SelectedDate
            ),
            _ => (DateTime.MinValue, DateTime.MaxValue)
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}

/// <summary>
/// Calendar view mode.
/// </summary>
public enum CalendarView
{
    Month,
    Week,
    Day
}

/// <summary>
/// ViewModel wrapper for scheduled items.
/// </summary>
public class ScheduledItemViewModel
{
    private readonly ScheduledItem _item;

    public ScheduledItemViewModel(ScheduledItem item)
    {
        _item = item;
    }

    public string ItemId => _item.ItemId;
    public string Name => _item.Name;
    public string TypeDisplay => _item.Type.ToString();
    public string DeadlineDisplay => _item.Deadline?.ToString("MMM dd, yyyy") ?? "No deadline";
    public string ProgressDisplay => $"{_item.ProgressPercent:F0}%";
    public string StatusDisplay => _item.Status.ToString();
    public string StatusColor => _item.Status switch
    {
        DeadlineStatus.OnTrack => "#28a745",
        DeadlineStatus.AtRisk => "#ffc107",
        DeadlineStatus.Overdue => "#dc3545",
        _ => "#6c757d"
    };
    public bool IsMilestone => _item.IsMilestone;
    public bool HasDependencies => _item.Dependencies.Any();
    public int DependencyCount => _item.Dependencies.Count;
}

/// <summary>
/// ViewModel wrapper for deadline items.
/// </summary>
public class DeadlineItemViewModel
{
    private readonly DeadlineItem _item;

    public DeadlineItemViewModel(DeadlineItem item)
    {
        _item = item;
    }

    public string ItemId => _item.ItemId;
    public string Name => _item.Name;
    public string TypeDisplay => _item.Type.ToString();
    public string DeadlineDisplay => _item.Deadline.ToString("MMM dd, yyyy");
    public string DaysRemainingDisplay => _item.DaysRemaining == 0 ? "Today" :
        _item.DaysRemaining == 1 ? "Tomorrow" :
        $"{_item.DaysRemaining} days";
    public string StatusDisplay => _item.Status.ToString();
    public string StatusColor => _item.Status switch
    {
        DeadlineStatus.OnTrack => "#28a745",
        DeadlineStatus.AtRisk => "#ffc107",
        DeadlineStatus.Overdue => "#dc3545",
        _ => "#6c757d"
    };
    public bool IsUrgent => _item.IsUrgent;
    public string UrgencyIndicator => _item.IsUrgent ? "🔥" : "";
}

/// <summary>
/// ViewModel wrapper for reminder items.
/// </summary>
public class ReminderItemViewModel
{
    private readonly ReminderItem _item;

    public ReminderItemViewModel(ReminderItem item)
    {
        _item = item;
    }

    public string ReminderId => _item.ReminderId;
    public string ItemId => _item.ItemId;
    public string ItemName => _item.ItemName;
    public string Message => _item.Message;
    public string TypeDisplay => _item.Type.ToString();
    public string TimeDisplay => GetRelativeTime(_item.CreatedAt);
    public bool IsRead => _item.IsRead;
    public bool IsSnoozed => _item.SnoozedUntil.HasValue && _item.SnoozedUntil.Value > DateTime.UtcNow;

    private static string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
