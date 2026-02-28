using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Selects appropriate models based on task type and user preferences.
/// </summary>
public class ModelSelector : IModelSelector
{
    private readonly ModelSelectorOptions _options;
    private readonly ILogger<ModelSelector> _logger;
    private readonly Dictionary<TaskType, string> _taskTypeModelMappings;
    private readonly HashSet<string> _availableModels;

    public ModelSelector(
        IOptions<ModelSelectorOptions> options,
        ILogger<ModelSelector> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Initialize default mappings if none configured
        if (_options.TaskTypeModelMappings.Count == 0)
        {
            _options.TaskTypeModelMappings = ModelSelectorOptions.GetDefaultMappings();
        }

        // Build runtime task type mappings
        _taskTypeModelMappings = new Dictionary<TaskType, string>();
        foreach (var mapping in _options.TaskTypeModelMappings)
        {
            if (Enum.TryParse<TaskType>(mapping.Key, ignoreCase: true, out var taskType))
            {
                _taskTypeModelMappings[taskType] = mapping.Value;
            }
        }

        // Build available models set (local + online)
        _availableModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in _options.AvailableLocalModels)
        {
            _availableModels.Add(model);
        }
        foreach (var model in _options.AvailableOnlineModels)
        {
            _availableModels.Add(model);
        }
    }

    public string SelectModel(TaskType taskType, ModelSelectionPreferences? preferences = null)
    {
        preferences ??= new ModelSelectionPreferences();

        // 1. Check for explicit user preference
        if (!string.IsNullOrWhiteSpace(preferences.PreferredModelId))
        {
            if (IsModelAvailable(preferences.PreferredModelId))
            {
                _logger.LogDebug(
                    "Using user-preferred model {ModelId} for task type {TaskType}",
                    preferences.PreferredModelId, taskType);
                return preferences.PreferredModelId;
            }

            if (!preferences.AllowFallback)
            {
                throw new InvalidOperationException(
                    $"Preferred model '{preferences.PreferredModelId}' is not available and fallback is disabled.");
            }

            _logger.LogWarning(
                "Preferred model {PreferredModel} not available, falling back to task-based selection",
                preferences.PreferredModelId);
        }

        // 2. Look up task type mapping
        if (_taskTypeModelMappings.TryGetValue(taskType, out var mappedModel))
        {
            if (IsModelAvailable(mappedModel))
            {
                _logger.LogDebug(
                    "Using task-mapped model {ModelId} for task type {TaskType}",
                    mappedModel, taskType);
                return mappedModel;
            }

            _logger.LogWarning(
                "Task-mapped model {MappedModel} for {TaskType} not available, falling back",
                mappedModel, taskType);
        }

        // 3. Try default fallback model
        if (IsModelAvailable(_options.DefaultFallbackModel))
        {
            _logger.LogDebug(
                "Using default fallback model {ModelId} for task type {TaskType}",
                _options.DefaultFallbackModel, taskType);
            return _options.DefaultFallbackModel;
        }

        // 4. Try any available local model if prefer local
        if (preferences.PreferLocalModels || _options.PreferLocalModels)
        {
            var firstLocalModel = _options.AvailableLocalModels.FirstOrDefault();
            if (firstLocalModel != null)
            {
                _logger.LogDebug(
                    "Using first available local model {ModelId} for task type {TaskType}",
                    firstLocalModel, taskType);
                return firstLocalModel;
            }
        }

        // 5. Try any available online model if allowed
        if ((preferences.AllowOnlineFallback && _options.AllowOnlineFallback) ||
            (preferences.AllowOnlineFallback && preferences.PreferLocalModels == false))
        {
            var firstOnlineModel = _options.AvailableOnlineModels.FirstOrDefault();
            if (firstOnlineModel != null)
            {
                _logger.LogWarning(
                    "No local models available, using online model {ModelId} for task type {TaskType}",
                    firstOnlineModel, taskType);
                return firstOnlineModel;
            }
        }

        // 6. No models available
        throw new InvalidOperationException(
            $"No models available for task type '{taskType}'. " +
            $"Configure AvailableLocalModels or AvailableOnlineModels.");
    }

    public string GetDefaultModel(TaskType taskType)
    {
        if (_taskTypeModelMappings.TryGetValue(taskType, out var mappedModel))
        {
            return mappedModel;
        }

        return _options.DefaultFallbackModel;
    }

    public bool IsModelAvailable(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return _availableModels.Contains(modelId);
    }
}
