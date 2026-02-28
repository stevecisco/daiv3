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
    /// Confirmation mode for online provider requests.
    /// </summary>
    public ConfirmationMode ConfirmationMode { get; set; } = ConfirmationMode.AboveThreshold;

    /// <summary>
    /// Token threshold above which to require confirmation (when ConfirmationMode is AboveThreshold).
    /// </summary>
    public int ConfirmationThreshold { get; set; } = 1000;

    /// <summary>
    /// Context minimization settings for privacy and efficiency.
    /// </summary>
    /// <remarks>
    /// MQ-REQ-015: Send only minimal required context to online providers.
    /// </remarks>
    public ContextMinimizationOptions ContextMinimization { get; set; } = new();
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
    public int RateLimitWindowSeconds { get; set; } = 60;
    public int MaxRequestsPerWindow { get; set; } = 60;
    public Dictionary<string, string> TaskTypeToModel { get; set; } = new();
}

/// <summary>
/// Context minimization options for privacy and efficiency when sending requests to online providers.
/// </summary>
/// <remarks>
/// MQ-REQ-015: Send only minimal required context to online providers.
/// These settings control what contextual information is sent to protect privacy and reduce token usage.
/// </remarks>
public class ContextMinimizationOptions
{
    /// <summary>
    /// Whether context minimization is enabled.
    /// </summary>
    /// <remarks>
    /// When false, full context is sent (not recommended for sensitive data).
    /// Default: true (privacy-first design).
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum tokens allowed for all context values combined.
    /// </summary>
    /// <remarks>
    /// Context exceeding this limit will be truncated.
    /// Default: 2000 tokens (approximately 1500 words).
    /// </remarks>
    public int MaxContextTokens { get; set; } = 2000;

    /// <summary>
    /// Maximum tokens allowed per individual context key.
    /// </summary>
    /// <remarks>
    /// Individual context values exceeding this limit will be truncated.
    /// Default: 1000 tokens (approximately 750 words).
    /// </remarks>
    public int MaxTokensPerKey { get; set; } = 1000;

    /// <summary>
    /// Context keys to exclude from online requests.
    /// </summary>
    /// <remarks>
    /// Use this to block sensitive information (e.g., "full_document", "user_history", "raw_data").
    /// Case-insensitive matching. Empty by default (no keys excluded).
    /// </remarks>
    public HashSet<string> ExcludeKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Context keys to include (whitelist). If specified, only these keys are sent.
    /// </summary>
    /// <remarks>
    /// When non-empty, acts as a whitelist - only these keys are included.
    /// Takes precedence over ExcludeKeys.
    /// Case-insensitive matching. Empty by default (all keys allowed, subject to ExcludeKeys).
    /// </remarks>
    public HashSet<string> IncludeOnlyKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to log context minimization actions.
    /// </summary>
    /// <remarks>
    /// When true, logs which context keys were removed/truncated and token counts.
    /// Useful for transparency and debugging. Default: true.
    /// </remarks>
    public bool LogMinimization { get; set; } = true;
}
