using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Daiv3.OnlineProviders.Abstractions;

/// <summary>
/// Base class for online provider implementations using Microsoft.Extensions.AI abstractions.
/// </summary>
/// <remarks>
/// Provides common functionality for all online providers:
/// - IChatClient wrapping and integration
/// - Logging infrastructure
/// - Token usage tracking
/// - Error handling and retry logic
/// 
/// Implementations should override GenerateAsync to call their specific API.
/// </remarks>
public abstract class OnlineProviderBase : IOnlineProvider
{
    protected readonly ILogger<OnlineProviderBase> Logger;
    protected ProviderTokenUsage _tokenUsage = new();

    /// <summary>
    /// Gets the unique name/identifier for this provider.
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets the underlying IChatClient from Microsoft.Extensions.AI abstractions.
    /// </summary>
    public abstract IChatClient ChatClient { get; }

    protected OnlineProviderBase(ILogger<OnlineProviderBase> logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a response for the given prompt using this provider.
    /// </summary>
    public abstract Task<string> GenerateAsync(
        string prompt,
        OnlineInferenceOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if this provider is currently available.
    /// </summary>
    /// <remarks>
    /// Default implementation checks if ChatClient is initialized.
    /// Derived classes should override to add API health checks.
    /// </remarks>
    public virtual async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Derived classes should override with actual availability checks
            return ChatClient != null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error checking availability for provider '{ProviderName}'", ProviderName);
            return false;
        }
    }

    /// <summary>
    /// Estimates the cost of a request based on token counts.
    /// </summary>
    /// <remarks>
    /// Default implementation returns 0.
    /// Derived classes should override with actual pricing models.
    /// </remarks>
    public virtual decimal GetEstimatedCost(int inputTokens, int outputTokens)
    {
        // Derived classes should override with provider-specific pricing
        return 0m;
    }

    /// <summary>
    /// Gets the token usage statistics for this provider.
    /// </summary>
    public virtual async Task<ProviderTokenUsage> GetTokenUsageAsync(CancellationToken ct = default)
    {
        return new ProviderTokenUsage
        {
            InputTokens = _tokenUsage.InputTokens,
            OutputTokens = _tokenUsage.OutputTokens,
            TotalTokens = _tokenUsage.TotalTokens,
            LastUpdated = _tokenUsage.LastUpdated
        };
    }

    /// <summary>
    /// Gets the context window size (maximum tokens) for the specified model.
    /// </summary>
    /// <remarks>
    /// Default implementation returns null.
    /// Derived classes should override with provider-specific context windows.
    /// </remarks>
    public virtual int? GetContextWindowSize(string model)
    {
        // Derived classes should override with provider-specific context window information
        return null;
    }

    /// <summary>
    /// Updates token usage statistics based on actual API response.
    /// </summary>
    /// <remarks>
    /// Called internally to track cumulative token usage.
    /// </remarks>
    protected void UpdateTokenUsage(int inputTokens, int outputTokens)
    {
        _tokenUsage.InputTokens += inputTokens;
        _tokenUsage.OutputTokens += outputTokens;
        _tokenUsage.TotalTokens += inputTokens + outputTokens;
        _tokenUsage.LastUpdated = DateTimeOffset.UtcNow;

        Logger.LogDebug(
            "Updated token usage for '{ProviderName}': +{InputTokens} input, +{OutputTokens} output (total: {TotalTokens})",
            ProviderName, inputTokens, outputTokens, _tokenUsage.TotalTokens);
    }

    /// <summary>
    /// Validates inference options.
    /// </summary>
    /// <remarks>
    /// Called before making API requests to catch configuration errors early.
    /// </remarks>
    protected virtual void ValidateOptions(OnlineInferenceOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.Model))
            throw new ArgumentException("Model must be specified in options", nameof(options));

        if (options.MaxTokens < 1 || options.MaxTokens > 200000)
            throw new ArgumentException("MaxTokens must be between 1 and 200000", nameof(options));

        if (options.Temperature < 0m || options.Temperature > 2m)
            throw new ArgumentException("Temperature must be between 0 and 2", nameof(options));

        if (options.TopP.HasValue && (options.TopP < 0m || options.TopP > 1m))
            throw new ArgumentException("TopP must be between 0 and 1", nameof(options));
    }
}
