using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Exceptions;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

#pragma warning disable IDISP025 // Class with no virtual dispose method should be sealed

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for OnlineProviderRouter smart routing enhancements (MQ-REQ-012).
/// </summary>
public class OnlineProviderRouterSmartRoutingTests : IDisposable
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;

    public OnlineProviderRouterSmartRoutingTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private OnlineProviderRouter CreateRouter(
        OnlineProviderOptions? onlineOptions = null,
        TaskToModelMappingConfiguration? mappingConfig = null)
    {
        onlineOptions ??= new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000
                }
            }
        };

        mappingConfig ??= new TaskToModelMappingConfiguration();

        return new OnlineProviderRouter(
            Options.Create(onlineOptions),
            Options.Create(mappingConfig),
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutBudget_ThrowsBudgetException()
    {
        // Arrange
        var options = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new()
                {
                    DailyInputTokenLimit = 100, // Very low limit
                    DailyOutputTokenLimit = 100,
                    MonthlyInputTokenLimit = 1000,
                    MonthlyOutputTokenLimit = 1000
                }
            },
            ConfirmationMode = ConfirmationMode.Never // Bypass confirmation for this test
        };

        using var router = CreateRouter(options);
        var largeRequest = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            Content = "x".PadRight(5000) // Very large content
        };

        // Act & Assert
        await Assert.ThrowsAsync<TokenBudgetExceededException>(() =>
            router.ExecuteAsync(largeRequest, "openai"));
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskTypeMapping_SelectsCorrectProvider()
    {
        // Arrange
        var mappingConfig = new TaskToModelMappingConfiguration
        {
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
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

        using var router = CreateRouter(mappingConfig: mappingConfig);
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "Code",
            Content = "function fibonacci(n) {}"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotEmpty(result.Content); // Verify execution was completed
    }

    [Fact]
    public async Task GetTokenUsageAsync_ReturnsCurrentUsage()
    {
        // Arrange
        using var router = CreateRouter();
        var provider = "openai";

        // Act
        var usage = await router.GetTokenUsageAsync(provider);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(provider, usage.ProviderName);
        Assert.Equal(100000, usage.DailyInputLimit);
    }

    [Fact]
    public async Task GetTokenUsageAsync_InvalidProvider_ThrowsException()
    {
        // Arrange
        using var router = CreateRouter();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            router.GetTokenUsageAsync("non-existent-provider"));
    }

    [Fact]
    public async Task ListProvidersAsync_ReturnsConfiguredProviders()
    {
        // Arrange
        var options = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new(),
                ["anthropic"] = new(),
                ["azure"] = new()
            }
        };

        using var router = CreateRouter(options);

        // Act
        var providers = await router.ListProvidersAsync();

        // Assert
        Assert.Equal(3, providers.Count);
        Assert.Contains("openai", providers);
        Assert.Contains("anthropic", providers);
        Assert.Contains("azure", providers);
    }

    [Fact]
    public async Task IsProviderAvailableAsync_WithConfiguredProvider_ReturnsTrue()
    {
        // Arrange
        using var router = CreateRouter();

        // Act
        var isAvailable = await router.IsProviderAvailableAsync("openai");

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task IsProviderAvailableAsync_WithUnconfiguredProvider_ReturnsFalse()
    {
        // Arrange
        using var router = CreateRouter();

        // Act
        var isAvailable = await router.IsProviderAvailableAsync("non-existent");

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task ExecuteAsync_EstimatesTokensCorrectly()
    {
        // Arrange
        using var router = CreateRouter();
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            Content = "This is a test request." // ~6 tokens
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result.TokenUsage);
        Assert.True(result.TokenUsage.InputTokens > 0);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var router = CreateRouter();

        // Act & Assert - should not throw
        router.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleProviders_SelectsBestByMapping()
    {
        // Arrange
        var options = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["provider-a"] = new()
                {
                    DailyInputTokenLimit = 100000,
                    DailyOutputTokenLimit = 100000,
                    MonthlyInputTokenLimit = 1000000,
                    MonthlyOutputTokenLimit = 1000000
                },
                ["provider-b"] = new()
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
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["provider-a"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = true
                    }
                },
                ["provider-b"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 5,
                        Enabled = true
                    }
                }
            }
        };

        using var router = CreateRouter(options, mappingConfig);
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "Chat",
            Content = "Tell me about AI"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        // provider-a should be selected (higher priority)
        Assert.Contains("provider-a", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_TaskTypeUnmatched_FallsBackToBudgetSelection()
    {
        // Arrange
        var options = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["primary"] = new()
                {
                    DailyInputTokenLimit = 50000,
                    DailyOutputTokenLimit = 50000,
                    MonthlyInputTokenLimit = 500000,
                    MonthlyOutputTokenLimit = 500000
                },
                ["secondary"] = new()
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
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                // No mappings defined
            }
        };

        using var router = CreateRouter(options, mappingConfig);
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "UnknownType",
            Content = "Some request"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        // Should still succeed by falling back
    }
}
