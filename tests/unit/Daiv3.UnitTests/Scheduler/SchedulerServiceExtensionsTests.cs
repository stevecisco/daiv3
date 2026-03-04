using Daiv3.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.UnitTests.Scheduler;

/// <summary>
/// Unit tests for SchedulerServiceExtensions, testing DI registration and configuration.
/// </summary>
public class SchedulerServiceExtensionsTests
{
    [Fact]
    public void AddScheduler_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler();
        using var provider = services.BuildServiceProvider();

        // Assert
        var scheduler = provider.GetService<IScheduler>();
        Assert.NotNull(scheduler);
    }

    [Fact]
    public void AddScheduler_RegistersHostedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler();
        using var provider = services.BuildServiceProvider();

        // Assert
        var hostedService = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<SchedulerHostedService>()
            .FirstOrDefault();
        Assert.NotNull(hostedService);
    }

    [Fact]
    public void AddScheduler_WithCustomOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler(options =>
        {
            options.JobTimeoutSeconds = 60;
            options.CheckIntervalMilliseconds = 2000;
            options.MaxConcurrentJobs = 8;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SchedulerOptions>>();

        // Assert
        Assert.Equal(60u, options.Value.JobTimeoutSeconds);
        Assert.Equal(2000u, options.Value.CheckIntervalMilliseconds);
        Assert.Equal(8, options.Value.MaxConcurrentJobs);
    }

    [Fact]
    public void AddScheduler_DefaultOptions_HasExpectedDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SchedulerOptions>>();

        // Assert
        Assert.Equal(300u, options.Value.JobTimeoutSeconds);
        Assert.Equal(1000u, options.Value.CheckIntervalMilliseconds);
        Assert.Equal(4, options.Value.MaxConcurrentJobs);
        Assert.True(options.Value.PersistJobHistory);
        Assert.Equal(100u, options.Value.MaxHistoryPerJob);
        Assert.True(options.Value.EnableStartupRecovery);
    }

    [Fact]
    public void AddScheduler_MultipleCalls_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScheduler();
        services.AddScheduler();
        using var provider = services.BuildServiceProvider();

        // Get all IScheduler instances (should be same singleton)
        var schedulers = provider.GetServices<IScheduler>().ToList();

        // Assert
        // With TryAddSingleton, multiple calls should not create duplicates
        Assert.Single(schedulers);
    }
}
