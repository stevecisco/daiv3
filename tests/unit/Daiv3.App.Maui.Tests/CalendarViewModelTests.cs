using Daiv3.App.Maui.ViewModels;
using Daiv3.App.Maui.Services;
using Daiv3.App.Maui.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for CalendarViewModel.
/// Tests CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public sealed class CalendarViewModelTests : IDisposable
{
    private readonly Mock<ICalendarService> _mockCalendarService;
    private readonly Mock<ILogger<CalendarViewModel>> _mockLogger;
    private readonly CalendarViewModel _viewModel;

    public CalendarViewModelTests()
    {
        _mockCalendarService = new Mock<ICalendarService>();
        _mockLogger = new Mock<ILogger<CalendarViewModel>>();

        _viewModel = new CalendarViewModel(
            _mockCalendarService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_viewModel.ScheduledItems);
        Assert.NotNull(_viewModel.UpcomingDeadlines);
        Assert.NotNull(_viewModel.ActiveReminders);
        Assert.Equal("Calendar", _viewModel.Title);
        Assert.Equal(CalendarView.Month, _viewModel.SelectedView);
        Assert.Equal(DateTime.Today, _viewModel.SelectedDate);
    }

    [Fact(Skip = "Requires MainThread dispatcher infrastructure - see CT-REQ-014 notes")]
    public async Task InitializeAsync_LoadsCalendarData()
    {
        // Arrange
        var today = DateTime.Today;
        var calendarData = new CalendarData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            ScheduledItems = new List<ScheduledItem>
            {
                new ScheduledItem
                {
                    ItemId = "item-1",
                    Name = "Test Item",
                    Type = ItemType.Project,
                    Deadline = today.AddDays(3), // Within current month/week
                    ProgressPercent = 50
                }
            },
            UpcomingDeadlines = new List<DeadlineItem>
            {
                new DeadlineItem
                {
                    ItemId ="dead-1",
                    Name  = "Test Deadline",
                    Type = ItemType.Task,
                    Deadline = today.AddDays(2),
                    DaysRemaining = 2
                }
            },
            ActiveReminders = new List<ReminderItem>(),
            Summary = new CalendarSummary
            {
                UpcomingCount = 1,
                OverdueCount = 0,
                AtRiskCount = 0,
                MilestoneCount = 0
            }
        };

        _mockCalendarService.Setup(s => s.CollectCalendarDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendarData);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        Assert.Single(_viewModel.ScheduledItems);
        Assert.Single(_viewModel.UpcomingDeadlines);
        Assert.Equal(1, _viewModel.UpcomingCount);
        Assert.Equal(0, _viewModel.OverdueCount);
    }

    [Fact(Skip = "Requires MainThread dispatcher infrastructure - see CT-REQ-014 notes")]
    public async Task RefreshDataAsync_UpdatesCollections()
    {
        // Arrange
        var calendarData = new CalendarData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            ScheduledItems = new List<ScheduledItem>(),
            UpcomingDeadlines = new List<DeadlineItem>(),
            ActiveReminders = new List<ReminderItem>
            {
                new ReminderItem
                {
                    ReminderId = "rem-1",
                    ItemId = "item-1",
                    ItemName = "Test",
                    Type = ReminderType.DeadlineApproaching,
                    Message = "Test reminder",
                    IsRead = false
                }
            },
            Summary = new CalendarSummary { UpcomingCount = 0, OverdueCount = 0, AtRiskCount = 0, MilestoneCount = 0 }
        };

        _mockCalendarService.Setup(s => s.CollectCalendarDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendarData);

        // Act
        await _viewModel.RefreshDataAsync();

        // Assert
        Assert.Single(_viewModel.ActiveReminders);
        Assert.Equal(1, _viewModel.UnreadReminderCount);
    }

    [Fact]
    public void NavigateNext_Month_AddsOneMonth()
    {
        // Arrange
        var initialDate = new DateTime(2026, 3, 15);
        _viewModel.SelectedDate = initialDate;
        _viewModel.SelectedView = CalendarView.Month;

        // Act
        _viewModel.NavigateNext();

        // Assert
        Assert.Equal(new DateTime(2026, 4, 15), _viewModel.SelectedDate);
    }

    [Fact]
    public void NavigateNext_Week_AddsSevenDays()
    {
        // Arrange
        var initialDate = new DateTime(2026, 3, 15);
        _viewModel.SelectedDate = initialDate;
        _viewModel.SelectedView = CalendarView.Week;

        // Act
        _viewModel.NavigateNext();

        // Assert
        Assert.Equal(new DateTime(2026, 3, 22), _viewModel.SelectedDate);
    }

    [Fact]
    public void NavigateNext_Day_AddsOneDay()
    {
        // Arrange
        var initialDate = new DateTime(2026, 3, 15);
        _viewModel.SelectedDate = initialDate;
        _viewModel.SelectedView = CalendarView.Day;

        // Act
        _viewModel.NavigateNext();

        // Assert
        Assert.Equal(new DateTime(2026, 3, 16), _viewModel.SelectedDate);
    }

    [Fact]
    public void NavigatePrevious_Month_SubtractsOneMonth()
    {
        // Arrange
        var initialDate = new DateTime(2026, 3, 15);
        _viewModel.SelectedDate = initialDate;
        _viewModel.SelectedView = CalendarView.Month;

        // Act
        _viewModel.NavigatePrevious();

        // Assert
        Assert.Equal(new DateTime(2026, 2, 15), _viewModel.SelectedDate);
    }

    [Fact]
    public void NavigatePrevious_Week_SubtractsSevenDays()
    {
        // Arrange
        var initialDate = new DateTime(2026, 3, 15);
        _viewModel.SelectedDate = initialDate;
        _viewModel.SelectedView = CalendarView.Week;

        // Act
        _viewModel.NavigatePrevious();

        // Assert
        Assert.Equal(new DateTime(2026, 3, 8), _viewModel.SelectedDate);
    }

    [Fact]
    public void NavigateToday_SetsDateToToday()
    {
        // Arrange
        _viewModel.SelectedDate = new DateTime(2025, 1, 1);

        // Act
        _viewModel.NavigateToday();

        // Assert
        Assert.Equal(DateTime.Today, _viewModel.SelectedDate);
    }

    [Fact]
    public async Task MarkReminderAsReadAsync_CallsServiceMethod()
    {
        // Arrange
        var reminderId = "rem-1";

        // Act
        await _viewModel.MarkReminderAsReadAsync(reminderId);

        // Assert
        _mockCalendarService.Verify(s => s.MarkReminderAsReadAsync(reminderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SnoozeReminderAsync_CallsServiceWithCorrectDuration()
    {
        // Arrange
        var reminderId = "rem-1";
        var duration = TimeSpan.FromHours(1);

        // Act
        await _viewModel.SnoozeReminderAsync(reminderId, duration);

        // Assert
        _mockCalendarService.Verify(s => s.SnoozeReminderAsync(
            reminderId,
            It.Is<DateTime>(d => d > DateTime.UtcNow && d <= DateTime.UtcNow.AddHours(1.1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ScheduledItemViewModel_DisplaysCorrectProperties()
    {
        // Arrange
        var item = new ScheduledItem
        {
            ItemId = "item-1",
            Name = "Test Item",
            Type = ItemType.Project,
            Deadline = new DateTime(2026, 3, 15),
            ProgressPercent = 75,
            Status = DeadlineStatus.OnTrack,
            IsMilestone = true
        };

        // Act
        var viewModel = new ScheduledItemViewModel(item);

        // Assert
        Assert.Equal("item-1", viewModel.ItemId);
        Assert.Equal("Test Item", viewModel.Name);
        Assert.Equal("Project", viewModel.TypeDisplay);
        Assert.Contains("Mar 15", viewModel.DeadlineDisplay);
        Assert.Equal("75%", viewModel.ProgressDisplay);
        Assert.Equal("OnTrack", viewModel.StatusDisplay);
        Assert.True(viewModel.IsMilestone);
    }

    [Fact]
    public void DeadlineItemViewModel_DisplaysUrgencyIndicator()
    {
        // Arrange
        var urgentItem = new DeadlineItem
        {
            ItemId = "item-1",
            Name = "Urgent Item",
            Type = ItemType.Task,
            Deadline = DateTime.UtcNow.AddHours(12),
            DaysRemaining = 0,
            IsUrgent = true,
            Status = DeadlineStatus.AtRisk
        };

        // Act
        var viewModel = new DeadlineItemViewModel(urgentItem);

        // Assert
        Assert.True(viewModel.IsUrgent);
        Assert.Equal("🔥", viewModel.UrgencyIndicator);
    }

    [Fact]
    public void DeadlineItemViewModel_DisplaysDaysRemaining()
    {
        // Arrange
        var items = new[]
        {
            new DeadlineItem { DaysRemaining = 0 },
            new DeadlineItem { DaysRemaining = 1 },
            new DeadlineItem { DaysRemaining = 5 }
        };

        // Act & Assert
        Assert.Equal("Today", new DeadlineItemViewModel(items[0]).DaysRemainingDisplay);
        Assert.Equal("Tomorrow", new DeadlineItemViewModel(items[1]).DaysRemainingDisplay);
        Assert.Equal("5 days", new DeadlineItemViewModel(items[2]).DaysRemainingDisplay);
    }

    [Fact]
    public void ReminderItemViewModel_DisplaysRelativeTime()
    {
        // Arrange
        var reminder = new ReminderItem
        {
            ReminderId = "rem-1",
            ItemId = "item-1",
            ItemName = "Test Item",
            Type = ReminderType.DeadlineApproaching,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            Message = "Test message",
            IsRead = false
        };

        // Act
        var viewModel = new ReminderItemViewModel(reminder);

        // Assert
        Assert.Contains("ago", viewModel.TimeDisplay);
    }
}
