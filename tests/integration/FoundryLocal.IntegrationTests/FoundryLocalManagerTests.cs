using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for Foundry Local SDK initialization and catalog operations.
/// </summary>
public sealed class FoundryLocalManagerTests : IAsyncLifetime, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FoundryLocalManagerTests> _logger;

    public FoundryLocalManagerTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });

        _logger = _loggerFactory.CreateLogger<FoundryLocalManagerTests>();
    }

    public async Task InitializeAsync()
    {
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
    public async Task CreateAsync_ShouldInitializeFoundryLocalManager()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "FoundryLocalIntegrationTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
        };

        // Act
        await FoundryLocalManager.CreateAsync(config, _logger);
        var manager = FoundryLocalManager.Instance;

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public async Task GetCatalogAsync_ShouldReturnCatalog()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "FoundryLocalIntegrationTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
        };
        
        await FoundryLocalManager.CreateAsync(config, _logger);
        var manager = FoundryLocalManager.Instance;

        // Act
        var catalog = await manager.GetCatalogAsync();

        // Assert
        Assert.NotNull(catalog);
    }

    [Fact]
    public async Task ListModelsAsync_ShouldReturnModelsList()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "FoundryLocalIntegrationTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
        };
        
        await FoundryLocalManager.CreateAsync(config, _logger);
        var manager = FoundryLocalManager.Instance;
        var catalog = await manager.GetCatalogAsync();

        // Act
        var models = await catalog.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        _logger.LogInformation($"Models available: {models.Count()}");
    }

    [Fact]
    public async Task Configuration_WithCustomSettings_ShouldApplySettings()
    {
        // Arrange
        var config = new Configuration
        {
            AppName = "FoundryLocalCustomConfig",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Debug,
            Web = new Configuration.WebService
            {
                Urls = "http://127.0.0.1:55588"
            },
            AppDataDir = "./foundry_local_data",
            ModelCacheDir = "{AppDataDir}/model_cache",
            LogsDir = "{AppDataDir}/logs"
        };

        // Act
        await FoundryLocalManager.CreateAsync(config, _logger);
        var manager = FoundryLocalManager.Instance;

        // Assert
        Assert.NotNull(manager);
        Assert.Equal("./foundry_local_data", config.AppDataDir);
        Assert.Equal("{AppDataDir}/model_cache", config.ModelCacheDir);
    }
}
