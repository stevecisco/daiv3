using System.Diagnostics;
using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.ModelExecution.Tests;

/// <summary>
/// Unit tests for OnlineProviderRouter concurrent provider execution (MQ-REQ-016).
/// </summary>
public class OnlineProviderRouterParallelExecutionTests
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;

    public OnlineProviderRouterParallelExecutionTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
    }

    [Fact]
    public async Task ExecuteBatchAsync_NullRequests_ThrowsArgumentNullException()
    {
        using var router = CreateRouter();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            router.ExecuteBatchAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteBatchAsync_EmptyRequests_ReturnsEmptyResults()
    {
        using var router = CreateRouter();

        var results = await router.ExecuteBatchAsync(Array.Empty<ExecutionRequest>());

        Assert.Empty(results);
    }

    [Fact]
    public async Task ExecuteBatchAsync_ParallelEnabled_ExecutesDifferentProvidersConcurrently()
    {
        using var router = CreateRouter(allowParallelProviderExecution: true);
        var requests = new[]
        {
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Chat",
                Content = "Parallel request A"
            },
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Code",
                Content = "Parallel request B"
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var results = await router.ExecuteBatchAsync(requests);
        stopwatch.Stop();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(ExecutionStatus.Completed, result.Status));
        Assert.Equal(requests[0].Id, results[0].RequestId);
        Assert.Equal(requests[1].Id, results[1].RequestId);
        Assert.True(stopwatch.ElapsedMilliseconds < 350,
            $"Expected concurrent execution under 350ms, actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteBatchAsync_ParallelDisabled_ExecutesSequentially()
    {
        using var router = CreateRouter(allowParallelProviderExecution: false);
        var requests = new[]
        {
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Chat",
                Content = "Sequential request A"
            },
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Code",
                Content = "Sequential request B"
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var results = await router.ExecuteBatchAsync(requests);
        stopwatch.Stop();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(ExecutionStatus.Completed, result.Status));
        Assert.True(stopwatch.ElapsedMilliseconds >= 350,
            $"Expected sequential execution at or above 350ms, actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    private OnlineProviderRouter CreateRouter(bool allowParallelProviderExecution = true)
    {
        var onlineOptions = new OnlineProviderOptions
        {
            ConfirmationMode = ConfirmationMode.Never,
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000
                },
                ["anthropic"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000
                }
            }
        };

        var mappingConfig = new TaskToModelMappingConfiguration
        {
            AllowParallelProviderExecution = allowParallelProviderExecution,
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = true
                    }
                },
                ["anthropic"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Code" },
                        Priority = 10,
                        Enabled = true
                    }
                }
            }
        };

        return new OnlineProviderRouter(
            Options.Create(onlineOptions),
            Options.Create(mappingConfig),
            _mockLogger.Object);
    }
}
