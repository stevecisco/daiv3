using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for model selection.
/// </summary>
public class ModelSelectorOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ModelSelector";

    /// <summary>
    /// Task type to model mappings.
    /// Key: TaskType enum name, Value: Model ID.
    /// </summary>
    public Dictionary<string, string> TaskTypeModelMappings { get; set; } = new();

    /// <summary>
    /// Fallback model ID when no specific mapping exists or preferred model unavailable.
    /// Default: "phi-4" (Foundry Local default chat model).
    /// </summary>
    public string DefaultFallbackModel { get; set; } = "phi-4";

    /// <summary>
    /// List of available local model IDs.
    /// </summary>
    public List<string> AvailableLocalModels { get; set; } = new() { "phi-4" };

    /// <summary>
    /// List of available online provider models.
    /// Format: "provider:model-id" (e.g., "openai:gpt-4", "azure:gpt-35-turbo").
    /// </summary>
    public List<string> AvailableOnlineModels { get; set; } = new();

    /// <summary>
    /// Whether to prefer local models over online providers by default.
    /// </summary>
    public bool PreferLocalModels { get; set; } = true;

    /// <summary>
    /// Whether to allow automatic fallback to online providers.
    /// </summary>
    public bool AllowOnlineFallback { get; set; } = true;

    /// <summary>
    /// Default model mappings for common task types.
    /// </summary>
    public static Dictionary<string, string> GetDefaultMappings()
    {
        return new Dictionary<string, string>
        {
            [nameof(TaskType.Chat)] = "phi-4",
            [nameof(TaskType.Search)] = "phi-4",
            [nameof(TaskType.Summarize)] = "phi-4",
            [nameof(TaskType.Code)] = "phi-4",
            [nameof(TaskType.QuestionAnswer)] = "phi-4",
            [nameof(TaskType.Rewrite)] = "phi-4",
            [nameof(TaskType.Translation)] = "phi-4",
            [nameof(TaskType.Analysis)] = "phi-4",
            [nameof(TaskType.Generation)] = "phi-4",
            [nameof(TaskType.Extraction)] = "phi-4",
            [nameof(TaskType.Unknown)] = "phi-4"
        };
    }
}
