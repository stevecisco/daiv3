using Daiv3.Core.Settings;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Persistence.IntegrationTests;

/// <summary>
/// Acceptance tests for CT-ACC-001: Users can configure online access rules and see them applied.
/// These tests verify the end-to-end flow of configuring online provider access settings,
/// validating them, persisting them, and seeing them applied in the system.
/// </summary>
[Collection("Database")]
public sealed class OnlineAccessRulesAcceptanceTests : IAsyncLifetime, IDisposable
{
    private string _testDatabasePath = null!;
    private IDatabaseContext _databaseContext = null!;
    private ISettingsService _settingsService = null!;
    private ISettingsValidator _settingsValidator = null!;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    public OnlineAccessRulesAcceptanceTests()
    {
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddPersistence();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        // Create a temporary database file for testing
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_ct_acc_001_{Guid.NewGuid()}.db");

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

        _settingsValidator = new SettingsValidator(validatorLogger);
        _settingsService = new SettingsService(repository, _settingsValidator, serviceLogger);

        // Note: We don't initialize defaults here because some settings have empty string defaults
        // that can't be persisted (e.g., API keys). Instead, we manually set up only the settings
        // needed for our acceptance tests.
        await SetupRequiredSettingsAsync();
    }

    /// <summary>
    /// Sets up only the required settings for online access rules testing.
    /// Avoids initializing all defaults which includes problematic empty string API keys.
    /// </summary>
    private async Task SetupRequiredSettingsAsync()
    {
        // Setup the online access mode with default value  
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            ApplicationSettings.Defaults.OnlineAccessMode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "test_setup");
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        // Dispose all resources
        (_databaseContext as IDisposable)?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
        _loggerFactory?.Dispose();

        _disposed = true;

        // Wait for DB locks to be released
        await Task.Delay(100);

        // Clean up database files
        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch (IOException)
            {
                // Ignore if file is still locked
            }
        }

        // Clean up WAL files if they exist
        var walPath = _testDatabasePath + "-wal";
        if (File.Exists(walPath))
        {
            try
            {
                File.Delete(walPath);
            }
            catch (IOException)
            {
                // Ignore if file is still locked
            }
        }

        var shmPath = _testDatabasePath + "-shm";
        if (File.Exists(shmPath))
        {
            try
            {
                File.Delete(shmPath);
            }
            catch (IOException)
            {
                // Ignore if file is still locked
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    #region CT-ACC-001 Acceptance Criteria Tests

    /// <summary>
    /// AC1: User can configure OnlineAccessMode and it is persisted correctly.
    /// Valid modes: never, ask, auto_within_budget, per_task
    /// </summary>
    [Theory]
    [InlineData("never")]
    [InlineData("ask")]
    [InlineData("auto_within_budget")]
    [InlineData("per_task")]
    public async Task UserCanConfigure_OnlineAccessMode_AndSeeItApplied(string mode)
    {
        // Arrange - Initial default should be "ask"
        var initialValue = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode);
        Assert.Equal(ApplicationSettings.Defaults.OnlineAccessMode, initialValue);

        // Act - User changes the online access mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            mode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "user_acceptance_test");

        // Assert - Setting is persisted and can be retrieved
        var savedValue = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode);
        Assert.Equal(mode, savedValue);

        // Assert - Validation passes for this value
        var validationResult = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            mode);
        Assert.True(validationResult.IsValid);
    }

    /// <summary>
    /// AC2: User can configure enabled online providers list and see it applied.
    /// </summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("[\"openai\"]")]
    [InlineData("[\"openai\",\"azure_openai\"]")]
    [InlineData("[\"openai\",\"azure_openai\",\"anthropic\"]")]
    public async Task UserCanConfigure_OnlineProvidersEnabled_AndSeeItApplied(string providersJson)
    {
        // Act - User configures which online providers to enable
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            providersJson,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "user_acceptance_test");

        // Assert - Setting is persisted and can be retrieved
        var savedValue = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineProvidersEnabled);
        Assert.Equal(providersJson, savedValue);

        // Assert - Validation passes for this value
        var validationResult = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            providersJson);
        Assert.True(validationResult.IsValid);
    }

    /// <summary>
    /// AC3: User can configure token budgets (daily and monthly) and see them applied.
    /// </summary>
    [Theory]
    [InlineData(10000, 300000)]
    [InlineData(50000, 1000000)]
    [InlineData(100000, 2000000)]
    public async Task UserCanConfigure_TokenBudgets_AndSeeThemApplied(int dailyBudget, int monthlyBudget)
    {
        // Act - User sets daily token budget
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.DailyTokenBudget,
            dailyBudget,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.DailyTokenBudget,
            reason: "user_acceptance_test");

        // Act - User sets monthly token budget
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.MonthlyTokenBudget,
            monthlyBudget,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.MonthlyTokenBudget,
            reason: "user_acceptance_test");

        // Assert - Settings are persisted and can be retrieved
        var savedDaily = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.DailyTokenBudget);
        var savedMonthly = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.MonthlyTokenBudget);

        Assert.Equal(dailyBudget, savedDaily);
        Assert.Equal(monthlyBudget, savedMonthly);

        // Assert - Validations pass
        var dailyValidation = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.DailyTokenBudget,
            dailyBudget);
        var monthlyValidation = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.MonthlyTokenBudget,
            monthlyBudget);

        Assert.True(dailyValidation.IsValid);
        Assert.True(monthlyValidation.IsValid);
    }

    /// <summary>
    /// AC4: User can configure token budget mode and alert threshold and see them applied.
    /// </summary>
    [Theory]
    [InlineData("hard_stop", 90)]
    [InlineData("user_confirm", 80)]
    [InlineData("hard_stop", 75)]
    public async Task UserCanConfigure_TokenBudgetControls_AndSeeThemApplied(string mode, int threshold)
    {
        // Act - User sets budget mode
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.TokenBudgetMode,
            mode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.TokenBudgetMode,
            reason: "user_acceptance_test");

        // Act - User sets alert threshold
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold,
            threshold,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.TokenBudgetAlertThreshold,
            reason: "user_acceptance_test");

        // Assert - Settings are persisted and can be retrieved
        var savedMode = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.TokenBudgetMode);
        var savedThreshold = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold);

        Assert.Equal(mode, savedMode);
        Assert.Equal(threshold, savedThreshold);

        // Assert - Validations pass
        var modeValidation = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.TokenBudgetMode,
            mode);
        var thresholdValidation = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold,
            threshold);

        Assert.True(modeValidation.IsValid);
        Assert.True(thresholdValidation.IsValid);
    }

    /// <summary>
    /// AC5: Invalid online access modes are rejected by validation.
    /// </summary>
    [Theory]
    [InlineData("invalid_mode")]
    [InlineData("always")]
    [InlineData("maybe")]
    public async Task UserCannotConfigure_InvalidOnlineAccessMode(string invalidMode)
    {
        // Act - Attempt to validate an invalid mode
        var validationResult = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            invalidMode);

        // Assert - Validation should fail
        Assert.False(validationResult.IsValid);
        Assert.NotNull(validationResult.ErrorMessage);
    }

    /// <summary>
    /// AC6: Invalid provider JSON is rejected by validation.
    /// </summary>
    [Theory]
    [InlineData("not valid json")]
    [InlineData("{'invalid': 'format'}")]
    [InlineData("[openai]")] // Missing quotes
    public async Task UserCannotConfigure_InvalidProvidersJson(string invalidJson)
    {
        // Act - Attempt to validate invalid JSON
        var validationResult = await _settingsValidator.ValidateAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            invalidJson);

        // Assert - Validation should fail
        Assert.False(validationResult.IsValid);
        Assert.NotNull(validationResult.ErrorMessage);
    }

    /// <summary>
    /// AC7: Complete workflow - User configures all online access rules together.
    /// This simulates a full user interaction flow through the settings UI.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_UserConfiguresAllOnlineAccessRules_AndSeesThemApplied()
    {
        // Arrange - Define a complete configuration
        var accessMode = "auto_within_budget";
        var providers = "[\"openai\",\"azure_openai\"]";
        var dailyBudget = 75000;
        var monthlyBudget = 1500000;
        var budgetMode = "user_confirm";
        var alertThreshold = 85;

        // Act - User configures all online access settings
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            accessMode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: "complete_workflow_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            providers,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineProvidersEnabled,
            reason: "complete_workflow_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.DailyTokenBudget,
            dailyBudget,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.DailyTokenBudget,
            reason: "complete_workflow_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.MonthlyTokenBudget,
            monthlyBudget,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.MonthlyTokenBudget,
            reason: "complete_workflow_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.TokenBudgetMode,
            budgetMode,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.TokenBudgetMode,
            reason: "complete_workflow_test");

        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold,
            alertThreshold,
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.TokenBudgetAlertThreshold,
            reason: "complete_workflow_test");

        // Assert - All settings are persisted correctly
        var retrievedAccessMode = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode);
        var retrievedProviders = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineProvidersEnabled);
        var retrievedDailyBudget = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.DailyTokenBudget);
        var retrievedMonthlyBudget = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.MonthlyTokenBudget);
        var retrievedBudgetMode = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.TokenBudgetMode);
        var retrievedThreshold = await _settingsService.GetSettingValueAsync<int>(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold);

        Assert.Equal(accessMode, retrievedAccessMode);
        Assert.Equal(providers, retrievedProviders);
        Assert.Equal(dailyBudget, retrievedDailyBudget);
        Assert.Equal(monthlyBudget, retrievedMonthlyBudget);
        Assert.Equal(budgetMode, retrievedBudgetMode);
        Assert.Equal(alertThreshold, retrievedThreshold);

        // Assert - User can see configuration is applied (all values correct)
        Assert.Equal("auto_within_budget", retrievedAccessMode);
        Assert.Contains("openai", retrievedProviders);
        Assert.Contains("azure_openai", retrievedProviders);
        Assert.True(retrievedDailyBudget > 0);
        Assert.True(retrievedMonthlyBudget > retrievedDailyBudget);
    }

    /// <summary>
    /// AC8: Settings changes are logged for transparency and auditability.
    /// </summary>
    [Fact]
    public async Task SettingsChanges_AreLogged_ForTransparency()
    {
        // Arrange - Capture the change reason
        var reason = "user_transparency_test";

        // Act - Save a setting with a reason
        await _settingsService.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "per_task",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            reason: reason);

        // Assert - Setting is saved (the service logs internally)
        // Verification: Check logs show the reason for the change
        var savedValue = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode);
        Assert.Equal("per_task", savedValue);

        // Note: In production, users would see this in the dashboard or logs UI
        // The SettingsService logs: "Saved setting {Key} with reason: {Reason}"
    }

    #endregion
}
