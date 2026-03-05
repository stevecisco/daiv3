using Daiv3.Orchestration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for SkillResourceMonitor.
/// Tests resource tracking, threshold detection, and cancellation triggers.
/// </summary>
public class SkillResourceMonitorTests
{
    private readonly Mock<ILogger<SkillResourceMonitor>> _loggerMock;
    private readonly SkillSandboxConfiguration _sandboxConfig;

    public SkillResourceMonitorTests()
    {
        _loggerMock = new Mock<ILogger<SkillResourceMonitor>>();
        _sandboxConfig = new SkillSandboxConfiguration
        {
            MaxMemoryBytes = 100 * 1024 * 1024, // 100 MB
            MaxCpuPercentage = 80,
            ResourceCheckIntervalMs = 100 // Fast checks for testing
        };
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesMonitor()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        using var monitor = new SkillResourceMonitor(
            _loggerMock.Object,
            _sandboxConfig,
            "TestSkill",
            cts);

        // Assert
        Assert.NotNull(monitor);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsInitialMetrics()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var monitor = new SkillResourceMonitor(
            _loggerMock.Object,
            _sandboxConfig,
            "TestSkill",
            cts);

        // Act
        await Task.Delay(50); // Allow monitoring to start
        var snapshot = monitor.GetSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.ExecutionDuration.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task GetSnapshot_TracksExecutionDuration()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var monitor = new SkillResourceMonitor(
            _loggerMock.Object,
            _sandboxConfig,
            "TestSkill",
            cts);

        // Act
        await Task.Delay(200);
        var snapshot = monitor.GetSnapshot();

        // Assert
        Assert.True(snapshot.ExecutionDuration.TotalMilliseconds >= 150);
    }

    [Fact]
    public void GetEffectiveMode_UsesDefaultMode()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            DefaultMode = SkillSandboxMode.ResourceLimits
        };

        // Act
        var mode = config.GetEffectiveMode("TestSkill");

        // Assert
        Assert.Equal(SkillSandboxMode.ResourceLimits, mode);
    }

    [Fact]
    public void GetEffectiveMode_UsesSkillOverride()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            DefaultMode = SkillSandboxMode.PermissionChecks,
            SkillOverrides =
            {
                ["TestSkill"] = new SkillSandboxOverride
                {
                    Mode = SkillSandboxMode.None
                }
            }
        };

        // Act
        var mode = config.GetEffectiveMode("TestSkill");

        // Assert
        Assert.Equal(SkillSandboxMode.None, mode);
    }

    [Fact]
    public void GetEffectiveMaxMemory_UsesDefaultLimit()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            MaxMemoryBytes = 500 * 1024 * 1024
        };

        // Act
        var maxMemory = config.GetEffectiveMaxMemory("TestSkill");

        // Assert
        Assert.Equal(500 * 1024 * 1024, maxMemory);
    }

    [Fact]
    public void GetEffectiveMaxMemory_UsesSkillOverride()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            MaxMemoryBytes = 500 * 1024 * 1024,
            SkillOverrides =
            {
                ["MemoryIntensiveSkill"] = new SkillSandboxOverride
                {
                    MaxMemoryBytes = 2L * 1024 * 1024 * 1024 // 2 GB
                }
            }
        };

        // Act
        var maxMemory = config.GetEffectiveMaxMemory("MemoryIntensiveSkill");

        // Assert
        Assert.Equal(2L * 1024 * 1024 * 1024, maxMemory);
    }

    [Fact]
    public void GetEffectiveMaxCpu_UsesDefaultLimit()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            MaxCpuPercentage = 80
        };

        // Act
        var maxCpu = config.GetEffectiveMaxCpu("TestSkill");

        // Assert
        Assert.Equal(80, maxCpu);
    }

    [Fact]
    public void GetEffectiveMaxCpu_UsesSkillOverride()
    {
        // Arrange
        var config = new SkillSandboxConfiguration
        {
            MaxCpuPercentage = 80,
            SkillOverrides =
            {
                ["CpuIntensiveSkill"] = new SkillSandboxOverride
                {
                    MaxCpuPercentage = 95
                }
            }
        };

        // Act
        var maxCpu = config.GetEffectiveMaxCpu("CpuIntensiveSkill");

        // Assert
        Assert.Equal(95, maxCpu);
    }

    [Fact]
    public void SkillSandboxConfiguration_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var config = new SkillSandboxConfiguration();

        // Assert
        Assert.Equal(SkillSandboxMode.PermissionChecks, config.DefaultMode);
        Assert.Equal(500 * 1024 * 1024, config.MaxMemoryBytes);
        Assert.Equal(80, config.MaxCpuPercentage);
        Assert.Equal(1000, config.ResourceCheckIntervalMs);
        Assert.True(config.AllowUntrustedSkills);
        Assert.Empty(config.GlobalAllowedPermissions);
        Assert.Empty(config.GlobalDeniedPermissions);
        Assert.Empty(config.SkillOverrides);
    }

    [Fact]
    public void SkillPermissionCheckResult_InitialState_IsCorrect()
    {
        // Arrange & Act
        var result = new SkillPermissionCheckResult();

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Empty(result.RequestedPermissions);
        Assert.Empty(result.DeniedPermissions);
        Assert.Null(result.DenialReason);
    }

    [Fact]
    public void SkillResourceMetrics_InitialState_IsCorrect()
    {
        // Arrange & Act
        var metrics = new SkillResourceMetrics();

        // Assert
        Assert.Equal(0, metrics.CurrentMemoryBytes);
        Assert.Equal(0, metrics.PeakMemoryBytes);
        Assert.Equal(0, metrics.CpuPercentage);
        Assert.Equal(TimeSpan.Zero, metrics.ExecutionDuration);
        Assert.False(metrics.LimitsExceeded);
        Assert.Null(metrics.ViolationDetails);
    }
}
