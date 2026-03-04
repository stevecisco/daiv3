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
    /// Executes multiple online requests, allowing concurrent execution across different providers.
    /// </summary>
    /// <remarks>
    /// Implements MQ-REQ-016.
    /// - When <c>AllowParallelProviderExecution</c> is enabled, requests are dispatched concurrently.
    /// - Provider-specific concurrency limits are still enforced.
    /// - When disabled, requests execute sequentially in input order.
    /// </remarks>
    /// <param name="requests">Requests to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Execution results in the same order as the input requests.</returns>
    Task<IReadOnlyList<ExecutionResult>> ExecuteBatchAsync(
        IReadOnlyList<ExecutionRequest> requests,
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

    /// <summary>
    /// Retries pending requests that were queued while offline.
    /// </summary>
    /// <remarks>
    /// Implements MQ-REQ-013: Processes queued online tasks when system comes back online.
    /// </remarks>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of requests successfully retried</returns>
    Task<int> RetryPendingRequestsAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a request requires user confirmation before execution.
    /// </summary>
    /// <remarks>
    /// Implements MQ-REQ-014: Decision logic based on ConfirmationMode setting.
    /// - Always: Always requires confirmation
    /// - AboveThreshold: Requires confirmation if estimated tokens > threshold
    /// - AutoWithinBudget: Requires confirmation if exceeding budget
    /// - Never: Never requires confirmation
    /// </remarks>
    /// <param name="request">The execution request to check</param>
    /// <param name="provider">Optional specific provider (or null for auto-select)</param>
    /// <returns>True if user confirmation is required</returns>
    bool RequiresConfirmation(ExecutionRequest request, string? provider = null);

    /// <summary>
    /// Gets confirmation details for a request (estimated tokens, cost, provider).
    /// </summary>
    /// <param name="request">The execution request</param>
    /// <param name="provider">Optional specific provider (or null for auto-select)</param>
    /// <returns>Confirmation details including estimated tokens and selected provider</returns>
    ConfirmationDetails GetConfirmationDetails(ExecutionRequest request, string? provider = null);

    /// <summary>
    /// Executes a request with confirmation already granted (bypasses confirmation check).
    /// </summary>
    /// <remarks>
    /// Use this after user has provided explicit confirmation via RequiresConfirmation/GetConfirmationDetails flow.
    /// </remarks>
    /// <param name="request">Execution request</param>
    /// <param name="provider">Specific provider or null for auto-select</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result</returns>
    Task<ExecutionResult> ExecuteWithConfirmationAsync(
        ExecutionRequest request,
        string? provider = null,
        CancellationToken ct = default);
}