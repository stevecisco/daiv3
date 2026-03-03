using Daiv3.OnlineProviders.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Tests.Unit.OnlineProviders;

/// <summary>
/// Unit tests for IOnlineProvider and related abstractions.
/// </summary>
public class OnlineProviderAbstractionsTests
{
    /// <summary>
    /// Tests that OnlineInferenceOptions initializes with defaults.
    /// </summary>
    [Fact]
    public void OnlineInferenceOptions_InitializesWithDefaults()
    {
        // Arrange & Act
        var options = new OnlineInferenceOptions();

        // Assert
        Assert.Equal("gpt-4", options.Model);
        Assert.Equal(2048, options.MaxTokens);
        Assert.Equal(0.7m, options.Temperature);
        Assert.Empty(options.SystemPrompts);
        Assert.Null(options.TopP);
        Assert.Null(options.FrequencyPenalty);
        Assert.Null(options.PresencePenalty);
    }

    /// <summary>
    /// Tests that OnlineInferenceOptions can be configured with custom values.
    /// </summary>
    [Fact]
    public void OnlineInferenceOptions_AcceptsCustomConfiguration()
    {
        // Arrange & Act
        var options = new OnlineInferenceOptions
        {
            Model = "gpt-4-turbo",
            MaxTokens = 4096,
            Temperature = 0.5m,
            TopP = 0.95m,
            FrequencyPenalty = 0.5m,
            PresencePenalty = 0.5m,
            SystemPrompts = new List<string> { "You are a helpful assistant." }
        };

        // Assert
        Assert.Equal("gpt-4-turbo", options.Model);
        Assert.Equal(4096, options.MaxTokens);
        Assert.Equal(0.5m, options.Temperature);
        Assert.Equal(0.95m, options.TopP);
        Assert.Equal(0.5m, options.FrequencyPenalty);
        Assert.Equal(0.5m, options.PresencePenalty);
        Assert.Single(options.SystemPrompts);
    }

    /// <summary>
    /// Tests that ProviderTokenUsage initializes with zero values.
    /// </summary>
    [Fact]
    public void ProviderTokenUsage_InitializesWithZeroes()
    {
        // Arrange & Act
        var usage = new ProviderTokenUsage();

        // Assert
        Assert.Equal(0L, usage.InputTokens);
        Assert.Equal(0L, usage.OutputTokens);
        Assert.Equal(0L, usage.TotalTokens);
        Assert.NotEqual(DateTimeOffset.MinValue, usage.LastUpdated);
    }

    /// <summary>
    /// Tests that ProviderTokenUsage can aggregate token counts.
    /// </summary>
    [Fact]
    public void ProviderTokenUsage_AggregatesTokenCounts()
    {
        // Arrange
        var usage = new ProviderTokenUsage();

        // Act
        usage.InputTokens = 1000;
        usage.OutputTokens = 500;
        usage.TotalTokens = 1500;

        // Assert
        Assert.Equal(1000L, usage.InputTokens);
        Assert.Equal(500L, usage.OutputTokens);
        Assert.Equal(1500L, usage.TotalTokens);
    }

    /// <summary>
    /// Tests that OnlineProviderBase validates temperature option.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ValidatesTemperatureRange()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions { Temperature = 3m }; // Invalid: > 2

        // Act & Assert
        Assert.Throws<ArgumentException>(() => provider.ValidateOptionsPublic(options));
    }

    /// <summary>
    /// Tests that OnlineProviderBase validates model is specified.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ValidatesModelIsSpecified()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions { Model = "" }; // Invalid: empty

        // Act & Assert
        Assert.Throws<ArgumentException>(() => provider.ValidateOptionsPublic(options));
    }

    /// <summary>
    /// Tests that OnlineProviderBase validates MaxTokens range.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ValidatesMaxTokensRange()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions { MaxTokens = 0 }; // Invalid: < 1

        // Act & Assert
        Assert.Throws<ArgumentException>(() => provider.ValidateOptionsPublic(options));
    }

    /// <summary>
    /// Tests that OnlineProviderBase validates TopP range.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ValidatesTopPRange()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions { TopP = 1.5m }; // Invalid: > 1

        // Act & Assert
        Assert.Throws<ArgumentException>(() => provider.ValidateOptionsPublic(options));
    }

    /// <summary>
    /// Tests that OnlineProviderBase accepts valid options.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_AcceptsValidOptions()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions
        {
            Model = "gpt-4",
            MaxTokens = 2048,
            Temperature = 0.7m,
            TopP = 0.9m
        };

        // Act & Assert (no exception thrown)
        provider.ValidateOptionsPublic(options);
    }

    /// <summary>
    /// Tests that OnlineProviderBase updates token usage.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_UpdatesTokenUsage()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);

        // Act
        provider.UpdateTokenUsagePublic(1000, 500);
        provider.UpdateTokenUsagePublic(500, 250);

        // Assert
        var usage = provider.GetTokenUsagePublic();
        Assert.Equal(1500L, usage.InputTokens);
        Assert.Equal(750L, usage.OutputTokens);
        Assert.Equal(2250L, usage.TotalTokens);
    }

    /// <summary>
    /// Tests that OnlineProviderBase returns null for context window by default.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ContextWindowDefaultsToNull()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);

        // Act
        var contextWindow = provider.GetContextWindowSize("any-model");

        // Assert
        Assert.Null(contextWindow);
    }

    /// <summary>
    /// Tests that OnlineProviderBase checks availability.
    /// </summary>
    [Fact]
    public async Task OnlineProviderBase_ChecksAvailability()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable); // Test implementation always returns true
    }

    /// <summary>
    /// Tests that OnlineProviderBase provides ProviderName.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ProvidesProviderName()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);

        // Act & Assert
        Assert.Equal("test-provider", provider.ProviderName);
    }

    /// <summary>
    /// Tests that OnlineProviderBase provides ChatClient access.
    /// </summary>
    [Fact]
    public void OnlineProviderBase_ProvidesChatClientAccess()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);

        // Act & Assert
        Assert.NotNull(provider.ChatClient);
    }

    /// <summary>
    /// Tests that GenerateAsync returns expected output.
    /// </summary>
    [Fact]
    public async Task OnlineProviderBase_GenerateAsyncReturnsOutput()
    {
        // Arrange
        var logger = new Mock<ILogger<OnlineProviderBase>>();
        var provider = new TestOnlineProvider(logger.Object);
        var options = new OnlineInferenceOptions { Model = "test-model" };

        // Act
        var result = await provider.GenerateAsync("test prompt", options);

        // Assert
        Assert.Contains("test prompt", result);
    }
}

/// <summary>
/// Test implementation of OnlineProviderBase for testing abstract functionality.
/// </summary>
public class TestOnlineProvider : OnlineProviderBase
{
    private readonly IChatClient _chatClient;

    public override string ProviderName => "test-provider";

    public override IChatClient ChatClient => _chatClient;

    public TestOnlineProvider(ILogger<OnlineProviderBase> logger)
        : base(logger)
    {
        // Create a mock ChatClient using Moq
        var mockClient = new Mock<IChatClient>();
        _chatClient = mockClient.Object;
    }

    public override Task<string> GenerateAsync(
        string prompt,
        OnlineInferenceOptions options,
        CancellationToken ct = default)
    {
        ValidateOptions(options);
        return Task.FromResult($"[Test] {prompt}");
    }

    // Expose protected methods for testing
    public void ValidateOptionsPublic(OnlineInferenceOptions options)
    {
        ValidateOptions(options);
    }

    public void UpdateTokenUsagePublic(int input, int output)
    {
        UpdateTokenUsage(input, output);
    }

    public ProviderTokenUsage GetTokenUsagePublic()
    {
        return _tokenUsage;
    }
}
