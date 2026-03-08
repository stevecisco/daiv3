using Daiv3.Core.Settings;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP001, IDISP002 // Test fixture members disposed in DisposeAsync via IAsyncLifetime

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Acceptance tests for ES-ACC-002: Users can enable online providers and must see usage/budget indicators.
/// </summary>
/// <remarks>
/// Tests the end-to-end workflow of enabling online providers and verifying they can be used by the system.
/// Covers provider configuration, policy enforcement, and graceful degradation when no providers configured.
/// </remarks>
[Collection("Database")]
public sealed class OnlineProviderAcceptanceTests : IAsyncLifetime, IDisposable
{
    private string _testDatabasePath = null!;
    private IDatabaseContext _databaseContext = null!;
    private ISettingsService _settingsService = null!;
    private IOnlineAccessPolicy _onlineAccessPolicy = null!;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    public OnlineProviderAcceptanceTests()
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    }

    public async Task InitializeAsync()
    {
        // Dispose previous context if exists (prevents re-assignment without disposal)
        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
        }

        // Create a temporary database file for testing
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_es_acc_002_{Guid.NewGuid()}.db");

        // Setup persistence options
        var options = new PersistenceOptions
        {
            DatabasePath = _testDatabasePath,
            EnableWAL = true,
            BusyTimeout = 5000
        };

        // Create database context and initialize
        var dbLogger = _loggerFactory.CreateLogger<DatabaseContext>();
        _databaseContext = new DatabaseContext(dbLogger, Microsoft.Extensions.Options.Options.Create(options));
        await _databaseContext.InitializeAsync();

        // Create repository and services
        var repoLogger = _loggerFactory.CreateLogger<SettingsRepository>();
        var repository = new SettingsRepository(_databaseContext, repoLogger);

        var serviceLogger = _loggerFactory.CreateLogger<SettingsService>();
        var validatorLogger = _loggerFactory.CreateLogger<SettingsValidator>();
        var policyLogger = _loggerFactory.CreateLogger<OnlineAccessPolicyService>();

        var settingsValidator = new SettingsValidator(validatorLogger);
        _settingsService = new SettingsService(repository, settingsValidator, serviceLogger);
        _onlineAccessPolicy = new OnlineAccessPolicyService(_settingsService, policyLogger);

        // Initialize required settings
        await SetupRequiredSettingsAsync();
    }

    private async Task SetupRequiredSettingsAsync()
    {
        // Setup the online access mode with default value  
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            ApplicationSettings.Defaults.OnlineAccessMode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "test_setup");
        
        // Setup online providers enabled with default value
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            ApplicationSettings.Defaults.OnlineProvidersEnabled,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "test_setup");
        
        // Setup force offline mode with default value
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.ForceOfflineMode,
            ApplicationSettings.Defaults.ForceOfflineMode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.ForceOfflineMode,
            reason: "test_setup");
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        // Dispose IDisposable members (context and logger factory)
        if (_databaseContext is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_databaseContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _loggerFactory?.Dispose();

        _disposed = true;

        // Wait for DB locks to be released
        await Task.Delay(100);

        // Delete test database
        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch
            {
                // Ignore deletion failures
            }
        }
    }

    public void Dispose()
    {
        // Handle synchronous disposal for IDisposable compliance
        // xUnit calls Dispose after DisposeAsync, so we clean up in DisposeAsync
        // This is required for IDisposable contract even with IAsyncLifetime
    }

    /// <summary>
    /// AC1: Enable single online provider (OpenAI) via settings.
    /// Provider should be enabled and available for routing.
    /// </summary>
    [Fact]
    public async Task EnableSingleProvider_OpenAI_ConfiguresAndRoutesCorrectly()
    {
        // Arrange - Enable OpenAI provider
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);
        var enabledProvidersEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.NotNull(enabledProvidersEntity);
        Assert.NotNull(enabledProvidersEntity.SettingValue);
        Assert.Contains("openai", enabledProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC2: Enable multiple online providers (OpenAI, Azure OpenAI, Anthropic).
    /// All providers should be available for routing.
    /// </summary>
    [Fact]
    public async Task EnableMultipleProviders_AllConfigured_AllRoutable()
    {
        // Arrange - Enable multiple providers
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\",\"azure-openai\",\"anthropic\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);
        var enabledProvidersEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.NotNull(enabledProvidersEntity);
        Assert.NotNull(enabledProvidersEntity.SettingValue);
        Assert.Contains("openai", enabledProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("azure-openai", enabledProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic", enabledProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC3: Set online access mode to "auto_within_budget".
    /// System should allow online routing without confirmation.
    /// </summary>
    [Fact]
    public async Task OnlineAccessMode_AutoWithinBudget_AllowsRoutingWithoutConfirmation()
    {
        // Arrange
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal("auto_within_budget", decision.AccessMode);
    }

    /// <summary>
    /// AC8: No providers configured should gracefully degrade.
    /// System should not crash and should disable online routing.
    /// </summary>
    [Fact]
    public async Task NoProvidersConfigured_GracefullyDegrades()
    {
        // Arrange - Empty provider list
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert - Should deny access gracefully
        Assert.False(decision.IsAllowed);
        Assert.Contains("provider", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC9: Force offline mode suppresses online providers.
    /// Even with providers enabled, system should deny online routing.
    /// </summary>
    [Fact]
    public async Task ForceOfflineMode_SupressesOnlineProviders()
    {
        // Arrange - Enable providers but force offline mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.ForceOfflineMode,
            true,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.ForceOfflineMode,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains("offline", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC7 (Workflow): Enable providers, verify they're stored, retrieve and confirm.
    /// Full end-to-end configuration workflow.
    /// </summary>
    [Fact]
    public async Task EndToEnd_EnableProviders_ConfigurationPersists()
    {
        // Arrange & Act - Enable providers
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\",\"anthropic\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        // Act - Retrieve settings
        var providersSettingsEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);
        var accessModeSettingsEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode);

        // Assert - Configuration persists and is correct
        Assert.NotNull(providersSettingsEntity);
        Assert.NotNull(providersSettingsEntity.SettingValue);
        Assert.NotNull(accessModeSettingsEntity);
        Assert.NotNull(accessModeSettingsEntity.SettingValue);
        Assert.Contains("openai", providersSettingsEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic", providersSettingsEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("auto_within_budget", accessModeSettingsEntity.SettingValue);

        // Act - Check if online access is allowed
        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert - Policy enforces the configuration
        Assert.True(decision.IsAllowed);
    }

    /// <summary>
    /// Test that provider configuration can be updated (e.g., add/remove providers).
    /// </summary>
    [Fact]
    public async Task UpdateProviderConfiguration_AddsAndRemovesProviders()
    {
        // Arrange - Initial configuration with OpenAI
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        // Act - Verify initial state
        var initialProvidersEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);
        Assert.NotNull(initialProvidersEntity?.SettingValue);
        Assert.Contains("openai", initialProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);

        // Act - Update to add Anthropic
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\",\"anthropic\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        var updatedProvidersEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);

        // Assert
        Assert.NotNull(updatedProvidersEntity?.SettingValue);
        Assert.Contains("openai", updatedProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic", updatedProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);

        // Act - Update to remove OpenAI
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"anthropic\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        var finalProvidersEntity = await _settingsService.GetSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled);

        // Assert
        Assert.NotNull(finalProvidersEntity?.SettingValue);
        Assert.DoesNotContain("openai", finalProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic", finalProvidersEntity.SettingValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test that access mode changes take effect immediately.
    /// </summary>
    [Fact]
    public async Task AccessModeChange_TakesEffectImmediately()
    {
        // Arrange - Start with "ask" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "ask",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        var request = new ExecutionRequest { Id = Guid.NewGuid(), TaskType = "chat", Content = "test" };

        // Act - Check initial decision (ask mode requires confirmation)
        var initialDecision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert
        Assert.True(initialDecision.IsAllowed);
        Assert.True(initialDecision.RequiresConfirmation);

        // Act - Change to "never" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "never",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        var newRequest = new ExecutionRequest { Id = Guid.NewGuid(), TaskType = "chat", Content = "test" };
        var updatedDecision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(newRequest);

        // Assert - Decision should change immediately
        Assert.False(updatedDecision.IsAllowed);
    }
}
