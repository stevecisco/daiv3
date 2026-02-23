namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for online provider routing.
/// </summary>
public class OnlineProviderOptions
{
    /// <summary>
    /// Configured online providers with their settings.
    /// </summary>
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    /// <summary>
    /// Default provider to use when not specified.
    /// </summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>
    /// Whether to require user confirmation for online calls.
    /// </summary>
    public bool RequireUserConfirmation { get; set; } = true;

    /// <summary>
    /// Token threshold above which to require confirmation (if enabled).
    /// </summary>
    public int ConfirmationThreshold { get; set; } = 1000;
}

/// <summary>
/// Configuration for a specific online provider.
/// </summary>
public class ProviderConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int DailyInputTokenLimit { get; set; } = 10000;
    public int DailyOutputTokenLimit { get; set; } = 10000;
    public int MonthlyInputTokenLimit { get; set; } = 300000;
    public int MonthlyOutputTokenLimit { get; set; } = 300000;
    public Dictionary<string, string> TaskTypeToModel { get; set; } = new();
}
