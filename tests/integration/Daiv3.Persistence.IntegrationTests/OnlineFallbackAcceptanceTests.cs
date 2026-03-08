using Daiv3.Core.Settings;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Acceptance tests for ES-REQ-002: Configurable online fallback with explicit rules.
/// </summary>
/// <remarks>
/// Tests the end-to-end configuration and enforcement of online access policies
/// through the persistence layer (settings service) and policy service.
/// </remarks>
[Collection("Database")]
public sealed class OnlineFallbackAcceptanceTests : IAsyncLifetime, IDisposable
{
    private string _testDatabasePath = null!;
    private IDatabaseContext _databaseContext = null!;
    private ISettingsService _settingsService = null!;
    private IOnlineAccessPolicy _onlineAccessPolicy = null!;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    public OnlineFallbackAcceptanceTests()
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    }

    public async Task InitializeAsync()
    {
        // Create a temporary database file for testing
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_es_req_002_{Guid.NewGuid()}.db");

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
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
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

    /// <summary>
    /// AC1: When online_access_mode = "never", online access is denied.
    /// </summary>
    [Fact]
    public async Task OnlineAccessMode_Never_DeniesOnlineAccess()
    {
        // Arrange - Configure "never" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "never",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
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

        // Assert - Online access is denied
        Assert.False(decision.IsAllowed);
        Assert.Equal("never", decision.AccessMode);
        Assert.Contains("never", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC2: When online_access_mode = "ask", online access is allowed but requires confirmation.
    /// </summary>
    [Fact]
    public async Task OnlineAccessMode_Ask_AllowsWithConfirmation()
    {
        // Arrange - Configure "ask" mode
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

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act
        var decision = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);

        // Assert - Online access is allowed but requires confirmation
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal("ask", decision.AccessMode);
    }

    /// <summary>
    /// AC3: When online_access_mode = "auto_within_budget", online access is allowed (subject to budget checks).
    /// </summary>
    [Fact]
    public async Task OnlineAccessMode_AutoWithinBudget_AllowsWithConfirmation()
    {
        // Arrange - Configure "auto_within_budget" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
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

        // Assert - Online access is allowed
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal("auto_within_budget", decision.AccessMode);
    }

    /// <summary>
    /// AC4: When online_access_mode = "per_task", online access is allowed but requires per-task confirmation.
    /// </summary>
    [Fact]
    public async Task OnlineAccessMode_PerTask_AllowsWithConfirmation()
    {
        // Arrange - Configure "per_task" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "per_task",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
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

        // Assert - Online access is allowed but requires per-task confirmation
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal("per_task", decision.AccessMode);
    }

    /// <summary>
    /// AC5: When no online providers are enabled, online access is denied regardless of mode.
    /// </summary>
    [Fact]
    public async Task NoProvidersEnabled_DeniesOnlineAccess()
    {
        // Arrange - Configure "ask" mode but with no providers enabled
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "ask",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[]", // Empty array - no providers
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

        // Assert - Online access is denied
        Assert.False(decision.IsAllowed);
        Assert.Contains("no online providers", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC6: Configuration changes are immediately reflected in policy decisions.
    /// </summary>
    [Fact]
    public async Task ConfigurationChanges_ImmediatelyApplied()
    {
        // Arrange - Start with "never" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "never",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\"]",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "acceptance_test");

        var request = new ExecutionRequest
        {
            Id = Guid.NewGuid(),
            TaskType = "chat",
            Content = "Test message"
        };

        // Act & Assert - Initially denied
        var decision1 = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);
        Assert.False(decision1.IsAllowed);
        Assert.Equal("never", decision1.AccessMode);

        // Change to "ask" mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "ask",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        // Act & Assert - Now allowed with confirmation
        var decision2 = await _onlineAccessPolicy.IsOnlineAccessAllowedAsync(request);
        Assert.True(decision2.IsAllowed);
        Assert.True(decision2.RequiresConfirmation);
        Assert.Equal("ask", decision2.AccessMode);
    }

    /// <summary>
    /// AC7: Complete workflow - User configures multiple providers and access mode together.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_ConfigureMultipleProvidersAndMode()
    {
        // Arrange - Configure multiple providers and auto_within_budget mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "auto_within_budget",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "acceptance_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            "[\"openai\",\"azure_openai\",\"anthropic\"]",
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
        var providersEnabled = await _onlineAccessPolicy.AreOnlineProvidersEnabledAsync();
        var accessMode = await _onlineAccessPolicy.GetOnlineAccessModeAsync();

        // Assert - All configurations are applied
        Assert.True(decision.IsAllowed);
        Assert.True(providersEnabled);
        Assert.Equal("auto_within_budget", accessMode);
        Assert.Equal("auto_within_budget", decision.AccessMode);
    }

    public void Dispose()
    {
        // Cleanup handled by DisposeAsync
    }
}
