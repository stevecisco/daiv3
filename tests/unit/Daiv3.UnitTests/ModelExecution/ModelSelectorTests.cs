using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for ModelSelector (MQ-REQ-009).
/// </summary>
public class ModelSelectorTests
{
    private readonly ILogger<ModelSelector> _logger;

    public ModelSelectorTests()
    {
        _logger = new LoggerFactory().CreateLogger<ModelSelector>();
    }

    private ModelSelectorOptions GetDefaultOptions()
    {
        return new ModelSelectorOptions
        {
            TaskTypeModelMappings = ModelSelectorOptions.GetDefaultMappings(),
            DefaultFallbackModel = "phi-4",
            AvailableLocalModels = new List<string> { "phi-4", "llama-3", "mistral-7b" },
            AvailableOnlineModels = new List<string> { "openai:gpt-4", "azure:gpt-35-turbo" },
            PreferLocalModels = true,
            AllowOnlineFallback = true
        };
    }

    [Theory]
    [InlineData(TaskType.Chat, "phi-4")]
    [InlineData(TaskType.Search, "phi-4")]
    [InlineData(TaskType.Summarize, "phi-4")]
    [InlineData(TaskType.Code, "phi-4")]
    [InlineData(TaskType.QuestionAnswer, "phi-4")]
    public void SelectModel_WithDefaultMappings_ReturnsCorrectModel(TaskType taskType, string expectedModel)
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(taskType);

        // Assert
        Assert.Equal(expectedModel, result);
    }

    [Fact]
    public void SelectModel_WithUserPreference_RespectsUserPreference()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);
        var preferences = new ModelSelectionPreferences
        {
            PreferredModelId = "mistral-7b"
        };

        // Act
        var result = selector.SelectModel(TaskType.Chat, preferences);

        // Assert
        Assert.Equal("mistral-7b", result);
    }

    [Fact]
    public void SelectModel_WithUnavailablePreference_FallsBackToTaskMapping()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);
        var preferences = new ModelSelectionPreferences
        {
            PreferredModelId = "unavailable-model",
            AllowFallback = true
        };

        // Act
        var result = selector.SelectModel(TaskType.Chat, preferences);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Fact]
    public void SelectModel_WithUnavailablePreferenceAndNoFallback_ThrowsException()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);
        var preferences = new ModelSelectionPreferences
        {
            PreferredModelId = "unavailable-model",
            AllowFallback = false
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            selector.SelectModel(TaskType.Chat, preferences));
        Assert.Contains("unavailable-model", ex.Message);
    }

    [Fact]
    public void SelectModel_WithCustomMapping_UsesCustomMapping()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypeModelMappings["Code"] = "mistral-7b";
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Code);

        // Assert
        Assert.Equal("mistral-7b", result);
    }

    [Fact]
    public void SelectModel_WithUnmappedTaskType_UsesDefaultFallback()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypeModelMappings.Clear(); // Remove all mappings
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Chat);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Fact]
    public void SelectModel_WithLocalModelPreference_PrefersLocalModel()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);
        var preferences = new ModelSelectionPreferences
        {
            PreferLocalModels = true
        };

        // Act
        var result = selector.SelectModel(TaskType.Chat, preferences);

        // Assert
        Assert.Contains(result, GetDefaultOptions().AvailableLocalModels);
    }

    [Fact]
    public void SelectModel_WithNoLocalModelsAvailable_FallsBackToOnline()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.AvailableLocalModels.Clear(); // No local models
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Chat);

        // Assert
        Assert.Contains(result, customOptions.AvailableOnlineModels);
    }

    [Fact]
    public void SelectModel_WithNoModelsAvailable_ThrowsException()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.AvailableLocalModels.Clear();
        customOptions.AvailableOnlineModels.Clear();
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            selector.SelectModel(TaskType.Chat));
        Assert.Contains("No models available", ex.Message);
    }

    [Fact]
    public void SelectModel_WithOnlineFallbackDisabled_DoesNotUseOnlineModels()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.AvailableLocalModels.Clear();
        customOptions.AllowOnlineFallback = false;
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            selector.SelectModel(TaskType.Chat));
        Assert.Contains("No models available", ex.Message);
    }

    [Fact]
    public void GetDefaultModel_ReturnsTaskTypeMappedModel()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.GetDefaultModel(TaskType.Chat);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Fact]
    public void GetDefaultModel_WithUnmappedType_ReturnsFallback()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypeModelMappings.Clear();
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.GetDefaultModel(TaskType.Chat);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Theory]
    [InlineData("phi-4", true)]
    [InlineData("llama-3", true)]
    [InlineData("openai:gpt-4", true)]
    [InlineData("unavailable-model", false)]
    [InlineData("", false)]
    public void IsModelAvailable_ChecksAvailability(string modelId, bool expectedAvailable)
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);

        // Act & Assert
        if (string.IsNullOrEmpty(modelId))
        {
            Assert.Throws<ArgumentException>(() => selector.IsModelAvailable(modelId));
        }
        else
        {
            var result = selector.IsModelAvailable(modelId);
            Assert.Equal(expectedAvailable, result);
        }
    }

    [Fact]
    public void IsModelAvailable_IsCaseInsensitive()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);

        // Act
        var result1 = selector.IsModelAvailable("PHI-4");
        var result2 = selector.IsModelAvailable("Phi-4");
        var result3 = selector.IsModelAvailable("phi-4");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void SelectModel_WithNullPreferences_UsesDefaults()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Chat, preferences: null);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Fact]
    public void SelectModel_WithEmptyMappingsConfiguration_UsesDefaultMappings()
    {
        // Arrange
        var customOptions = new ModelSelectorOptions
        {
            // TaskTypeModelMappings left empty - should use defaults
            DefaultFallbackModel = "phi-4",
            AvailableLocalModels = new List<string> { "phi-4" },
            AvailableOnlineModels = new List<string>(),
            PreferLocalModels = true,
            AllowOnlineFallback = false
        };
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Chat);

        // Assert
        Assert.Equal("phi-4", result);
    }

    [Fact]
    public void SelectModel_WithTaskMappedModelUnavailable_UsesFallback()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypeModelMappings["Code"] = "unavailable-model";
        var options = Options.Create(customOptions);
        var selector = new ModelSelector(options, _logger);

        // Act
        var result = selector.SelectModel(TaskType.Code);

        // Assert
        Assert.Equal("phi-4", result); // Should fallback to default
    }
}
