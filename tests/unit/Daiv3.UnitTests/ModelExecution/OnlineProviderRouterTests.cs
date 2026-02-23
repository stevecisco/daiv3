using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Exceptions;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

public class OnlineProviderRouterTests
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;
    private readonly OnlineProviderOptions _options;

    public OnlineProviderRouterTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
        _options = new OnlineProviderOptions
        {
            DefaultProvider = "openai",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new ProviderConfig
                {
                    ApiKey = "test-key",
                    Endpoint = "https://api.openai.com",
                    DailyInputTokenLimit = 10000,
                    DailyOutputTokenLimit = 10000,
                    MonthlyInputTokenLimit = 300000,
                    MonthlyOutputTokenLimit = 300000
                },
                ["azure-openai"] = new ProviderConfig
                {
                    ApiKey = "azure-key",
                    Endpoint = "https://azure.openai.com",
                    DailyInputTokenLimit = 50000,
                    DailyOutputTokenLimit = 50000
                }
            }
        };
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsResult()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "online-chat",
            Content = "Hello, GPT!"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Id, result.RequestId);
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_SpecificProvider_UsesProvider()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        var result = await router.ExecuteAsync(request, "azure-openai");

        // Assert
        Assert.NotNull(result);
        Assert.Contains( "azure-openai", result.Content);
    }

    [Fact]
    public async Task GetTokenUsageAsync_ValidProvider_ReturnsUsage()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act
        var usage = await router.GetTokenUsageAsync("openai");

        // Assert
        Assert.NotNull(usage);
        Assert.Equal("openai", usage.ProviderName);
        Assert.Equal(10000, usage.DailyInputLimit);
    }

    [Fact]
    public async Task GetTokenUsageAsync_InvalidProvider_ThrowsArgumentException()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => router.GetTokenUsageAsync("invalid-provider"));
    }

    [Fact]
    public async Task ListProvidersAsync_ReturnsConfiguredProviders()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act
        var providers = await router.ListProvidersAsync();

        // Assert
        Assert.Contains("openai", providers);
        Assert.Contains("azure-openai", providers);
        Assert.Equal(2, providers.Count);
    }

    [Fact]
    public async Task IsProviderAvailableAsync_ConfiguredProvider_ReturnsTrue()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act
        var isAvailable = await router.IsProviderAvailableAsync("openai");

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task IsProviderAvailableAsync_UnconfiguredProvider_ReturnsFalse()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act
        var isAvailable = await router.IsProviderAvailableAsync("anthropic");

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task ExecuteAsync_TracksTokenUsage()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test message" };

        // Act
        await router.ExecuteAsync(request, "openai");
        var usage = await router.GetTokenUsageAsync("openai");

        // Assert
        Assert.True(usage.DailyInputTokens > 0);
        Assert.True(usage.DailyOutputTokens > 0);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedingBudget_ThrowsTokenBudgetExceededException()
    {
        // Arrange  
        var limitedOptions = new OnlineProviderOptions
        {
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["limited"] = new ProviderConfig
                {
                    DailyInputTokenLimit = 10, // Very low limit
                    DailyOutputTokenLimit = 10
                }
            }
        };

        var router = new OnlineProviderRouter(Options.Create(limitedOptions), _mockLogger.Object);
        var request = new ExecutionRequest
        {
            TaskType = "chat",
            Content = "This is a very long message that will exceed the token budget limit and trigger an exception"
        };

        // Act & Assert
        await Assert.ThrowsAsync<TokenBudgetExceededException>(() => router.ExecuteAsync(request, "limited"));
    }

    [Fact]
    public async Task ExecuteAsync_MultipleRequests_AccumulatesTokenUsage()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var request1 = new ExecutionRequest { TaskType = "chat", Content = "First message" };
        var request2 = new ExecutionRequest { TaskType = "chat", Content = "Second message" };

        // Act
        await router.ExecuteAsync(request1, "openai");
        var usage1 = await router.GetTokenUsageAsync("openai");

        await router.ExecuteAsync(request2, "openai");
        var usage2 = await router.GetTokenUsageAsync("openai");

        // Assert
        Assert.True(usage2.DailyInputTokens > usage1.DailyInputTokens);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentProviders_TrackedSeparately()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var request = new ExecutionRequest { TaskType = "chat", Content = "Test" };

        // Act
        await router.ExecuteAsync(request, "openai");
        await router.ExecuteAsync(request, "azure-openai");

        var openaiUsage = await router.GetTokenUsageAsync("openai");
        var azureUsage = await router.GetTokenUsageAsync("azure-openai");

        // Assert
        Assert.True(openaiUsage.DailyInputTokens > 0);
        Assert.True(azureUsage.DailyInputTokens > 0);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesRequestId()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);
        var requestId = Guid.NewGuid();
        var request = new ExecutionRequest
        {
            Id = requestId,
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var result = await router.ExecuteAsync(request);

        // Assert
        Assert.Equal(requestId, result.RequestId);
    }

    [Fact]
    public void Constructor_InitializesTokenTracking()
    {
        // Arrange & Act
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Assert - router created without exceptions
        Assert.NotNull(router);
    }

    [Fact]
    public async Task GetTokenUsageAsync_NewlyCreated_ReturnsZeroUsage()
    {
        // Arrange
        var router = new OnlineProviderRouter(Options.Create(_options), _mockLogger.Object);

        // Act
        var usage = await router.GetTokenUsageAsync("openai");

        // Assert
        Assert.Equal(0, usage.DailyInputTokens);
        Assert.Equal(0, usage.DailyOutputTokens);
        Assert.Equal(0, usage.MonthlyInputTokens);
        Assert.Equal(0, usage.MonthlyOutputTokens);
    }
}
