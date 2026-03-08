using Daiv3.Core.Settings;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Unit tests for SettingsValidator.
/// Verifies CT-NFR-002: Settings changes SHOULD be validated and applied safely.
/// </summary>
public class SettingsValidatorTests
{
    private readonly Mock<ILogger<SettingsValidator>> _mockLogger;
    private readonly SettingsValidator _validator;

    public SettingsValidatorTests()
    {
        _mockLogger = new Mock<ILogger<SettingsValidator>>();
        _validator = new SettingsValidator(_mockLogger.Object);
    }

    #region Path Settings Tests

    [Fact]
    public async Task ValidateAsync_WithValidDataDirectory_ReturnsValid()
    {
        // Arrange
        var validPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Paths.DataDirectory, validPath);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidDataDirectory_ReturnsInvalid()
    {
        // Arrange
        var invalidPath = "Z:\\invalid\\path\\that\\does\\not\\exist\\parent";

        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Paths.DataDirectory, invalidPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithValidWatchedDirectories_ReturnsValid()
    {
        // Arrange
        var validJson = "[]"; // Empty array

        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Paths.WatchedDirectories, validJson);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidWatchedDirectoriesJson_ReturnsInvalid()
    {
        // Arrange
        var invalidJson = "not a valid json array";

        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Paths.WatchedDirectories, invalidJson);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid JSON", result.ErrorMessage);
    }

    #endregion

    #region Positive Integer Tests

    [Theory]
    [InlineData(ApplicationSettings.Providers.DailyTokenBudget, 100)]
    [InlineData(ApplicationSettings.Providers.MonthlyTokenBudget, 10000)]
    [InlineData(ApplicationSettings.General.AgentIterationLimit, 5)]
    [InlineData(ApplicationSettings.General.AgentTokenBudget, 1000)]
    [InlineData(ApplicationSettings.General.SchedulerCheckInterval, 30)]
    public async Task ValidateAsync_WithPositiveInteger_ReturnsValid(string key, int value)
    {
        // Act
        var result = await _validator.ValidateAsync(key, value);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(ApplicationSettings.Providers.DailyTokenBudget, 0)]
    [InlineData(ApplicationSettings.Providers.MonthlyTokenBudget, -100)]
    [InlineData(ApplicationSettings.General.AgentIterationLimit, -1)]
    public async Task ValidateAsync_WithNonPositiveInteger_ReturnsInvalid(string key, int value)
    {
        // Act
        var result = await _validator.ValidateAsync(key, value);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Percentage Tests

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ValidateAsync_WithValidPercentage_ReturnsValid(int percentage)
    {
        // Act
        var result = await _validator.ValidateAsync(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold, percentage);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ValidateAsync_WithInvalidPercentage_ReturnsInvalid(int percentage)
    {
        // Act
        var result = await _validator.ValidateAsync(
            ApplicationSettings.Providers.TokenBudgetAlertThreshold, percentage);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Enum/Choice Settings Tests

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("system")]
    public async Task ValidateAsync_WithValidTheme_ReturnsValid(string theme)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.UI.Theme, theme);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidTheme_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.UI.Theme, "invalid_theme");

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("ask")]
    [InlineData("auto_within_budget")]
    [InlineData("per_task")]
    public async Task ValidateAsync_WithValidOnlineAccessMode_ReturnsValid(string mode)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OnlineAccessMode, mode);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidOnlineAccessMode_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OnlineAccessMode, "invalid_mode");

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("hard_stop")]
    [InlineData("user_confirm")]
    public async Task ValidateAsync_WithValidTokenBudgetMode_ReturnsValid(string mode)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.TokenBudgetMode, mode);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidTokenBudgetMode_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.TokenBudgetMode, "invalid");

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("npu")]
    [InlineData("gpu")]
    [InlineData("cpu")]
    public async Task ValidateAsync_WithValidExecutionProvider_ReturnsValid(string provider)
    {
        // Act
        var result = await _validator.ValidateAsync(
            ApplicationSettings.Hardware.PreferredExecutionProvider, provider);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region URL Tests

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://api.anthropic.com")]
    [InlineData("https://api.example.com:8080/path")]
    public async Task ValidateAsync_WithValidUrl_ReturnsValid(string url)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OpenAIBaseUrl, url);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ht!p://invalid")]
    public async Task ValidateAsync_WithInvalidUrl_ReturnsInvalid(string url)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OpenAIBaseUrl, url);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyUrl_ReturnsValid()
    {
        // URLs can be optional/empty
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OpenAIBaseUrl, "");

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region JSON Array Tests

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"item1\",\"item2\"]")]
    public async Task ValidateAsync_WithValidJsonArray_ReturnsValid(string json)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OnlineProvidersEnabled, json);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{ invalid json")]
    public async Task ValidateAsync_WithInvalidJsonArray_ReturnsInvalid(string json)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.OnlineProvidersEnabled, json);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region JSON Object Tests

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"key\":\"value\"}")]
    public async Task ValidateAsync_WithValidJsonObject_ReturnsValid(string json)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Models.ModelToTaskMappings, json);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task ValidateAsync_WithInvalidJsonObject_ReturnsInvalid(string json)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Models.ModelToTaskMappings, json);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Model Name Tests

    [Theory]
    [InlineData("phi-3-mini")]
    [InlineData("phi-4")]
    [InlineData("custom-model")]
    public async Task ValidateAsync_WithValidModelName_ReturnsValid(string modelName)
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Models.FoundryLocalDefaultModel, modelName);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyModelName_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Models.FoundryLocalDefaultModel, "");

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Batch Validation Tests

    [Fact]
    public async Task ValidateBatchAsync_WithAllValidSettings_ReturnsAllValid()
    {
        // Arrange
        var settings = new Dictionary<string, object>
        {
            { ApplicationSettings.Providers.DailyTokenBudget, 50000 },
            { ApplicationSettings.UI.Theme, "dark" },
            { ApplicationSettings.Providers.OnlineAccessMode, "ask" }
        };

        // Act
        var results = await _validator.ValidateBatchAsync(settings);

        // Assert
        Assert.All(results, r => Assert.True(r.IsValid));
    }

    [Fact]
    public async Task ValidateBatchAsync_WithSomeInvalidSettings_ReturnsValidAndInvalid()
    {
        // Arrange
        var settings = new Dictionary<string, object>
        {
            { ApplicationSettings.Providers.DailyTokenBudget, 50000 }, // Valid
            { ApplicationSettings.UI.Theme, "invalid" }, // Invalid
            { ApplicationSettings.Providers.OnlineAccessMode, "ask" } // Valid
        };

        // Act
        var results = await _validator.ValidateBatchAsync(settings);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r.IsValid));
        Assert.Equal(1, results.Count(r => !r.IsValid));
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task ValidateAsync_WithWrongType_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync(ApplicationSettings.Providers.DailyTokenBudget, "not an integer");

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion
}
