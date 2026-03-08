using Daiv3.ModelExecution.Models;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Unit tests for ES-REQ-002: OnlineAccessPolicyService enforces configured online access rules.
/// </summary>
public sealed class OnlineAccessPolicyServiceTests : IDisposable
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<OnlineAccessPolicyService>> _mockLogger;
    private readonly OnlineAccessPolicyService _policyService;

    public OnlineAccessPolicyServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<OnlineAccessPolicyService>>();
        _policyService = new OnlineAccessPolicyService(_mockSettingsService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_NeverMode_DeniesAccess()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("never");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains("never", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("never", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_AskMode_AllowsWithConfirmation()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ask");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Contains("ask", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ask", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_AutoWithinBudgetMode_AllowsWithConfirmation()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("auto_within_budget");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Contains("auto_within_budget", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("auto_within_budget", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_PerTaskMode_AllowsWithConfirmation()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("per_task");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Contains("per_task", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("per_task", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_NoProvidersEnabled_DeniesAccess()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ask");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]"); // Empty array

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains("no online providers", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_InvalidJson_DeniesAccess()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ask");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-valid-json");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task GetOnlineAccessModeAsync_ReturnsConfiguredMode()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("never");

        // Act
        var mode = await _policyService.GetOnlineAccessModeAsync();

        // Assert
        Assert.Equal("never", mode);
    }

    [Fact]
    public async Task GetOnlineAccessModeAsync_NullSetting_ReturnsDefault()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var mode = await _policyService.GetOnlineAccessModeAsync();

        // Assert
        Assert.Equal("ask", mode); // Default from ApplicationSettings.Defaults
    }

    [Fact]
    public async Task AreOnlineProvidersEnabledAsync_WithProviders_ReturnsTrue()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\",\"anthropic\"]");

        // Act
        var enabled = await _policyService.AreOnlineProvidersEnabledAsync();

        // Assert
        Assert.True(enabled);
    }

    [Fact]
    public async Task AreOnlineProvidersEnabledAsync_EmptyArray_ReturnsFalse()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        // Act
        var enabled = await _policyService.AreOnlineProvidersEnabledAsync();

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public async Task AreOnlineProvidersEnabledAsync_NullSetting_ReturnsFalse()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var enabled = await _policyService.AreOnlineProvidersEnabledAsync();

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_UnknownMode_DefaultsToAsk()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("unknown_mode");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Contains("unknown", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ES-ACC-001: force_offline_mode tests
    /// </summary>
    [Fact]
    public async Task IsOnlineAccessAllowedAsync_ForceOfflineEnabled_DeniesAccessRegardlessOfMode()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<bool>("force_offline_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("auto_within_budget"); // Even with permissive mode
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains("force offline", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("force_offline", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_ForceOfflineDisabled_ChecksNormalPolicy()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<bool>("force_offline_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ask");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal("ask", decision.AccessMode);
    }

    [Fact]
    public async Task IsOnlineAccessAllowedAsync_ForceOfflineNotSet_DefaultsToFalse()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<bool>("force_offline_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(bool)); // Default is false
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_access_mode", It.IsAny<CancellationToken>()))
            .ReturnsAsync("auto_within_budget");
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("online_providers_enabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\"]");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test"
        };

        // Act
        var decision = await _policyService.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal("auto_within_budget", decision.AccessMode);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
