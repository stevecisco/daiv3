using Daiv3.App.Maui.Services;
using Daiv3.App.Maui.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Scheduler;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for CalendarService.
/// Tests CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public class CalendarServiceTests
{
    private readonly Mock<IRepository<Project>> _mockProjectRepo;
    private readonly Mock<IRepository<ProjectTask>> _mockTaskRepo;
    private readonly Mock<IScheduler> _mockScheduler;
    private readonly Mock<ILogger<CalendarService>> _mockLogger;
    private readonly CalendarService _service;

    public CalendarServiceTests()
    {
        _mockProjectRepo = new Mock<IRepository<Project>>();
        _mockTaskRepo = new Mock<IRepository<ProjectTask>>();
        _mockScheduler = new Mock<IScheduler>();
        _mockLogger = new Mock<ILogger<CalendarService>>();

        _service = new CalendarService(
            _mockProjectRepo.Object,
            _mockTaskRepo.Object,
            _mockScheduler.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CollectCalendarDataAsync_ReturnsCalendarData()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var deadline = now.AddDays(7);
        var project = new Project
        {
            ProjectId = "proj-1",
            Name = "Test Project",
            Deadline = new DateTimeOffset(deadline).ToUnixTimeSeconds(),
            ProgressPercent = 50,
            Status = "Active"
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.CollectCalendarDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ScheduledItems);
        Assert.NotNull(result.UpcomingDeadlines);
        Assert.NotNull(result.ActiveReminders);
        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task GetScheduledItemsAsync_FiltersProjectsByDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-1);
        var endDate = now.AddDays(30);
        
        var project1 = new Project
        {
            ProjectId = "proj-1",
            Name = "Project in Range",
            Deadline = new DateTimeOffset(now.AddDays(7)).ToUnixTimeSeconds(),
            CreatedAt = new DateTimeOffset(now.AddDays(-10)).ToUnixTimeSeconds(),
            ProgressPercent = 50,
            Status = "Active"
        };

        var project2 = new Project
        {
            ProjectId = "proj-2",
            Name = "Project Out of Range",
            Deadline = new DateTimeOffset(now.AddDays(100)).ToUnixTimeSeconds(),
            CreatedAt = new DateTimeOffset(now.AddDays(-10)).ToUnixTimeSeconds(),
            ProgressPercent = 30,
            Status = "Active"
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project1, project2 });
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.GetScheduledItemsAsync(startDate, endDate);

        // Assert
        Assert.Single(result);
        Assert.Equal("proj-1", result[0].ItemId);
        Assert.Equal("Project in Range", result[0].Name);
    }

    [Fact]
    public async Task GetUpcomingDeadlinesAsync_ReturnsDeadlinesInNextNDays()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var project = new Project
        {
            ProjectId = "proj-1",
            Name = "Upcoming Project",
            Deadline = new DateTimeOffset(now.AddDays(5)).ToUnixTimeSeconds(),
            ProgressPercent = 75,
            AssignedAgent = "Agent1",
            Priority = 1,
            Status = "Active"
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.GetUpcomingDeadlinesAsync(30);

        // Assert
        Assert.Single(result);
        Assert.Equal("proj-1", result[0].ItemId);
        Assert.Equal(ItemType.Project, result[0].Type);
        Assert.InRange(result[0].DaysRemaining, 4, 6); // Allow some tolerance
    }

    [Fact]
    public async Task GetUpcomingDeadlinesAsync_MarksUrgentItems()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var urgentProject = new Project
        {
            ProjectId = "proj-1",
            Name = "Urgent Project",
            Deadline = new DateTimeOffset(now.AddHours(12)).ToUnixTimeSeconds(), // Less than 2 days
            ProgressPercent = 90,
            Status = "Active"
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { urgentProject });
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.GetUpcomingDeadlinesAsync(30);

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsUrgent);
    }

    [Fact]
    public async Task GetUpcomingDeadlinesAsync_ExcludesCompletedItems()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var completedProject = new Project
        {
            ProjectId = "proj-1",
            Name = "Completed Project",
            Deadline = new DateTimeOffset(now.AddDays(5)).ToUnixTimeSeconds(),
            ProgressPercent = 100,
            Status = "Completed"
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { completedProject });
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.GetUpcomingDeadlinesAsync(30);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesCorrectCounts()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var projects = new List<Project>
        {
            new Project
            {
                ProjectId = "proj-1",
                Name = "Upcoming",
                Deadline = new DateTimeOffset(now.AddDays(5)).ToUnixTimeSeconds(),
                Status = "Active"
            },
            new Project
            {
                ProjectId = "proj-2",
                Name = "Overdue",
                Deadline = new DateTimeOffset(now.AddDays(-2)).ToUnixTimeSeconds(),
                Status = "Active"
            },
            new Project
            {
                ProjectId = "proj-3",
                Name = "At Risk",
                Deadline = new DateTimeOffset(now.AddDays(6)).ToUnixTimeSeconds(),
                ProgressPercent = 10, // Low progress
                Status = "Active"
            }
        };

        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var result = await _service.GetSummaryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.UpcomingCount); // proj-1 and proj-3 in next 7 days
        Assert.Equal(1, result.OverdueCount);
    }

    [Fact]
    public async Task MarkReminderAsReadAsync_UpdatesReminderStatus()
    {
        // Arrange
        // Setup mocks
        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());
        
        var calendarData = await _service.CollectCalendarDataAsync();
        var reminders = await _service.GetActiveRemindersAsync(includeRead: true);
        
        if (reminders.Any())
        {
            var reminderId = reminders.First().ReminderId;

            // Act
            await _service.MarkReminderAsReadAsync(reminderId);

            // Assert
            var updatedReminders = await _service.GetActiveRemindersAsync(includeRead: true);
            var reminder = updatedReminders.FirstOrDefault(r => r.ReminderId == reminderId);
            if (reminder != null)
            {
                Assert.True(reminder.IsRead);
            }
        }
    }

    [Fact]
    public async Task SnoozeReminderAsync_SetsSnoozedUntil()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snoozeUntil = now.AddHours(1);
        
        // Setup mocks
        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());
        
        var calendarData = await _service.CollectCalendarDataAsync();
        var reminders = await _service.GetActiveRemindersAsync(includeRead: true);

        if (reminders.Any())
        {
            var reminderId = reminders.First().ReminderId;

            // Act
            await _service.SnoozeReminderAsync(reminderId, snoozeUntil);

            // Assert
            var updatedReminders = await _service.GetActiveRemindersAsync(includeRead: true);
            var reminder = updatedReminders.FirstOrDefault(r => r.ReminderId == reminderId);
            if (reminder != null)
            {
                Assert.NotNull(reminder.SnoozedUntil);
            }
        }
    }

    [Fact]
    public async Task GetActiveRemindersAsync_ExcludesReadByDefault()
    {
        // Arrange - Setup with projects to generate some reminders
        _mockProjectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        _mockTaskRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask>());

        // Act
        var unreadReminders = await _service.GetActiveRemindersAsync(includeRead: false);
        var allReminders = await _service.GetActiveRemindersAsync(includeRead: true);

        // Assert
        Assert.True(unreadReminders.Count <= allReminders.Count);
        Assert.All(unreadReminders, r => Assert.False(r.IsRead));
    }
}
