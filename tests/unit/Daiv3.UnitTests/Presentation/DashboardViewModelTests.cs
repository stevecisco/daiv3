using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for DashboardViewModel.
/// Tests CT-REQ-003: Real-time transparency dashboard.
/// Tests CT-NFR-001: Async/await patterns for UI responsiveness.
/// </summary>
public class DashboardViewModelTests
{
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
    private readonly Mock<IDashboardService> _mockDashboardService;

    public DashboardViewModelTests()
    {
        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
        _mockDashboardService = new Mock<IDashboardService>();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithService()
    {
        // Act
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        // Assert
        Assert.Equal("Dashboard", viewModel.Title);
        Assert.NotNull(viewModel.HardwareStatus);
        Assert.False(viewModel.IsMonitoring);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardViewModel(null!, _mockDashboardService.Object));
    }

    [Fact]
    public void Constructor_WithNullService_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardViewModel(_mockLogger.Object, null!));
    }

    [Fact]
    public void HardwareStatus_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
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
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
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
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
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
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        const int taskCount = 5;

        // Act
        viewModel.QueuedTasks = taskCount;

        // Assert
        Assert.Equal(taskCount, viewModel.QueuedTasks);
    }

    [Fact]
    public void CompletedTasks_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        const int taskCount = 10;

        // Act
        viewModel.CompletedTasks = taskCount;

        // Assert
        Assert.Equal(taskCount, viewModel.CompletedTasks);
    }

    [Fact]
    public void CurrentActivity_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var newActivity = "Processing tasks";

        // Act
        viewModel.CurrentActivity = newActivity;

        // Assert
        Assert.Equal(newActivity, viewModel.CurrentActivity);
    }

    [Fact]
    public void IsMonitoring_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        // Act
        viewModel.IsMonitoring = true;

        // Assert
        Assert.True(viewModel.IsMonitoring);
    }

    [Fact]
    public void LastUpdateTime_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var timestamp = "14:30:45";

        // Act
        viewModel.LastUpdateTime = timestamp;

        // Assert
        Assert.Equal(timestamp, viewModel.LastUpdateTime);
    }

    [Fact]
    public async Task InitializeAsync_WhenNotBusy_ShouldStartMonitoring()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var testData = new DashboardData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            Hardware = new HardwareStatus { OverallStatus = "Ready" }
        };

        _mockDashboardService
            .Setup(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        await viewModel.InitializeAsync();

        // Assert
        _mockDashboardService.Verify(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenBusy_ShouldNotInitialize()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        viewModel.IsBusy = true;

        // Act
        await viewModel.InitializeAsync();

        // Assert
        _mockDashboardService.Verify(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShutdownAsync_ShouldStopMonitoring()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        viewModel.IsMonitoring = true;

        // Act
        await viewModel.ShutdownAsync();

        // Assert
        _mockDashboardService.Verify(s => s.StopMonitoringAsync(), Times.Once);
        Assert.False(viewModel.IsMonitoring);
    }

    [Fact]
    public async Task ManualRefreshAsync_WhenNotBusy_ShouldRefreshData()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var testData = new DashboardData
        {
            CollectedAt = DateTimeOffset.UtcNow,
            Hardware = new HardwareStatus { OverallStatus = "Ready" }
        };

        _mockDashboardService
            .Setup(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Initialize the view model (needed to set up _viewLifetimeCts)
        await viewModel.InitializeAsync();

        // Reset the mock to track the manual refresh call
        _mockDashboardService.Reset();
        _mockDashboardService
            .Setup(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        await viewModel.ManualRefreshAsync();

        // Assert - The service should have been called again
        _mockDashboardService.Verify(s => s.GetDashboardDataAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        await viewModel.ShutdownAsync();
    }

#pragma warning disable IDISP016 // Intentionally testing disposal behavior
    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        // Act
        await viewModel.DisposeAsync();

        // Assert - Should not throw - Intentionally checking non-null after disposal
        Assert.NotNull(viewModel);
    }
#pragma warning restore IDISP016

    [Fact]
    public void PropertyChanged_ShouldNotifyWhenPropertyUpdates()
    {
        // Arrange
        var viewModel = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var propertyChangedCalled = false;
        var changedProperty = string.Empty;

        viewModel.PropertyChanged += (s, e) =>
        {
            propertyChangedCalled = true;
            changedProperty = e.PropertyName;
        };

        // Act
        viewModel.HardwareStatus = "New Status";

        // Assert
        Assert.True(propertyChangedCalled);
        Assert.Equal(nameof(viewModel.HardwareStatus), changedProperty);
    }
}
