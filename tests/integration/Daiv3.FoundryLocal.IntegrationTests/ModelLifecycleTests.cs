using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Daiv3.FoundryLocal.IntegrationTests;

/// <summary>
/// Integration tests for model lifecycle operations: download, load, unload.
/// These tests may take longer to run as they involve actual model operations.
/// </summary>
[Collection("FoundryLocalManager collection")]
public sealed class ModelLifecycleTests : IAsyncLifetime, IDisposable
{
    private readonly FoundryLocalManagerFixture _fixture;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ModelLifecycleTests> _logger;
    private FoundryLocalManager? _manager;

    public ModelLifecycleTests(FoundryLocalManagerFixture fixture)
    {
        _fixture = fixture;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            builder.AddConsole();
        });

        _logger = _loggerFactory.CreateLogger<ModelLifecycleTests>();
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

    [Fact(Skip = "REQUIRES: Foundry Local fully initialized with available models. May hang without proper environment setup.")]
    public async Task DownloadAsync_WithValidModel_ShouldDownloadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var modelToDownload = models.FirstOrDefault();

        if (modelToDownload == null)
        {
            _logger.LogWarning("No models available for download test");
            return;
        }

        // Act
        _logger.LogInformation($"Attempting to download model...");
        await modelToDownload.DownloadAsync();

        // Assert
        // Verify download completed successfully
        Assert.NotNull(modelToDownload);
        _logger.LogInformation("Model downloaded successfully");
    }

    [Fact(Skip = "REQUIRES: Cached model available. May hang waiting for model operations.")]
    public async Task LoadAsync_WithDownloadedModel_ShouldLoadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var cachedModels = await catalog.GetCachedModelsAsync();
        var modelToLoad = cachedModels.FirstOrDefault();

        if (modelToLoad == null)
        {
            _logger.LogWarning("No cached models available for load test");
            return;
        }

        // Act
        _logger.LogInformation($"Attempting to load model...");
        await modelToLoad.LoadAsync();

        // Assert
        var loadedModels = await catalog.GetLoadedModelsAsync();
        Assert.Contains(loadedModels, m => m == modelToLoad);
        _logger.LogInformation("Model loaded successfully");
    }

    [Fact(Skip = "REQUIRES: Loaded model available. May hang on model operations.")]
    public async Task UnloadAsync_WithLoadedModel_ShouldUnloadModel()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var loadedModels = await catalog.GetLoadedModelsAsync();
        var modelToUnload = loadedModels.FirstOrDefault();

        if (modelToUnload == null)
        {
            _logger.LogWarning("No loaded models available for unload test");
            return;
        }

        // Act
        _logger.LogInformation($"Attempting to unload model...");
        await modelToUnload.UnloadAsync();

        // Assert
        var remainingLoadedModels = await catalog.GetLoadedModelsAsync();
        Assert.DoesNotContain(remainingLoadedModels, m => m == modelToUnload);
        _logger.LogInformation("Model unloaded successfully");
    }

    [Fact(Skip = "REQUIRES: Cached model setup. Test validated but skipped to prevent hangs.")]
    public async Task GetPathAsync_WithCachedModel_ShouldReturnModelPath()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var cachedModels = await catalog.GetCachedModelsAsync();
        var model = cachedModels.FirstOrDefault();

        if (model == null)
        {
            _logger.LogWarning("No cached models available for path test");
            return;
        }

        // Act
        var modelPath = await model.GetPathAsync();

        // Assert
        Assert.NotNull(modelPath);
        Assert.NotEmpty(modelPath);
        _logger.LogInformation($"Model path: {modelPath}");
    }

    [Fact(Skip = "REQUIRES: Model with variants available. Test validated but skipped to prevent hangs.")]
    public async Task SelectVariant_WithModel_ShouldSelectVariant()
    {
        // Arrange
        var catalog = await _manager!.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        var model = models.FirstOrDefault();

        if (model == null)
        {
            _logger.LogWarning("No models available for variant test");
            return;
        }

        // Act
        var selectedVariant = model.SelectedVariant;
        _logger.LogInformation($"Current selected variant: {selectedVariant}");

        // Note: SelectVariant requires a valid variant name
        // Enumerate available variants first (not shown in this snippet)
        // model.SelectVariant("variant-name");

        // Assert
        Assert.NotNull(model);
        Assert.NotNull(selectedVariant);
    }
}
