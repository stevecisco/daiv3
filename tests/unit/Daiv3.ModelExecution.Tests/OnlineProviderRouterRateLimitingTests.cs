using System.Diagnostics;
using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.ModelExecution.Tests;

/// <summary>
/// Unit tests for OnlineProviderRouter provider-scoped request rate limiting (MQ-REQ-017).
/// </summary>
public class OnlineProviderRouterRateLimitingTests
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;

    public OnlineProviderRouterRateLimitingTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
    }

    [Fact]
    public async Task ExecuteBatchAsync_SameProvider_RateLimitedByProviderWindow()
    {
        using var router = CreateRouter(
            openAiWindowSeconds: 1,
            openAiMaxRequestsPerWindow: 1,
            anthropicWindowSeconds: 1,
            anthropicMaxRequestsPerWindow: 10,
            mapCodeToOpenAi: true);

        var requests = new[]
        {
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Chat",
                Content = "Rate limited request A"
            },
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Code",
                Content = "Rate limited request B"
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var results = await router.ExecuteBatchAsync(requests);
        stopwatch.Stop();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(ExecutionStatus.Completed, result.Status));
        Assert.True(stopwatch.ElapsedMilliseconds >= 850,
            $"Expected rate-limited execution to take at least ~850ms, actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteBatchAsync_DifferentProviders_NotRateLimitedByOtherProvider()
    {
        using var router = CreateRouter(
            openAiWindowSeconds: 1,
            openAiMaxRequestsPerWindow: 1,
            anthropicWindowSeconds: 1,
            anthropicMaxRequestsPerWindow: 1,
            mapCodeToOpenAi: false);

        var requests = new[]
        {
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Chat",
                Content = "OpenAI request"
            },
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Code",
                Content = "Anthropic request"
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var results = await router.ExecuteBatchAsync(requests);
        stopwatch.Stop();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(ExecutionStatus.Completed, result.Status));
        Assert.True(stopwatch.ElapsedMilliseconds < 900,
            $"Expected independent providers to avoid cross-provider throttling (<900ms), actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteBatchAsync_MaxRequestsPerWindowZero_DisablesRateLimiting()
    {
        using var router = CreateRouter(
            openAiWindowSeconds: 1,
            openAiMaxRequestsPerWindow: 0,
            anthropicWindowSeconds: 1,
            anthropicMaxRequestsPerWindow: 10,
            mapCodeToOpenAi: true);

        var requests = new[]
        {
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Chat",
                Content = "Unthrottled request A"
            },
            new ExecutionRequest
            {
                Id = Guid.NewGuid(),
                TaskType = "Code",
                Content = "Unthrottled request B"
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var results = await router.ExecuteBatchAsync(requests);
        stopwatch.Stop();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(ExecutionStatus.Completed, result.Status));
        Assert.True(stopwatch.ElapsedMilliseconds < 900,
            $"Expected disabled rate limiting to complete quickly (<900ms), actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    private OnlineProviderRouter CreateRouter(
        int openAiWindowSeconds,
        int openAiMaxRequestsPerWindow,
        int anthropicWindowSeconds,
        int anthropicMaxRequestsPerWindow,
        bool mapCodeToOpenAi)
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
                    MonthlyOutputTokenLimit = 1000000,
                    RateLimitWindowSeconds = openAiWindowSeconds,
                    MaxRequestsPerWindow = openAiMaxRequestsPerWindow
                },
                ["anthropic"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000,
                    RateLimitWindowSeconds = anthropicWindowSeconds,
                    MaxRequestsPerWindow = anthropicMaxRequestsPerWindow
                }
            }
        };

        var mappingConfig = new TaskToModelMappingConfiguration
        {
            AllowParallelProviderExecution = true,
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
                        ApplicableTaskTypes = mapCodeToOpenAi ? new() { "Unused" } : new() { "Code" },
                        Priority = 10,
                        Enabled = true
                    }
                }
            }
        };

        if (mapCodeToOpenAi)
        {
            mappingConfig.ProviderMappings["openai"].Add(new TaskToModelMapping
            {
                ApplicableTaskTypes = new() { "Code" },
                Priority = 9,
                Enabled = true
            });
        }

        return new OnlineProviderRouter(
            Options.Create(onlineOptions),
            Options.Create(mappingConfig),
            _mockLogger.Object);
    }
}
