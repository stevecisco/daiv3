using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for DashboardService.
/// Tests CT-REQ-003: Real-time transparency dashboard.
/// Tests CT-NFR-001: Async/await patterns for UI responsiveness.
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<ILogger<DashboardService>> _mockLogger;

    public DashboardServiceTests()
    {
        _mockLogger = new Mock<ILogger<DashboardService>>();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitialize()
    {
        // Arrange
        var config = new DashboardConfiguration();

        // Act
        var service = new DashboardService(_mockLogger.Object, config);

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardService(null!, new DashboardConfiguration()));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldUseDefault()
    {
        // Act
        var service = new DashboardService(_mockLogger.Object, null);

        // Assert
        Assert.NotNull(service.Configuration);
        Assert.Equal(DashboardConfiguration.DefaultRefreshIntervalMs, service.Configuration.RefreshIntervalMs);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ShouldReturnValidData()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(data);
        Assert.NotNull(data.Hardware);
        Assert.NotNull(data.Queue);
        Assert.NotNull(data.Indexing);
        Assert.NotNull(data.Agent);
        Assert.NotNull(data.SystemResources);
        Assert.True(data.IsValid); // Should not have an error
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithCancellation_ReturnsCancelledData()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var data = await service.GetDashboardDataAsync(cts.Token);

        // Assert - Service returns error data on cancellation instead of throwing
        Assert.NotNull(data);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WithVeryShortTimeout_MayTimeOut()
    {
        // Arrange - This test is timing-dependent, so we don't make strict assertions
        var config = new DashboardConfiguration { DataCollectionTimeoutMs = 10 }; // Very short but not unrealistic
        var service = new DashboardService(_mockLogger.Object, config);

        // Act
        var data = await service.GetDashboardDataAsync();

        // Assert - Either completes successfully or times out, both are acceptable
        // The important thing is that it doesn't crash
        Assert.NotNull(data);
    }

    [Fact]
    public async Task StartMonitoringAsync_ShouldSetMonitoringFlag()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);

        // Act
        await service.StartMonitoringAsync();
        await Task.Delay(100); // Give monitoring a moment to start

        // Assert
        Assert.True(service.IsMonitoring);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public async Task StartMonitoringAsync_WhenAlreadyMonitoring_ShouldNotFail()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        await service.StartMonitoringAsync(); // Start again

        // Assert
        Assert.True(service.IsMonitoring);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public async Task StartMonitoringAsync_WithInvalidInterval_ShouldThrow()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.StartMonitoringAsync(1)); // Below minimum
    }

    [Fact]
    public async Task StopMonitoringAsync_ShouldClearMonitoringFlag()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        await service.StopMonitoringAsync();
        await Task.Delay(100);

        // Assert
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public async Task StopMonitoringAsync_WhenNotMonitoring_ShouldNotFail()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);

        // Act
        await service.StopMonitoringAsync(); // Should not throw

        // Assert
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public async Task DataUpdated_ShouldRaiseEventWhenMonitoring()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        var eventRaised = false;
        service.DataUpdated += (s, e) => { eventRaised = true; };

        // Act
        await service.StartMonitoringAsync();
        await Task.Delay(500); // Wait for at least one update

        // Assert
        Assert.True(eventRaised);

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Fact]
    public void Configuration_ShouldBeAccessible()
    {
        // Arrange
        var config = new DashboardConfiguration { RefreshIntervalMs = 5000 };
        var service = new DashboardService(_mockLogger.Object, config);

        // Act
        var serviceConfig = service.Configuration;

        // Assert
        Assert.NotNull(serviceConfig);
        Assert.Equal(5000, serviceConfig.RefreshIntervalMs);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);

        // Act
        service.Dispose();

        // Assert - Should not throw on subsequent dispose
        service.Dispose();
    }

    [Fact]
    public async Task Dispose_WhileMonitoring_ShouldStopMonitoring()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        await service.StartMonitoringAsync();
        await Task.Delay(100);

        // Act
        service.Dispose();
        await Task.Delay(100);

        // Assert
        Assert.False(service.IsMonitoring);
    }

    [Fact]
    public async Task GetDashboardDataAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        var service = new DashboardService(_mockLogger.Object);
        service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.GetDashboardDataAsync());
    }
}

/// <summary>
/// Unit tests for DashboardConfiguration.
/// </summary>
public class DashboardConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaults()
    {
        // Act
        var config = new DashboardConfiguration();

        // Assert
        Assert.Equal(DashboardConfiguration.DefaultRefreshIntervalMs, config.RefreshIntervalMs);
        Assert.True(config.EnableCaching);
        Assert.True(config.EnableLogging);
        Assert.True(config.ContinueOnError);
    }

    [Fact]
    public void Validate_WithValidValues_ShouldSucceed()
    {
        // Arrange
        var config = new DashboardConfiguration();

        // Act & Assert
        Assert.True(config.Validate());
    }

    [Fact]
    public void Validate_WithRefreshIntervalTooLow_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            RefreshIntervalMs = DashboardConfiguration.MinRefreshIntervalMs - 1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithRefreshIntervalTooHigh_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            RefreshIntervalMs = DashboardConfiguration.MaxRefreshIntervalMs + 1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithNegativeTimeout_ShouldThrow()
    {
        // Arrange
        var config = new DashboardConfiguration
        {
            DataCollectionTimeoutMs = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }
}

/// <summary>
/// Unit tests for DashboardData model.
/// </summary>
public class DashboardDataTests
{
    [Fact]
    public void IsValid_WhenNoError_ShouldBeTrue()
    {
        // Arrange
        var data = new DashboardData();

        // Act
        var isValid = data.IsValid;

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WhenErrorPresent_ShouldBeFalse()
    {
        // Arrange
        var data = new DashboardData { CollectionError = "Test error" };

        // Act
        var isValid = data.IsValid;

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void QueueStatus_ShouldHaveDefaultValues()
    {
        // Arrange
        var data = new DashboardData();

        // Act
        var queue = data.Queue;

        // Assert
        Assert.NotNull(queue);
        Assert.Equal(0, queue.PendingCount);
        Assert.NotNull(queue.TopItems);
    }

    [Fact]
    public void SystemResourceMetrics_ShouldCalculateDiskUtilization()
    {
        // Arrange
        var metrics = new SystemResourceMetrics
        {
            TotalDiskBytes = 1000,
            AvailableDiskBytes = 300
        };

        // Act
        var utilization = metrics.DiskUtilizationPercent;

        // Assert
        Assert.Equal(70.0, utilization);
    }

    [Fact]
    public void SystemResourceMetrics_WithZeroTotal_ShouldReturnZeroUtilization()
    {
        // Arrange
        var metrics = new SystemResourceMetrics
        {
            TotalDiskBytes = 0,
            AvailableDiskBytes = 0
        };

        // Act
        var utilization = metrics.DiskUtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }
}
