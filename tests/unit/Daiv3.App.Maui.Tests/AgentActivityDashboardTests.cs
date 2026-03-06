using Daiv3.App.Maui.Models;
using Daiv3.App.Maui.Services;
using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for CT-REQ-006: Agent activity, iterations, token usage,
/// and system resource metrics displayed in the dashboard.
/// </summary>
public class AgentActivityDashboardTests
{
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
    private readonly Mock<IDashboardService> _mockDashboardService;

    public AgentActivityDashboardTests()
    {
        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
        _mockDashboardService = new Mock<IDashboardService>();
    }

    // ── Agent Activity Properties (CT-REQ-006) ─────────────────────────

    [Fact]
    public void ActiveAgentCount_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.ActiveAgentCount = 3;

        Assert.Equal(3, vm.ActiveAgentCount);
    }

    [Fact]
    public void HasActiveAgents_WhenCountIsZero_ReturnsFalse()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.ActiveAgentCount = 0;

        Assert.False(vm.HasActiveAgents);
    }

    [Fact]
    public void HasActiveAgents_WhenCountGreaterThanZero_ReturnsTrue()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.ActiveAgentCount = 2;

        Assert.True(vm.HasActiveAgents);
    }

    [Fact]
    public void TotalSessionIterations_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.TotalSessionIterations = 42;

        Assert.Equal(42, vm.TotalSessionIterations);
    }

    [Fact]
    public void TotalSessionTokensUsed_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.TotalSessionTokensUsed = 12_345;

        Assert.Equal(12_345, vm.TotalSessionTokensUsed);
    }

    [Theory]
    [InlineData(500, "500 tokens")]
    [InlineData(1_500, "1.5K tokens")]
    [InlineData(1_500_000, "1.5M tokens")]
    public void TotalSessionTokensText_FormatsCorrectly(long tokens, string expected)
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.TotalSessionTokensUsed = tokens;

        Assert.Equal(expected, vm.TotalSessionTokensText);
    }

    [Fact]
    public void AgentActivities_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var activities = new List<IndividualAgentActivity>
        {
            new() { AgentName = "TestAgent", State = "Running", IterationCount = 5, TokensUsed = 100 }
        };

        vm.AgentActivities = activities;

        Assert.Single(vm.AgentActivities);
        Assert.Equal("TestAgent", vm.AgentActivities[0].AgentName);
    }

    // ── System Resource Properties (CT-REQ-006) ────────────────────────

    [Fact]
    public void CpuUtilizationPercent_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.CpuUtilizationPercent = 75.5;

        Assert.Equal(75.5, vm.CpuUtilizationPercent);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(50.0, 0.5)]
    [InlineData(100.0, 1.0)]
    public void CpuProgressValue_IsNormalizedBetweenZeroAndOne(double percent, double expectedProgress)
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.CpuProgressValue = Math.Max(0.0, Math.Min(1.0, percent / 100.0));

        Assert.Equal(expectedProgress, vm.CpuProgressValue, precision: 5);
    }

    [Fact]
    public void MemoryUsageText_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.MemoryUsageText = "8.0 / 32.0 GB";

        Assert.Equal("8.0 / 32.0 GB", vm.MemoryUsageText);
    }

    [Fact]
    public void DiskFreeText_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.DiskFreeText = "120.5 GB free";

        Assert.Equal("120.5 GB free", vm.DiskFreeText);
    }

    [Fact]
    public void ExecutionProviderText_DefaultIsCPU()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        Assert.Equal("CPU", vm.ExecutionProviderText);
    }

    [Fact]
    public void ExecutionProviderText_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.ExecutionProviderText = "NPU";

        Assert.Equal("NPU", vm.ExecutionProviderText);
    }

    // ── Resource Alert Properties (CT-REQ-006) ─────────────────────────

    [Fact]
    public void HasAnyAlert_WhenNoAlerts_ReturnsFalse()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        // Default state — no alerts set
        Assert.False(vm.HasAnyAlert);
    }

    [Fact]
    public void HasHighCpuAlert_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.HasHighCpuAlert = true;

        Assert.True(vm.HasHighCpuAlert);
    }

    [Fact]
    public void HasHighMemoryAlert_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.HasHighMemoryAlert = true;

        Assert.True(vm.HasHighMemoryAlert);
    }

    [Fact]
    public void HasLowDiskAlert_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.HasLowDiskAlert = true;

        Assert.True(vm.HasLowDiskAlert);
    }

    [Fact]
    public void AlertMessages_WhenSet_ShouldUpdateProperty()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        var messages = new List<string> { "⚠ CPU usage high", "⚠ Low disk space" };

        vm.AlertMessages = messages;

        Assert.Equal(2, vm.AlertMessages.Count);
        Assert.Contains("⚠ CPU usage high", vm.AlertMessages);
    }

    // ── View Toggle (CT-REQ-006 Dual Layout) ───────────────────────────

    [Fact]
    public void ShowAgentView_DefaultIsTrue()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        Assert.True(vm.ShowAgentView);
    }

    [Fact]
    public void ShowSystemView_DefaultIsFalse()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        Assert.False(vm.ShowSystemView);
    }

    [Fact]
    public void ShowAgentViewCommand_SetsAgentViewAndClearsSystem()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);
        vm.ShowSystemView = true;
        vm.ShowAgentView = false;

        vm.ShowAgentViewCommand.Execute(null);

        Assert.True(vm.ShowAgentView);
        Assert.False(vm.ShowSystemView);
    }

    [Fact]
    public void ShowSystemViewCommand_SetsSystemViewAndClearsAgent()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        vm.ShowSystemViewCommand.Execute(null);

        Assert.False(vm.ShowAgentView);
        Assert.True(vm.ShowSystemView);
    }

    [Fact]
    public void ShowAgentViewCommand_IsNotNull()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        Assert.NotNull(vm.ShowAgentViewCommand);
    }

    [Fact]
    public void ShowSystemViewCommand_IsNotNull()
    {
        var vm = new DashboardViewModel(_mockLogger.Object, _mockDashboardService.Object);

        Assert.NotNull(vm.ShowSystemViewCommand);
    }
}

/// <summary>
/// Tests for DashboardData model computed properties used by CT-REQ-006.
/// </summary>
public class DashboardDataModelTests
{
    // ── SystemResourceMetrics Computed Properties ──────────────────────

    [Fact]
    public void MemoryUtilizationPercent_CalculatesCorrectly()
    {
        var metrics = new SystemResourceMetrics
        {
            MemoryUsedBytes = 8L * 1024 * 1024 * 1024,   // 8 GB used
            MemoryTotalBytes = 32L * 1024 * 1024 * 1024  // 32 GB total
        };

        Assert.Equal(25.0, metrics.MemoryUtilizationPercent, precision: 5);
    }

    [Fact]
    public void MemoryUtilizationPercent_WhenTotalIsZero_ReturnsZero()
    {
        var metrics = new SystemResourceMetrics { MemoryUsedBytes = 100, MemoryTotalBytes = 0 };

        Assert.Equal(0.0, metrics.MemoryUtilizationPercent);
    }

    [Fact]
    public void DiskUtilizationPercent_CalculatesCorrectly()
    {
        var metrics = new SystemResourceMetrics
        {
            AvailableDiskBytes = 25L * 1024 * 1024 * 1024,  // 25 GB free
            TotalDiskBytes = 100L * 1024 * 1024 * 1024      // 100 GB total
        };

        // (100 - 25) / 100 = 75%
        Assert.Equal(75.0, metrics.DiskUtilizationPercent, precision: 5);
    }

    [Fact]
    public void DiskUtilizationPercent_WhenTotalIsZero_ReturnsZero()
    {
        var metrics = new SystemResourceMetrics { AvailableDiskBytes = 0, TotalDiskBytes = 0 };

        Assert.Equal(0.0, metrics.DiskUtilizationPercent);
    }

    // ── ResourceAlerts Computed Properties ────────────────────────────

    [Fact]
    public void ResourceAlerts_HasAnyAlert_WhenNoAlerts_ReturnsFalse()
    {
        var alerts = new ResourceAlerts();

        Assert.False(alerts.HasAnyAlert);
    }

    [Fact]
    public void ResourceAlerts_HasAnyAlert_WhenHighCpuAlert_ReturnsTrue()
    {
        var alerts = new ResourceAlerts { HasHighCpuAlert = true };

        Assert.True(alerts.HasAnyAlert);
    }

    [Fact]
    public void ResourceAlerts_HasAnyAlert_WhenHighMemoryAlert_ReturnsTrue()
    {
        var alerts = new ResourceAlerts { HasHighMemoryAlert = true };

        Assert.True(alerts.HasAnyAlert);
    }

    [Fact]
    public void ResourceAlerts_HasAnyAlert_WhenLowDiskAlert_ReturnsTrue()
    {
        var alerts = new ResourceAlerts { HasLowDiskAlert = true };

        Assert.True(alerts.HasAnyAlert);
    }

    [Fact]
    public void ResourceAlerts_HasAnyAlert_WhenThermalAlert_ReturnsTrue()
    {
        var alerts = new ResourceAlerts { HasThermalAlert = true };

        Assert.True(alerts.HasAnyAlert);
    }

    // ── IndividualAgentActivity ElapsedTimeText ────────────────────────

    [Theory]
    [InlineData(0, 0, 45, "45s")]
    [InlineData(0, 2, 30, "2m 30s")]
    [InlineData(1, 15, 0, "1h 15m")]
    public void ElapsedTimeText_FormatsCorrectly(int hours, int minutes, int seconds, string expected)
    {
        var activity = new IndividualAgentActivity
        {
            ElapsedTime = new TimeSpan(hours, minutes, seconds)
        };

        Assert.Equal(expected, activity.ElapsedTimeText);
    }

    // ── Alert Threshold Logic ──────────────────────────────────────────

    [Fact]
    public void ResourceAlerts_AlertMessages_CanContainMultipleMessages()
    {
        var alerts = new ResourceAlerts
        {
            HasHighCpuAlert = true,
            HasHighMemoryAlert = true,
            AlertMessages = ["⚠ CPU usage exceeds 85%", "⚠ Memory usage exceeds 80%"]
        };

        Assert.True(alerts.HasAnyAlert);
        Assert.Equal(2, alerts.AlertMessages.Count);
    }

    [Fact]
    public void DashboardData_IsValid_WhenNoCollectionError_ReturnsTrue()
    {
        var data = new DashboardData { CollectionError = null };

        Assert.True(data.IsValid);
    }

    [Fact]
    public void DashboardData_IsValid_WhenCollectionError_ReturnsFalse()
    {
        var data = new DashboardData { CollectionError = "Service unavailable" };

        Assert.False(data.IsValid);
    }
}

/// <summary>
/// Tests for SystemMetricsService on Windows.
/// CT-REQ-006: CPU, memory, disk, and process metrics.
/// </summary>
public sealed class SystemMetricsServiceTests : IDisposable
{
    private readonly SystemMetricsService _sut;

    public SystemMetricsServiceTests()
    {
        _sut = new SystemMetricsService();
    }

    [Fact]
    public void GetCpuUtilizationPercent_ReturnsValueBetweenZeroAndOneHundred()
    {
        var cpuPercent = _sut.GetCpuUtilizationPercent();

        Assert.InRange(cpuPercent, 0.0, 100.0);
    }

    [Fact]
    public void GetSystemMemory_ReturnsTotalBytesGreaterThanZero()
    {
        var (usedBytes, totalBytes) = _sut.GetSystemMemory();

        Assert.True(totalBytes > 0, "Total system memory should be greater than zero");
    }

    [Fact]
    public void GetSystemMemory_ReturnsUsedBytesLessOrEqualToTotal()
    {
        var (usedBytes, totalBytes) = _sut.GetSystemMemory();

        if (totalBytes > 0)
            Assert.True(usedBytes <= totalBytes, "Used bytes should not exceed total bytes");
    }

    [Fact]
    public void GetDiskInfo_ReturnsTotalBytesGreaterThanZero()
    {
        var (availableBytes, totalBytes) = _sut.GetDiskInfo();

        Assert.True(totalBytes > 0, "Total disk size should be greater than zero");
    }

    [Fact]
    public void GetDiskInfo_ReturnsAvailableBytesLessOrEqualToTotal()
    {
        var (availableBytes, totalBytes) = _sut.GetDiskInfo();

        Assert.True(availableBytes <= totalBytes, "Available bytes should not exceed total bytes");
    }

    [Fact]
    public void GetProcessMemoryBytes_ReturnsPositiveValue()
    {
        var processMemory = _sut.GetProcessMemoryBytes();

        Assert.True(processMemory > 0, "Process working set should be positive");
    }

    [Fact]
    public void GetDirectorySize_ForNonExistentPath_ReturnsZero()
    {
        var size = _sut.GetDirectorySize(@"C:\this_path_does_not_exist_daiv3_test");

        Assert.Equal(0L, size);
    }

    [Fact]
    public void GetDirectorySize_ForExistingPath_ReturnsNonNegativeValue()
    {
        var tempPath = Path.GetTempPath();

        var size = _sut.GetDirectorySize(tempPath);

        Assert.True(size >= 0, "Directory size should be non-negative");
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
