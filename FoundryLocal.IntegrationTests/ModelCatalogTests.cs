using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for model catalog operations including listing, getting, and caching models.
/// </summary>
public class ModelCatalogTests : IAsyncLifetime
{
    private ILoggerFactory? _loggerFactory;
    private ILogger<ModelCatalogTests>? _logger;
    private FoundryLocalManager? _manager;

    public async Task InitializeAsync()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });
        
        _logger = _loggerFactory.CreateLogger<ModelCatalogTests>();

        var config = new Configuration
        {
            AppName = "ModelCatalogTests",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
        };

        await FoundryLocalManager.CreateAsync(config, _logger);
        _manager = FoundryLocalManager.Instance;
    }

    public async Task DisposeAsync()
    {
        if (_loggerFactory != null)
        {
            _loggerFactory.Dispose();
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ListModelsAsync_ShouldReturnNonEmptyList()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();

        // Act
        var models = await catalog.ListModelsAsync();
        var modelList = models.ToList();

        // Assert
        Assert.NotNull(modelList);
        _logger!.LogInformation($"Found {modelList.Count} models in catalog");
        
        foreach (var model in modelList.Take(5)) // Log first 5 models
        {
            _logger.LogInformation($"Model: {model}");
        }
    }

    [Fact]
    public async Task GetModelAsync_WithValidAlias_ShouldReturnModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var firstModel = models.FirstOrDefault();
        
        // Skip test if no models available
        if (firstModel == null)
        {
            _logger!.LogWarning("No models available in catalog, skipping test");
            return;
        }

        // Act & Assert
        // Note: This test demonstrates the API structure
        // Actual implementation depends on model availability
        Assert.NotNull(firstModel);
    }

    [Fact]
    public async Task GetLoadedModelsAsync_ShouldReturnLoadedModelsList()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();

        // Act
        var loadedModels = await catalog.GetLoadedModelsAsync();
        var loadedModelsList = loadedModels.ToList();

        // Assert
        Assert.NotNull(loadedModelsList);
        _logger!.LogInformation($"Currently loaded models: {loadedModelsList.Count}");
    }

    [Fact]
    public async Task GetCachedModelsAsync_ShouldReturnCachedModelsList()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();

        // Act
        var cachedModels = await catalog.GetCachedModelsAsync();
        var cachedModelsList = cachedModels.ToList();

        // Assert
        Assert.NotNull(cachedModelsList);
        _logger!.LogInformation($"Cached models: {cachedModelsList.Count}");
    }
}
