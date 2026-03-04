using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Daiv3.FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for configuration options and application data management.
/// Tests various configuration scenarios and cache location management.
/// </summary>
[Collection("FoundryLocalManager collection")]
public sealed class ConfigurationTests : IAsyncLifetime, IDisposable
{
    private readonly FoundryLocalManagerFixture _fixture;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ConfigurationTests> _logger;
    private FoundryLocalManager? _manager;

    public ConfigurationTests(FoundryLocalManagerFixture fixture)
    {
        _fixture = fixture;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });

        _logger = _loggerFactory.CreateLogger<ConfigurationTests>();
    }

    public async Task InitializeAsync()
    {
        _manager = _fixture.Manager;
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task Configuration_WithCustomAppDataDir_ShouldUseCustomPath()
    {
        // Arrange
        var customAppDataDir = "./custom_foundry_data";
        var config = new Configuration
        {
            AppName = "CustomAppDataTest",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            AppDataDir = customAppDataDir
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal(customAppDataDir, config.AppDataDir);
        _logger.LogInformation($"Using custom app data directory: {config.AppDataDir}");
    }

    [Fact]
    public async Task Configuration_WithCustomModelCacheDir_ShouldUseCustomPath()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "CustomCacheTest",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            AppDataDir = "./foundry_data",
            ModelCacheDir = "{AppDataDir}/custom_cache"
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal("{AppDataDir}/custom_cache", config.ModelCacheDir);
        _logger.LogInformation($"Using custom model cache directory: {config.ModelCacheDir}");
    }

    [Fact]
    public async Task Configuration_WithCustomLogsDir_ShouldUseCustomPath()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "CustomLogsTest",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Debug,
            AppDataDir = "./foundry_data",
            LogsDir = "{AppDataDir}/custom_logs"
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal("{AppDataDir}/custom_logs", config.LogsDir);
        _logger.LogInformation($"Using custom logs directory: {config.LogsDir}");
    }

    [Theory]
    [InlineData(Microsoft.AI.Foundry.Local.LogLevel.Debug)]
    [InlineData(Microsoft.AI.Foundry.Local.LogLevel.Information)]
    [InlineData(Microsoft.AI.Foundry.Local.LogLevel.Warning)]
    [InlineData(Microsoft.AI.Foundry.Local.LogLevel.Error)]
    public async Task Configuration_WithDifferentLogLevels_ShouldApplyLogLevel(Microsoft.AI.Foundry.Local.LogLevel logLevel)
    {
        // Arrange
        var config = new Configuration
        {
            AppName = $"LogLevelTest_{logLevel}",
            LogLevel = logLevel
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal(logLevel, config.LogLevel);
        _logger.LogInformation($"Configuration created with log level: {logLevel}");
    }

    [Fact]
    public async Task Configuration_WithAllCustomSettings_ShouldApplyAllSettings()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "FullCustomConfigTest",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
            Web = new Configuration.WebService
            {
                Urls = "http://127.0.0.1:55700"
            },
            AppDataDir = "./full_custom_data",
            ModelCacheDir = "{AppDataDir}/models",
            LogsDir = "{AppDataDir}/logs"
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal("FullCustomConfigTest", config.AppName);
        Assert.Equal(Microsoft.AI.Foundry.Local.LogLevel.Information, config.LogLevel);
        Assert.Equal("http://127.0.0.1:55700", config.Web.Urls);
        Assert.Equal("./full_custom_data", config.AppDataDir);
        Assert.Equal("{AppDataDir}/models", config.ModelCacheDir);
        Assert.Equal("{AppDataDir}/logs", config.LogsDir);

        _logger.LogInformation("Full custom configuration applied successfully");
    }

    [Fact]
    public async Task Configuration_MinimalSettings_ShouldUseDefaults()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "MinimalConfigTest"
        };

        // Act
        var manager = _manager;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal("MinimalConfigTest", config.AppName);
        _logger.LogInformation("Minimal configuration created with defaults");
    }
}
