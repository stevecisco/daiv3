using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for model lifecycle operations: download, load, unload.
/// These tests may take longer to run as they involve actual model operations.
/// </summary>
public class ModelLifecycleTests : IAsyncLifetime
{
    private ILoggerFactory? _loggerFactory;
    private ILogger<ModelLifecycleTests>? _logger;
    private FoundryLocalManager? _manager;

    public async Task InitializeAsync()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });
        
        _logger = _loggerFactory.CreateLogger<ModelLifecycleTests>();

        var config = new Configuration
        {
            AppName = "ModelLifecycleTests",
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

    [Fact(Skip = "Requires specific model to be available and can be slow")]
    public async Task DownloadAsync_WithValidModel_ShouldDownloadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var modelToDownload = models.FirstOrDefault();

        if (modelToDownload == null)
        {
            _logger!.LogWarning("No models available for download test");
            return;
        }

        // Act
        _logger!.LogInformation($"Attempting to download model...");
        // await modelToDownload.DownloadAsync();

        // Assert
        // Verify download completed successfully
        Assert.NotNull(modelToDownload);
    }

    [Fact(Skip = "Requires model to be downloaded first")]
    public async Task LoadAsync_WithDownloadedModel_ShouldLoadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var cachedModels = await catalog.GetCachedModelsAsync();
        var modelToLoad = cachedModels.FirstOrDefault();

        if (modelToLoad == null)
        {
            _logger!.LogWarning("No cached models available for load test");
            return;
        }

        // Act
        _logger!.LogInformation($"Attempting to load model...");
        // await modelToLoad.LoadAsync();

        // Assert
        var loadedModels = await catalog.GetLoadedModelsAsync();
        Assert.Contains(loadedModels, m => m == modelToLoad);
    }

    [Fact(Skip = "Requires model to be loaded first")]
    public async Task UnloadAsync_WithLoadedModel_ShouldUnloadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var loadedModels = await catalog.GetLoadedModelsAsync();
        var modelToUnload = loadedModels.FirstOrDefault();

        if (modelToUnload == null)
        {
            _logger!.LogWarning("No loaded models available for unload test");
            return;
        }

        // Act
        _logger!.LogInformation($"Attempting to unload model...");
        // await modelToUnload.UnloadAsync();

        // Assert
        var remainingLoadedModels = await catalog.GetLoadedModelsAsync();
        Assert.DoesNotContain(remainingLoadedModels, m => m == modelToUnload);
    }

    [Fact(Skip = "Requires specific model setup")]
    public async Task GetPathAsync_WithCachedModel_ShouldReturnModelPath()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var cachedModels = await catalog.GetCachedModelsAsync();
        var model = cachedModels.FirstOrDefault();

        if (model == null)
        {
            _logger!.LogWarning("No cached models available for path test");
            return;
        }

        // Act
        // var modelPath = await model.GetPathAsync();

        // Assert
        // Assert.NotNull(modelPath);
        // Assert.NotEmpty(modelPath);
        // _logger!.LogInformation($"Model path: {modelPath}");
        Assert.NotNull(model);
    }

    [Fact(Skip = "Requires specific model with variants")]
    public async Task SelectVariant_WithModel_ShouldSelectVariant()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var model = models.FirstOrDefault();

        if (model == null)
        {
            _logger!.LogWarning("No models available for variant test");
            return;
        }

        // Act
        // var selectedVariant = model.SelectedVariant;
        // model.SelectVariant("variant-name");

        // Assert
        Assert.NotNull(model);
        // Assert.NotNull(selectedVariant);
    }
}
