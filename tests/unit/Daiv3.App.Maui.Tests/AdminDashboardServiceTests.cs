using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Daiv3.Infrastructure.Shared.Hardware;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for AdminDashboardService.
/// Tests CT-REQ-010: System Admin Dashboard.
/// </summary>
public sealed class AdminDashboardServiceTests : IDisposable
{
    private readonly Mock<ISystemMetricsService> _mockSystemMetrics;
    private readonly Mock<IHardwareDetectionProvider> _mockHardwareDetection;
    private readonly Mock<IDashboardService> _mockDashboardService;
    private readonly AdminDashboardOptions _options;
    private readonly AdminDashboardService _service;

    public AdminDashboardServiceTests()
    {
        _mockSystemMetrics = new Mock<ISystemMetricsService>();
        _mockHardwareDetection = new Mock<IHardwareDetectionProvider>();
        _mockDashboardService = new Mock<IDashboardService>();
        
        // Set up default options
        _options = new AdminDashboardOptions 
        { 
            CpuThresholdPercent = 85,
            GpuThresholdPercent = 90,
            MemoryThresholdPercent = 80,
            DiskFreeThresholdMb = 1024,
            QueueDepthThreshold = 100
        };

        var optionsWrapper = Options.Create(_options);
        var logger = new Mock<ILogger<AdminDashboardService>>();

        _service = new AdminDashboardService(
            _mockSystemMetrics.Object,
            _mockHardwareDetection.Object,
            _mockDashboardService.Object,
            optionsWrapper,
            logger.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsNonNullMetrics()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.False(metrics.LastUpdated == DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetMetricsAsync_CollectsCpuMetrics()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics.Cpu);
        Assert.True(metrics.Cpu.CoreCount > 0);
        Assert.NotEmpty(metrics.Cpu.PerCoreUtilization);
    }

    [Fact]
    public async Task GetMetricsAsync_CollectsMemoryMetrics()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics.Memory);
        Assert.True(metrics.Memory.PhysicalRamTotalBytes > 0);
        Assert.True(metrics.Memory.AvailableMemoryBytes >= 0);
    }

    [Fact]
    public async Task GetMetricsAsync_CollectsStorageMetrics()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics.Storage);
        Assert.True(metrics.Storage.TotalDiskSpaceBytes > 0);
        Assert.True(metrics.Storage.AvailableDiskSpaceBytes >= 0);
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsHealthyWhenNoErrors()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var metrics = await _service.GetMetricsAsync();

        // Assert
        Assert.True(metrics.IsHealthy);
        Assert.Null(metrics.CollectionError);
    }

    [Fact]
    public async Task GetAlertsAsync_NoAlertsWhenMetricsNormal()
    {
        // Arrange
        SetupDefaultMocks();
        _mockSystemMetrics.Setup(x => x.GetCpuUtilizationPercent()).Returns(30.0);
        _mockSystemMetrics.Setup(x => x.GetSystemMemory()).Returns((1L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024)); // 1GB/8GB

        // Act
        await _service.GetMetricsAsync();
        var alerts = _service.GetAlerts();

        // Assert
        Assert.NotNull(alerts);
        Assert.False(alerts.HighCpuAlert);
        Assert.False(alerts.HighMemoryAlert);
        Assert.False(alerts.LowDiskAlert);
    }

    [Fact]
    public async Task GetAlertsAsync_HighCpuAlertWhenUtilizationExceedsThreshold()
    {
        // Arrange
        SetupDefaultMocks();
        _mockSystemMetrics.Setup(x => x.GetCpuUtilizationPercent()).Returns(90.0); // Above 85% threshold

        // Act
        await _service.GetMetricsAsync();
        var alerts = _service.GetAlerts();

        // Assert
        Assert.True(alerts.HighCpuAlert);
    }

    [Fact]
    public async Task GetAlertsAsync_HighMemoryAlertWhenUtilizationExceedsThreshold()
    {
        // Arrange
        SetupDefaultMocks();
        var totalMemory = 8L * 1024 * 1024 * 1024;
        var usedMemory = 7L * 1024 * 1024 * 1024; // 87.5% - above 80% threshold
        _mockSystemMetrics.Setup(x => x.GetSystemMemory()).Returns((usedMemory, totalMemory));

        // Act
        await _service.GetMetricsAsync();
        var alerts = _service.GetAlerts();

        // Assert
        Assert.True(alerts.HighMemoryAlert);
    }

    [Fact]
    public async Task GetAlertsAsync_LowDiskAlertWhenFreeSpaceBelowThreshold()
    {
        // Arrange
        SetupDefaultMocks();
        var totalDisk = 500L * 1024 * 1024 * 1024;
        var availableDisk = 500L * 1024 * 1024; // 500MB - below 1GB threshold
        _mockSystemMetrics.Setup(x => x.GetDiskInfo()).Returns((availableDisk, totalDisk));

        // Act
        await _service.GetMetricsAsync();
        var alerts = _service.GetAlerts();

        // Assert
        Assert.True(alerts.LowDiskAlert);
    }

    [Fact]
    public void GetMetricsHistory_ReturnsEmptyWhenNoHistory()
    {
        // Act
        var history = _service.GetMetricsHistory(24);

        // Assert
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetMetricsHistory_ReturnsHistoryAfterCollection()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GetMetricsAsync();
        var history = _service.GetMetricsHistory(24);

        // Assert
        Assert.NotNull(history);
        Assert.NotEmpty(history);
        Assert.Single(history);
    }

    [Fact]
    public async Task GetMetricsHistory_FiltersByTimeRange()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GetMetricsAsync(); // Now
        var historyLastHour = _service.GetMetricsHistory(1);
        var historyLast7Days = _service.GetMetricsHistory(7 * 24);

        // Assert
        Assert.NotEmpty(historyLastHour);
        Assert.NotEmpty(historyLast7Days);
    }

    [Fact]
    public async Task StartPollingAsync_StartsPolling()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.StartMetricsPollingAsync(1); // 1 second interval
        await Task.Delay(2000); // Wait for at least one poll cycle
        await _service.StopMetricsPollingAsync();

        // Assert
        // Verify that at least some metrics were collected
        var history = _service.GetMetricsHistory(24);
        Assert.NotEmpty(history);
    }

    [Fact]
    public async Task StopPollingAsync_StopsPolling()
    {
        // Arrange
        SetupDefaultMocks();
        await _service.StartMetricsPollingAsync(1);

        // Act
        var initialCount = _service.GetMetricsHistory(24).Count;
        await _service.StopMetricsPollingAsync();
        await Task.Delay(2000);
        var finalCount = _service.GetMetricsHistory(24).Count;

        // Assert
        // Count should not increase significantly after stopping
        Assert.True(finalCount - initialCount < 5);
    }

    [Fact]
    public async Task MetricsUpdatedEvent_FiredWhenMetricsCollected()
    {
        // Arrange
        SetupDefaultMocks();
        var eventFired = false;
        _service.MetricsUpdated += (sender, metrics) => eventFired = true;

        // Act
        await _service.GetMetricsAsync();

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public async Task AlertsChangedEvent_FiredWhenAlertsChange()
    {
        // Arrange
        SetupDefaultMocks();
        var eventFired = false;
        _service.AlertsChanged += (sender, alerts) => eventFired = true;

        // Act
        _mockSystemMetrics.Setup(x => x.GetCpuUtilizationPercent()).Returns(90.0); // Trigger high CPU alert
        await _service.GetMetricsAsync();

        // Assert
        Assert.True(eventFired);
    }

    // Helper methods

    private void SetupDefaultMocks()
    {
        // System metrics
        _mockSystemMetrics.Setup(x => x.GetCpuUtilizationPercent()).Returns(50.0);
        _mockSystemMetrics.Setup(x => x.GetSystemMemory()).Returns((2L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024));
        _mockSystemMetrics.Setup(x => x.GetDiskInfo()).Returns((100L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024));
        _mockSystemMetrics.Setup(x => x.GetProcessMemoryBytes()).Returns(500L * 1024 * 1024);

        // Hardware detection
        _mockHardwareDetection.Setup(x => x.IsTierAvailable(Daiv3.Infrastructure.Shared.Hardware.HardwareAccelerationTier.Npu)).Returns(true);
        _mockHardwareDetection.Setup(x => x.IsTierAvailable(Daiv3.Infrastructure.Shared.Hardware.HardwareAccelerationTier.Gpu)).Returns(false);
        _mockHardwareDetection.Setup(x => x.GetBestAvailableTier()).Returns(Daiv3.Infrastructure.Shared.Hardware.HardwareAccelerationTier.Npu);

        // Dashboard service
        var dashboardData = new DashboardData 
        { 
            CollectedAt = DateTimeOffset.UtcNow,
            Queue = new QueueStatus { PendingCount = 5 },
            Agent = new AgentStatus { ActiveAgentCount = 2, Activities = [] }
        };
        _mockDashboardService.Setup(x => x.GetDashboardDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboardData);
    }
}
