using Daiv3.ModelExecution.Exceptions;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Routes requests to online AI providers with intelligent model-to-task mapping and token budget management.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-012: Routes based on model-to-task mappings, token budgets, and availability.
/// Smart provider selection considers task type, budget availability, and configured mappings.
/// </remarks>
public class OnlineProviderRouter : IOnlineProviderRouter, IDisposable
{
    private readonly ILogger<OnlineProviderRouter> _logger;
    private readonly OnlineProviderOptions _options;
    private readonly TaskToModelMappingConfiguration _taskModelMappings;
    private readonly Dictionary<string, ProviderTokenUsage> _tokenUsage = new();
    private readonly SemaphoreSlim _concurrencyLimiter;

    public OnlineProviderRouter(
        IOptions<OnlineProviderOptions> options,
        IOptions<TaskToModelMappingConfiguration> taskModelMappings,
        ILogger<OnlineProviderRouter> logger)
    {
        _logger = logger;
        _options = options.Value;
        _taskModelMappings = taskModelMappings.Value;
        _concurrencyLimiter = new SemaphoreSlim(_taskModelMappings.MaxConcurrentRequestsPerProvider);

        // Initialize token usage tracking
        foreach (var provider in _options.Providers.Keys)
        {
            _tokenUsage[provider] = new ProviderTokenUsage
            {
                ProviderName = provider,
                DailyInputLimit = _options.Providers[provider].DailyInputTokenLimit,
                DailyOutputLimit = _options.Providers[provider].DailyOutputTokenLimit,
                MonthlyInputLimit = _options.Providers[provider].MonthlyInputTokenLimit,
                MonthlyOutputLimit = _options.Providers[provider].MonthlyOutputTokenLimit,
                ResetDate = DateTimeOffset.UtcNow.Date.AddDays(1)
            };
        }

        _logger.LogInformation(
            "OnlineProviderRouter initialized with {ProviderCount} providers and task-to-model mappings. " +
            "Max concurrent: {MaxConcurrent}, Allow parallel: {AllowParallel}",
            _options.Providers.Count,
            _taskModelMappings.MaxConcurrentRequestsPerProvider,
            _taskModelMappings.AllowParallelProviderExecution);
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string? provider = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Select provider
        var providerName = provider ?? SelectProvider(request);

        _logger.LogInformation(
            "Routing request {RequestId} to online provider: {Provider}",
            request.Id, providerName);

        // Check budget
        await CheckTokenBudgetAsync(providerName, request);

        // TODO: Integrate with Microsoft.Extensions.AI abstractions
        // - Get IChatClient for provider
        // - Execute completion request
        // - Handle API errors and retries
        // - Track actual token usage

        // Stub implementation
        await Task.Delay(200, ct); // Simulate API call

        var result = new ExecutionResult
        {
            RequestId = request.Id,
            Content = $"[STUB] Processed by {providerName}: {request.Content}",
            Status = ExecutionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            TokenUsage = new TokenUsage
            {
                InputTokens = EstimateTokens(request.Content),
                OutputTokens = 100 // Stub output
            }
        };

        // Update token usage
        UpdateTokenUsage(providerName, result.TokenUsage);

        return result;
    }

    public Task<ProviderTokenUsage> GetTokenUsageAsync(
        string providerName,
        CancellationToken ct = default)
    {
        if (!_tokenUsage.TryGetValue(providerName, out var usage))
        {
            throw new ArgumentException($"Provider '{providerName}' not found", nameof(providerName));
        }

        // Return a copy to avoid exposing internal state
        var snapshot = new ProviderTokenUsage
        {
            ProviderName = usage.ProviderName,
            DailyInputTokens = usage.DailyInputTokens,
            DailyOutputTokens = usage.DailyOutputTokens,
            MonthlyInputTokens = usage.MonthlyInputTokens,
            MonthlyOutputTokens = usage.MonthlyOutputTokens,
            DailyInputLimit = usage.DailyInputLimit,
            DailyOutputLimit = usage.DailyOutputLimit,
            MonthlyInputLimit = usage.MonthlyInputLimit,
            MonthlyOutputLimit = usage.MonthlyOutputLimit,
            ResetDate = usage.ResetDate
        };

        return Task.FromResult(snapshot);
    }

    public Task<List<string>> ListProvidersAsync()
    {
        return Task.FromResult(_options.Providers.Keys.ToList());
    }

    public Task<bool> IsProviderAvailableAsync(
        string providerName,
        CancellationToken ct = default)
    {
        // TODO: Check actual provider availability
        // - Verify API key configured
        // - Check network connectivity
        // - Validate budget remaining

        var isConfigured = _options.Providers.ContainsKey(providerName);
        return Task.FromResult(isConfigured);
    }

    private string SelectProvider(ExecutionRequest request)
    {
        // Implement smart provider selection based on MQ-REQ-012
        // Considers task type, token budget availability, and configured mappings

        // Get task type from request
        var taskType = request.TaskType ?? "[unknown]";

        // Estimate tokens to check context window requirements
        var estimatedInputTokens = EstimateTokens(request.Content);

        // Find best provider for this task type using the mapping configuration
        var mappedProvider = _taskModelMappings.GetBestProviderForTaskType(taskType, estimatedInputTokens);

        if (!string.IsNullOrEmpty(mappedProvider) && IsProviderConfigured(mappedProvider))
        {
            _logger.LogDebug(
                "Selected provider '{Provider}' for task type '{TaskType}' using task-to-model mapping",
                mappedProvider, taskType);
            return mappedProvider;
        }

        // Fall back to checking budget availability
        var availableByBudget = _options.Providers.Keys
            .Where(p => IsProviderWithinDailyBudget(p))
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(availableByBudget))
        {
            _logger.LogInformation(
                "No mapping found for task type '{TaskType}'; selected '{Provider}' based on budget availability",
                taskType, availableByBudget);
            return availableByBudget;
        }

        // Final fallback: first configured provider
        var fallback = _options.Providers.Keys.FirstOrDefault() ?? "openai";

        _logger.LogWarning(
            "Could not find suitable provider for task type '{TaskType}'; using fallback '{Fallback}'",
            taskType, fallback);

        return fallback;
    }

    private bool IsProviderConfigured(string providerName)
    {
        return _options.Providers.ContainsKey(providerName);
    }

    private bool IsProviderWithinDailyBudget(string providerName)
    {
        if (!_tokenUsage.TryGetValue(providerName, out var usage))
        {
            return false;
        }

        return usage.DailyInputTokens < usage.DailyInputLimit &&
               usage.DailyOutputTokens < usage.DailyOutputLimit;
    }

    private async Task CheckTokenBudgetAsync(string providerName, ExecutionRequest request)
    {
        if (!_tokenUsage.TryGetValue(providerName, out var usage))
        {
            return; // No tracking for this provider
        }

        var estimatedTokens = EstimateTokens(request.Content);

        // Check daily budget
        if (usage.DailyInputTokens + estimatedTokens > usage.DailyInputLimit)
        {
            throw new TokenBudgetExceededException(
                providerName,
                estimatedTokens,
                usage.DailyInputLimit - usage.DailyInputTokens);
        }

        // TODO: Prompt user if above confirmation threshold
        await Task.CompletedTask;
    }

    private void UpdateTokenUsage(string providerName, TokenUsage usage)
    {
        if (!_tokenUsage.TryGetValue(providerName, out var providerUsage))
        {
            return;
        }

        providerUsage.DailyInputTokens += usage.InputTokens;
        providerUsage.DailyOutputTokens += usage.OutputTokens;
        providerUsage.MonthlyInputTokens += usage.InputTokens;
        providerUsage.MonthlyOutputTokens += usage.OutputTokens;

        _logger.LogInformation(
            "Token usage for {Provider}: Daily {DailyInput}+{DailyOutput}, Monthly {MonthlyInput}+{MonthlyOutput}",
            providerName,
            providerUsage.DailyInputTokens,
            providerUsage.DailyOutputTokens,
            providerUsage.MonthlyInputTokens,
            providerUsage.MonthlyOutputTokens);
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    public void Dispose()
    {
        _concurrencyLimiter?.Dispose();
    }
}
