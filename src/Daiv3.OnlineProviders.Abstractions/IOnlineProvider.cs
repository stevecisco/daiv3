using Microsoft.Extensions.AI;

namespace Daiv3.OnlineProviders.Abstractions;

/// <summary>
/// Options for online inference requests via Microsoft.Extensions.AI abstractions.
/// </summary>
public class OnlineInferenceOptions
{
    /// <summary>
    /// Model ID or name to use for inference (e.g., "gpt-4", "claude-3-opus").
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// Maximum number of tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Sampling temperature for response generation (0.0 to 2.0).
    /// Lower values make output more deterministic, higher values more creative.
    /// </summary>
    public decimal Temperature { get; set; } = 0.7m;

    /// <summary>
    /// System prompt(s) to guide model behavior.
    /// </summary>
    public List<string> SystemPrompts { get; set; } = new();

    /// <summary>
    /// Top-p (nucleus sampling) parameter for diversity.
    /// </summary>
    public decimal? TopP { get; set; }

    /// <summary>
    /// Frequency penalty to reduce repetition.
    /// </summary>
    public decimal? FrequencyPenalty { get; set; }

    /// <summary>
    /// Presence penalty to encourage new topics.
    /// </summary>
    public decimal? PresencePenalty { get; set; }
}

/// <summary>
/// Token usage tracking for a provider.
/// </summary>
public class ProviderTokenUsage
{
    /// <summary>
    /// Total input tokens used.
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Total output tokens generated.
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// Total tokens (input + output).
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Date when usage was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an online AI provider accessible via Microsoft.Extensions.AI abstractions.
/// </summary>
/// <remarks>
/// Implements KLC-REQ-006: Uses Microsoft.Extensions.AI abstractions for online providers.
/// Supports OpenAI, Azure OpenAI, Anthropic, and other providers through a unified interface.
/// Each provider implementation wraps an IChatClient for request handling.
/// </remarks>
public interface IOnlineProvider
{
    /// <summary>
    /// Gets the unique name/identifier for this provider.
    /// </summary>
    /// <remarks>
    /// Examples: "openai", "azure-openai", "anthropic"
    /// </remarks>
    string ProviderName { get; }

    /// <summary>
    /// Gets the underlying IChatClient from Microsoft.Extensions.AI abstractions.
    /// </summary>
    /// <remarks>
    /// This provides the unified interface to underlying provider APIs (OpenAI SDK, Azure SDK, etc.).
    /// </remarks>
    IChatClient ChatClient { get; }

    /// <summary>
    /// Generates a response for the given prompt using this provider.
    /// </summary>
    /// <param name="prompt">The input prompt for generation.</param>
    /// <param name="options">Inference options (model, temperature, max tokens, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated text response.</returns>
    /// <exception cref="OperationCanceledException">If cancellation is requested.</exception>
    /// <exception cref="InvalidOperationException">If provider is unavailable.</exception>
    Task<string> GenerateAsync(
        string prompt,
        OnlineInferenceOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if this provider is currently available (reachable, authenticated, funded).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if provider is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Estimates the cost of a request based on token counts.
    /// </summary>
    /// <param name="inputTokens">Number of input tokens.</param>
    /// <param name="outputTokens">Number of output tokens.</param>
    /// <returns>Estimated cost in USD.</returns>
    decimal GetEstimatedCost(int inputTokens, int outputTokens);

    /// <summary>
    /// Gets the token usage statistics for this provider.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token usage information.</returns>
    Task<ProviderTokenUsage> GetTokenUsageAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the context window size (maximum tokens) for the specified model.
    /// </summary>
    /// <param name="model">Model ID (e.g., "gpt-4", "claude-3-opus").</param>
    /// <returns>Maximum context window size in tokens, or null if unknown.</returns>
    int? GetContextWindowSize(string model);
}
