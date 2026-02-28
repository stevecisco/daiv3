using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for MQ-REQ-014: User confirmation based on configurable rules.
/// </summary>
public class OnlineProviderRouterConfirmationTests : IDisposable
{
    private readonly Mock<ILogger<OnlineProviderRouter>> _mockLogger;
    private readonly Mock<INetworkConnectivityService> _mockConnectivity;
    private OnlineProviderRouter _router = null!;

    public OnlineProviderRouterConfirmationTests()
    {
        _mockLogger = new Mock<ILogger<OnlineProviderRouter>>();
        _mockConnectivity = new Mock<INetworkConnectivityService>();
        
        // Default: system is online
        _mockConnectivity.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public void RequiresConfirmation_Always_ReturnsTrue()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Always);
        var request = CreateRequest("Test content");
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresConfirmation_Never_ReturnsFalse()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Never);
        var request = CreateRequest("Test content");
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresConfirmation_AboveThreshold_BelowLimit_ReturnsFalse()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 1000);
        var request = CreateRequest("Short text"); // ~2 tokens (4 chars / 2)
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresConfirmation_AboveThreshold_ExceedsLimit_ReturnsTrue()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 100);
        var request = CreateRequest(new string('a', 500)); // ~125 tokens
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresConfirmation_AutoWithinBudget_WithinBudget_ReturnsFalse()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AutoWithinBudget, dailyLimit: 10000);
        var request = CreateRequest("Short request"); // ~3 tokens
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresConfirmation_AutoWithinBudget_ExceedsBudget_ReturnsTrue()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AutoWithinBudget, dailyLimit: 50);
        var request = CreateRequest(new string('a', 500)); // ~125 tokens
        _router = CreateRouter(options);

        // Act
        var result = _router.RequiresConfirmation(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetConfirmationDetails_ReturnsCorrectEstimates()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 100, dailyLimit: 10000);
        var request = CreateRequest(new string('a', 400)); // ~100 tokens
        _router = CreateRouter(options);

        // Act
        var details = _router.GetConfirmationDetails(request);

        // Assert
        Assert.Equal("openai", details.ProviderName);
        Assert.Equal(100, details.EstimatedInputTokens);
        Assert.Equal(100, details.EstimatedOutputTokens);
        Assert.Equal(200, details.TotalEstimatedTokens);
        Assert.Equal(0, details.CurrentDailyInputTokens);
        Assert.Equal(10000, details.DailyInputLimit);
        Assert.False(details.ExceedsBudget);
    }

    [Fact]
    public void GetConfirmationDetails_Always_CorrectReason()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Always);
        var request = CreateRequest("Test");
        _router = CreateRouter(options);

        // Act
        var details = _router.GetConfirmationDetails(request);

        // Assert
        Assert.Contains("Always", details.ConfirmationReason);
    }

    [Fact]
    public void GetConfirmationDetails_AboveThreshold_CorrectReason()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 50);
        var request = CreateRequest(new string('a', 500)); // ~125 tokens
        _router = CreateRouter(options);

        // Act
        var details = _router.GetConfirmationDetails(request);

        // Assert
        Assert.Contains("exceed threshold", details.ConfirmationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("125", details.ConfirmationReason);
        Assert.Contains("50", details.ConfirmationReason);
    }

    [Fact]
    public void GetConfirmationDetails_AutoWithinBudget_ExceedsBudget_CorrectReason()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AutoWithinBudget, dailyLimit: 50);
        var request = CreateRequest(new string('a', 500)); // ~125 tokens
        _router = CreateRouter(options);

        // Act
        var details = _router.GetConfirmationDetails(request);

        // Assert
        Assert.Contains("exceed", details.ConfirmationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("budget", details.ConfirmationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Always_ReturnsAwaitingConfirmation()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Always);
        var request = CreateRequest("Test content");
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.AwaitingConfirmation, result.Status);
        Assert.Contains("confirmation required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Never_Executes()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Never);
        var request = CreateRequest("Test content");
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Contains("[STUB]", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_AboveThreshold_BelowLimit_Executes()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 1000);
        var request = CreateRequest("Short");
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AboveThreshold_ExceedsLimit_ReturnsAwaitingConfirmation()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.AboveThreshold, confirmationThreshold: 50);
        var request = CreateRequest(new string('a', 500)); // ~125 tokens
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.AwaitingConfirmation, result.Status);
    }

    [Fact]
    public async Task ExecuteWithConfirmationAsync_BypassesConfirmation_Executes()
    {
        // Arrange
        var options = CreateOptions(ConfirmationMode.Always);
        var request = CreateRequest("Test content");
        _router = CreateRouter(options);

        // Act
        var result = await _router.ExecuteWithConfirmationAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Contains("confirmed", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteWithConfirmationAsync_Offline_StillQueues()
    {
        // Arrange
        _mockConnectivity.Setup(x => x.IsOnlineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var mockQueueRepo = new Mock<IModelQueueRepository>();
        var options = CreateOptions(ConfirmationMode.Always);
        var request = CreateRequest("Test");
        _router = CreateRouter(options, mockQueueRepo.Object);

        // Act
        var result = await _router.ExecuteWithConfirmationAsync(request);

        // Assert
        Assert.Equal(ExecutionStatus.Pending, result.Status);
        mockQueueRepo.Verify(x => x.SavePendingRequestAsync(
            It.IsAny<ExecutionRequest>(),
            It.IsAny<ExecutionPriority>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private IOptions<OnlineProviderOptions> CreateOptions(
        ConfirmationMode mode,
        int confirmationThreshold = 1000,
        int dailyLimit = 10000)
    {
        var options = new OnlineProviderOptions
        {
            ConfirmationMode = mode,
            ConfirmationThreshold = confirmationThreshold,
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["openai"] = new ProviderConfig
                {
                    ApiKey = "test-key",
                    Endpoint = "https://api.openai.com",
                    DailyInputTokenLimit = dailyLimit,
                    DailyOutputTokenLimit = dailyLimit
                }
            }
        };

        return Options.Create(options);
    }

    private IOptions<TaskToModelMappingConfiguration> CreateMappingOptions()
    {
        var config = new TaskToModelMappingConfiguration
        {
            MaxConcurrentRequestsPerProvider = 5
        };

        return Options.Create(config);
    }

    private ExecutionRequest CreateRequest(string content)
    {
        return new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            Content = content,
            TaskType = "chat"
        };
    }

    private OnlineProviderRouter CreateRouter(
        IOptions<OnlineProviderOptions> options,
        IModelQueueRepository? queueRepo = null)
    {
        return new OnlineProviderRouter(
            options,
            CreateMappingOptions(),
            _mockLogger.Object,
            _mockConnectivity.Object,
            queueRepo);
    }

    public void Dispose()
    {
        _router?.Dispose();
    }
}
