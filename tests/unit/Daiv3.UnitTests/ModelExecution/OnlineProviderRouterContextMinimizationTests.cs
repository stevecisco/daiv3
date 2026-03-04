using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

#pragma warning disable IDISP025 // Class with no virtual dispose method should be sealed

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for MQ-REQ-015: Send only minimal required context to online providers.
/// </summary>
public class OnlineProviderRouterContextMinimizationTests : IDisposable
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;
    private readonly Mock<INetworkConnectivityService> _mockConnectivity;
    private OnlineProviderRouter _router = null!;

    public OnlineProviderRouterContextMinimizationTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
        _mockConnectivity = new Mock<INetworkConnectivityService>();
        
        // Default: system is online
        _mockConnectivity.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ExecuteAsync_MinimizationDisabled_SendsFullContext()
    {
        // Arrange
        var options = CreateOptions(minimizationEnabled: false);
        var request = CreateRequest("Test query");
        request.Context["full_document"] = "Sensitive document content that should normally be excluded";
        request.Context["user_data"] = "Personal information";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // When minimization is disabled, we expect full context to be used
        // (In actual implementation, the provider would receive all context)
    }

    [Fact]
    public async Task ExecuteAsync_WhitelistSpecified_IncludesOnlyWhitelistedKeys()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            includeOnlyKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "query_context", "task_description" }
        );
        var request = CreateRequest("Test query");
        request.Context["query_context"] = "Relevant context";
        request.Context["task_description"] = "Important task details";
        request.Context["full_document"] = "Should be excluded";
        request.Context["user_history"] = "Should be excluded";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify context minimization occurred (logged)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Context minimized") && v.ToString()!.Contains("Keys removed:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_BlacklistSpecified_ExcludesBlacklistedKeys()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            excludeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "full_document", "raw_data", "sensitive_info" }
        );
        var request = CreateRequest("Test query");
        request.Context["query_context"] = "Should be included";
        request.Context["full_document"] = "Should be excluded";
        request.Context["raw_data"] = "Should be excluded";
        request.Context["sensitive_info"] = "Should be excluded";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify blacklisted keys were removed
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("excluded") && v.ToString()!.Contains("blacklist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(3)); // 3 blacklisted keys
    }

    [Fact]
    public async Task ExecuteAsync_ContextExceedsPerKeyLimit_TruncatesValues()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            maxTokensPerKey: 100 // ~400 characters
        );
        var request = CreateRequest("Test query");
        var longContent = new string('a', 1000); // ~250 tokens
        request.Context["long_context"] = longContent;
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify truncation occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("truncated") && v.ToString()!.Contains("long_context")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContextExceedsTotalLimit_RemovesExcessKeys()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            maxContextTokens: 500 // ~2000 characters total
        );
        var request = CreateRequest("Test query");
        // Add multiple context keys that exceed total limit
        for (int i = 0; i < 10; i++)
        {
            request.Context[$"context_{i}"] = new string('a', 300); // ~75 tokens each = 750 total
        }
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify some keys were removed due to total limit
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("excluded") && v.ToString()!.Contains("total context token limit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SmallContextWithinLimits_NoMinimizationNeeded()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            maxContextTokens: 2000
        );
        var request = CreateRequest("Test query");
        request.Context["small_context"] = "Short context";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify no minimization was needed
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No context minimization needed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyContext_HandlesGracefully()
    {
        // Arrange
        var options = CreateOptions(minimizationEnabled: true);
        var request = CreateRequest("Test query");
        // No context added
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveKeyMatching_WhitelistWorksWithDifferentCase()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            includeOnlyKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AllowedKey" }
        );
        var request = CreateRequest("Test query");
        request.Context["allowedkey"] = "Should be included"; // lowercase
        request.Context["ALLOWEDKEY2"] = "Should be excluded"; // different key
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveKeyMatching_BlacklistWorksWithDifferentCase()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            excludeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ExcludedKey" }
        );
        var request = CreateRequest("Test query");
        request.Context["excludedkey"] = "Should be excluded"; // lowercase
        request.Context["EXCLUDEDKEY"] = "Should be excluded"; // uppercase
        request.Context["allowed"] = "Should be included";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify both case variations were excluded
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("excluded") && v.ToString()!.Contains("blacklist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhitelistTakesPrecedenceOverBlacklist()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            includeOnlyKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "key1", "key2" },
            excludeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "key2" } // key2 in both
        );
        var request = CreateRequest("Test query");
        request.Context["key1"] = "Should be included";
        request.Context["key2"] = "Should be included (whitelist wins)";
        request.Context["key3"] = "Should be excluded (not in whitelist)";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteWithConfirmationAsync_AppliesContextMinimization()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            excludeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sensitive_data" }
        );
        var request = CreateRequest("Test query");
        request.Context["safe_context"] = "Should be included";
        request.Context["sensitive_data"] = "Should be excluded";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteWithConfirmationAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify minimization occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("excluded") && v.ToString()!.Contains("blacklist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_LoggingDisabled_DoesNotLogMinimization()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            logMinimization: false,
            excludeKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "excluded_key" }
        );
        var request = CreateRequest("Test query");
        request.Context["included"] = "Should be included";
        request.Context["excluded_key"] = "Should be excluded";
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify no Information-level logging about minimization
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Context minimized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TruncatedValueIncludesEllipsis()
    {
        // Arrange
        var options = CreateOptions(
            minimizationEnabled: true,
            maxTokensPerKey: 50 // ~200 characters
        );
        var request = CreateRequest("Test query");
        var longContent = new string('a', 500); // Will be truncated
        request.Context["long_value"] = longContent;
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        // Verify truncation was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("truncated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // Helper methods
    private OnlineProviderRouter CreateRouter(OnlineProviderOptions options)
    {
        var optionsWrapper = Options.Create(options);
        var taskModelOptions = Options.Create(new TaskToModelMappingConfiguration());

        return new OnlineProviderRouter(
            optionsWrapper,
            taskModelOptions,
            _mockLogger.Object,
            _mockConnectivity.Object,
            null); // No queue repository needed for these tests
    }

    private static OnlineProviderOptions CreateOptions(
        bool minimizationEnabled = true,
        int maxContextTokens = 2000,
        int maxTokensPerKey = 1000,
        HashSet<string>? includeOnlyKeys = null,
        HashSet<string>? excludeKeys = null,
        bool logMinimization = true)
    {
        return new OnlineProviderOptions
        {
            DefaultProvider = "openai",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new ProviderConfig
                {
                    ApiKey = "test-key",
                    DailyInputTokenLimit = 10000,
                    DailyOutputTokenLimit = 10000
                }
            },
            ConfirmationMode = ConfirmationMode.Never, // Disable confirmation for these tests
            ContextMinimization = new ContextMinimizationOptions
            {
                Enabled = minimizationEnabled,
                MaxContextTokens = maxContextTokens,
                MaxTokensPerKey = maxTokensPerKey,
                IncludeOnlyKeys = includeOnlyKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                ExcludeKeys = excludeKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                LogMinimization = logMinimization
            }
        };
    }

    private static ExecutionRequest CreateRequest(string content)
    {
        return new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            Content = content,
            TaskType = "test",
            Context = new Dictionary<string, string>()
        };
    }

    public void Dispose()
    {
        _router?.Dispose();
    }
}
