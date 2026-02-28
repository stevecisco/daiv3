namespace Daiv3.ModelExecution.Models;

/// <summary>
/// User preferences for model selection.
/// </summary>
public class ModelSelectionPreferences
{
    /// <summary>Preferred model ID override (takes precedence over task-based selection).</summary>
    public string? PreferredModelId { get; set; }

    /// <summary>Whether to allow online provider fallback if local model unavailable.</summary>
    public bool AllowOnlineFallback { get; set; } = true;

    /// <summary>Whether to prefer local models over online providers.</summary>
    public bool PreferLocalModels { get; set; } = true;

    /// <summary>Maximum token budget for online provider calls (null = no limit).</summary>
    public int? MaxOnlineTokens { get; set; }

    /// <summary>Whether to allow model fallback if preferred model unavailable.</summary>
    public bool AllowFallback { get; set; } = true;

    /// <summary>Additional context for model selection decision.</summary>
    public Dictionary<string, string> Context { get; set; } = new();
}
