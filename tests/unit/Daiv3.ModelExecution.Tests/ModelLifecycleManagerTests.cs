using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.ModelExecution.Tests;

/// <summary>
/// Tests for MQ-REQ-001: Enforce constraint that only one Foundry Local model is loaded at a time.
/// </summary>
public class ModelLifecycleManagerTests
{
    private readonly Mock<ILogger<ModelLifecycleManager>> _mockLogger;
    private readonly ModelLifecycleManager _lifecycleManager;

    public ModelLifecycleManagerTests()
    {
        _mockLogger = new Mock<ILogger<ModelLifecycleManager>>();
        _lifecycleManager = new ModelLifecycleManager(_mockLogger.Object);
    }

    #region Constraint Enforcement Tests

    [Fact]
    public async Task LoadModelAsync_WithNoModelLoaded_Succeeds()
    {
        // Act
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Assert
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal("phi-3-mini", loadedModel);
    }

    [Fact]
    public async Task LoadModelAsync_AndDifferentModelAlreadyLoaded_ThrowsInvalidOperationException()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _lifecycleManager.LoadModelAsync("phi-3.5-mini"));

        Assert.Contains("Cannot load model", ex.Message);
        Assert.Contains("phi-3.5-mini", ex.Message);
        Assert.Contains("phi-3-mini", ex.Message);
        Assert.Contains("already loaded", ex.Message);
    }

    [Fact]
    public async Task LoadModelAsync_WithSameModelAlreadyLoaded_IsIdempotent()
    {
        // Arrange
        var modelId = "phi-3-mini";
        await _lifecycleManager.LoadModelAsync(modelId);

        // Act - Load the same model again
        await _lifecycleManager.LoadModelAsync(modelId);

        // Assert - Should still be loaded and succeed
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal(modelId, loadedModel);
    }

    [Fact]
    public async Task LoadModelAsync_NullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _lifecycleManager.LoadModelAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _lifecycleManager.LoadModelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _lifecycleManager.LoadModelAsync("   "));
    }

    #endregion

    #region Model Switching Tests

    [Fact]
    public async Task SwitchModelAsync_FromOneModelToAnother_Succeeds()
    {
        // Arrange
        var model1 = "phi-3-mini";
        var model2 = "phi-3.5-mini";
        await _lifecycleManager.LoadModelAsync(model1);

        // Act
        await _lifecycleManager.SwitchModelAsync(model2);

        // Assert
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal(model2, loadedModel);
    }

    [Fact]
    public async Task SwitchModelAsync_WithNoModelLoaded_Loads()
    {
        // Act
        await _lifecycleManager.SwitchModelAsync("phi-3-mini");

        // Assert
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal("phi-3-mini", loadedModel);
    }

    [Fact]
    public async Task SwitchModelAsync_ToSameModel_IsIdempotent()
    {
        // Arrange
        var modelId = "phi-3-mini";
        await _lifecycleManager.LoadModelAsync(modelId);

        // Act - Switch to the same model
        await _lifecycleManager.SwitchModelAsync(modelId);

        // Assert
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal(modelId, loadedModel);
    }

    [Fact]
    public async Task SwitchModelAsync_NullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _lifecycleManager.SwitchModelAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _lifecycleManager.SwitchModelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _lifecycleManager.SwitchModelAsync("   "));
    }

    #endregion

    #region Unload Tests

    [Fact]
    public async Task UnloadModelAsync_WithModelLoaded_Succeeds()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Act
        await _lifecycleManager.UnloadModelAsync();

        // Assert
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Null(loadedModel);
    }

    [Fact]
    public async Task UnloadModelAsync_WithNoModelLoaded_IsNoOp()
    {
        // Act - Unload when nothing is loaded
        await _lifecycleManager.UnloadModelAsync();

        // Assert - Still nothing loaded
        var loadedModel = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Null(loadedModel);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetLoadedModelAsync_WithoutLoading_ReturnsNull()
    {
        // Act
        var model = await _lifecycleManager.GetLoadedModelAsync();

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public async Task IsModelLoadedAsync_WithLoadedModel_ReturnsTrue()
    {
        // Arrange
        var modelId = "phi-3-mini";
        await _lifecycleManager.LoadModelAsync(modelId);

        // Act
        var isLoaded = await _lifecycleManager.IsModelLoadedAsync(modelId);

        // Assert
        Assert.True(isLoaded);
    }

    [Fact]
    public async Task IsModelLoadedAsync_WithDifferentModel_ReturnsFalse()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Act
        var isLoaded = await _lifecycleManager.IsModelLoadedAsync("phi-3.5-mini");

        // Assert
        Assert.False(isLoaded);
    }

    [Fact]
    public async Task IsModelLoadedAsync_WithNoModelLoaded_ReturnsFalse()
    {
        // Act
        var isLoaded = await _lifecycleManager.IsModelLoadedAsync("phi-3-mini");

        // Assert
        Assert.False(isLoaded);
    }

    [Fact]
    public async Task GetLastModelSwitchAsync_WithoutOperation_ReturnsNull()
    {
        // Act
        var lastSwitch = await _lifecycleManager.GetLastModelSwitchAsync();

        // Assert
        Assert.Null(lastSwitch);
    }

    [Fact]
    public async Task GetLastModelSwitchAsync_AfterLoad_ReturnsTimestamp()
    {
        // Arrange
        var beforeLoad = DateTimeOffset.UtcNow;

        // Act
        await _lifecycleManager.LoadModelAsync("phi-3-mini");
        var lastSwitch = await _lifecycleManager.GetLastModelSwitchAsync();
        var afterLoad = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(lastSwitch);
        Assert.True(lastSwitch >= beforeLoad && lastSwitch <= afterLoad);
    }

    [Fact]
    public async Task GetLastModelSwitchAsync_AfterSwitch_UpdatesTimestamp()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");
        var firstSwitch = await _lifecycleManager.GetLastModelSwitchAsync();
        await Task.Delay(10); // Ensure time difference

        // Act
        var beforeSwitch = DateTimeOffset.UtcNow;
        await _lifecycleManager.SwitchModelAsync("phi-3.5-mini");
        var secondSwitch = await _lifecycleManager.GetLastModelSwitchAsync();
        var afterSwitch = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(secondSwitch);
        Assert.True(secondSwitch > firstSwitch);
        Assert.True(secondSwitch >= beforeSwitch && secondSwitch <= afterSwitch);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public async Task GetMetricsAsync_WithoutOperations_ReturnsDefaultMetrics()
    {
        // Act
        var metrics = await _lifecycleManager.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TotalLoads);
        Assert.Equal(0, metrics.SuccessfulLoads);
        Assert.Equal(0, metrics.FailedLoads);
        Assert.Equal(0, metrics.ConstraintViolations);
        Assert.Null(metrics.CurrentModelId);
        Assert.Null(metrics.LastModelSwitch);
        Assert.Equal(0, metrics.AverageLoadTimeMs);
    }

    [Fact]
    public async Task GetMetricsAsync_AfterSuccessfulLoad_TracksMetrics()
    {
        // Act
        await _lifecycleManager.LoadModelAsync("phi-3-mini");
        var metrics = await _lifecycleManager.GetMetricsAsync();

        // Assert
        Assert.Equal(1, metrics.TotalLoads);
        Assert.Equal(1, metrics.SuccessfulLoads);
        Assert.Equal(0, metrics.FailedLoads);
        Assert.Equal(0, metrics.ConstraintViolations);
        Assert.Equal("phi-3-mini", metrics.CurrentModelId);
        Assert.NotNull(metrics.LastModelSwitch);
        Assert.True(metrics.AverageLoadTimeMs > 0);
    }

    [Fact]
    public async Task GetMetricsAsync_TracksConstraintViolations()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _lifecycleManager.LoadModelAsync("phi-3.5-mini"));

        var metrics = await _lifecycleManager.GetMetricsAsync();

        // Assert
        Assert.Equal(2, metrics.TotalLoads); // Initial load + attempted load
        Assert.Equal(1, metrics.SuccessfulLoads);
        Assert.Equal(1, metrics.FailedLoads);
        Assert.Equal(1, metrics.ConstraintViolations);
    }

    [Fact]
    public async Task GetMetricsAsync_MultipleOperations_AveragesLoadTimes()
    {
        // Act
        await _lifecycleManager.LoadModelAsync("phi-3-mini");
        await _lifecycleManager.SwitchModelAsync("phi-3.5-mini");
        await _lifecycleManager.SwitchModelAsync("phi-4-mini");

        var metrics = await _lifecycleManager.GetMetricsAsync();

        // Assert
        Assert.Equal(3, metrics.TotalLoads); // Load + 2 switches (each switch is a load operation)
        Assert.Equal(3, metrics.SuccessfulLoads);
        Assert.Equal(0, metrics.FailedLoads);
        Assert.True(metrics.AverageLoadTimeMs > 0);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task LoadModelAsync_ConcurrentCalls_AreThreadSafe()
    {
        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _lifecycleManager.LoadModelAsync("phi-3-mini"))
            .ToList();

        // Should not throw due to proper locking
        await Task.WhenAll(tasks);

        // Assert
        var model = await _lifecycleManager.GetLoadedModelAsync();
        Assert.Equal("phi-3-mini", model);
    }

    [Fact]
    public async Task ConcurrentLoadAndSwitch_AreThreadSafe()
    {
        // Act
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_lifecycleManager.LoadModelAsync("phi-3-mini"));
            tasks.Add(_lifecycleManager.SwitchModelAsync("phi-3.5-mini"));
            tasks.Add(_lifecycleManager.SwitchModelAsync("phi-3-mini"));
        }

        // Should not throw unexpectedly
        await Task.WhenAll(tasks);

        // Assert - Should end in a valid state (one model loaded or none)
        var model = await _lifecycleManager.GetLoadedModelAsync();
        Assert.True(model == "phi-3-mini" || model == "phi-3.5-mini");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ConstraintViolation_LogsDetailedError()
    {
        // Arrange
        await _lifecycleManager.LoadModelAsync("phi-3-mini");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _lifecycleManager.LoadModelAsync("phi-3.5-mini"));

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to load")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
