using Daiv3.ModelExecution.Exceptions;
using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Routes requests to online AI providers with token budget management.
/// </summary>
/// <remarks>
/// - Supports OpenAI, Azure OpenAI, Anthropic, and other providers
/// - Tracks token usage against daily/monthly budgets
/// - Requires user confirmation above threshold (configurable)
/// - Can execute requests in parallel (unlike Foundry Local)
/// </remarks>
public interface IOnlineProviderRouter
{
    /// <summary>
    /// Executes a request using an online provider.
    /// </summary>
    /// <remarks>
    /// - Routes to configured provider based on task type
    /// - Checks token budget before execution
    /// - May prompt user if above confirmation threshold
    /// - Throws if offline or budget exceeded
    /// </remarks>
    /// <param name="request">Execution request</param>
    /// <param name="provider">Specific provider or null for auto-select</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result</returns>
    /// <exception cref="InvalidOperationException">If offline or no provider configured</exception>
    /// <exception cref="TokenBudgetExceededException">If budget exceeded</exception>
    Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string? provider = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets token usage for a provider.
    /// </summary>
    /// <param name="providerName">Provider name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token usage summary</returns>
    Task<ProviderTokenUsage> GetTokenUsageAsync(
        string providerName,
        CancellationToken ct = default);

    /// <summary>
    /// Lists configured online providers.
    /// </summary>
    /// <returns>Provider names</returns>
    Task<List<string>> ListProvidersAsync();

    /// <summary>
    /// Checks if a provider is available (online and configured).
    /// </summary>
    /// <param name="providerName">Provider name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if available</returns>
    Task<bool> IsProviderAvailableAsync(
        string providerName,
        CancellationToken ct = default);
}
