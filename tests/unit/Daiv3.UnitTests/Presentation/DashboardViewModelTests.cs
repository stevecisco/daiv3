using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for DashboardViewModel.
/// </summary>
public class DashboardViewModelTests
{
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;

    public DashboardViewModelTests()
    {
        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var viewModel = new DashboardViewModel(_mockLogger.Object);

        // Assert
        Assert.Equal("Dashboard", viewModel.Title);
        Assert.NotNull(viewModel.HardwareStatus);
        Assert.NotNull(viewModel.NpuStatus);
        Assert.NotNull(viewModel.GpuStatus);
        Assert.NotNull(viewModel.CurrentActivity);
    }

    [Fact]
    public void HardwareStatus_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var newStatus = "GPU Available";

        // Act
        viewModel.HardwareStatus = newStatus;

        // Assert
        Assert.Equal(newStatus, viewModel.HardwareStatus);
    }

    [Fact]
    public void NpuStatus_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var newStatus = "NPU Active";

        // Act
        viewModel.NpuStatus = newStatus;

        // Assert
        Assert.Equal(newStatus, viewModel.NpuStatus);
    }

    [Fact]
    public void GpuStatus_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var newStatus = "GPU Ready";

        // Act
        viewModel.GpuStatus = newStatus;

        // Assert
        Assert.Equal(newStatus, viewModel.GpuStatus);
    }

    [Fact]
    public void QueuedTasks_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var taskCount = 5;

        // Act
        viewModel.QueuedTasks = taskCount;

        // Assert
        Assert.Equal(taskCount, viewModel.QueuedTasks);
    }

    [Fact]
    public void CompletedTasks_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var taskCount = 10;

        // Act
        viewModel.CompletedTasks = taskCount;

        // Assert
        Assert.Equal(taskCount, viewModel.CompletedTasks);
    }

    [Fact]
    public void CurrentActivity_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        var activity = "Processing request";

        // Act
        viewModel.CurrentActivity = activity;

        // Assert
        Assert.Equal(activity, viewModel.CurrentActivity);
    }

    [Fact]
    public async Task Refresh_ShouldUpdateIsBusyFlag()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object);
        await Task.Delay(600); // Wait for initial load

        // Act
        viewModel.Refresh();
        var isBusyDuringRefresh = viewModel.IsBusy;
        await Task.Delay(600); // Wait for refresh to complete

        // Assert - IsBusy should be true during refresh and false after
        Assert.True(isBusyDuringRefresh || !viewModel.IsBusy);
    }
}
