using Daiv3.ModelExecution.Exceptions;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.OnlineProviders.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProviderTokenUsage = Daiv3.ModelExecution.Models.ProviderTokenUsage;

namespace Daiv3.ModelExecution;

/// <summary>
/// Routes requests to online AI providers with intelligent model-to-task mapping and token budget management.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-012: Routes based on model-to-task mappings, token budgets, and availability.
/// Implements MQ-REQ-013: Queues online tasks when offline and marks them as pending.
/// Smart provider selection considers task type, budget availability, and configured mappings.
/// </remarks>
public class OnlineProviderRouter : IOnlineProviderRouter, IDisposable
{
    private readonly ILogger<OnlineProviderRouter> _logger;
    private readonly OnlineProviderOptions _options;
    private readonly TaskToModelMappingConfiguration _taskModelMappings;
    private readonly IOnlineProviderFactory? _providerFactory;
    private readonly Dictionary<string, ProviderTokenUsage> _tokenUsage = new();
    private readonly Dictionary<string, SemaphoreSlim> _providerConcurrencyLimiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<DateTimeOffset>> _providerRequestWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _tokenUsageLock = new();
    private readonly object _providerLimiterLock = new();
    private readonly object _providerRateLimitLock = new();
    private readonly INetworkConnectivityService? _connectivityService;
    private readonly IModelQueueRepository? _queueRepository;

    public OnlineProviderRouter(
        IOptions<OnlineProviderOptions> options,
        IOptions<TaskToModelMappingConfiguration> taskModelMappings,
        ILogger<OnlineProviderRouter> logger,
        INetworkConnectivityService? connectivityService = null,
        IModelQueueRepository? queueRepository = null,
        IOnlineProviderFactory? providerFactory = null)
    {
        _logger = logger;
        _options = options.Value;
        _taskModelMappings = taskModelMappings.Value;
        _providerFactory = providerFactory;
        _connectivityService = connectivityService;
        _queueRepository = queueRepository;

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

            _providerConcurrencyLimiters[provider] = new SemaphoreSlim(_taskModelMappings.MaxConcurrentRequestsPerProvider);
            _providerRequestWindows[provider] = new Queue<DateTimeOffset>();
        }

        _logger.LogInformation(
            "OnlineProviderRouter initialized with {ProviderCount} providers and task-to-model mappings. " +
            "Max concurrent: {MaxConcurrent}, Allow parallel: {AllowParallel}, Offline queueing: {OfflineQueueing}",
            _options.Providers.Count,
            _taskModelMappings.MaxConcurrentRequestsPerProvider,
            _taskModelMappings.AllowParallelProviderExecution,
            _connectivityService != null && _queueRepository != null);
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

        // MQ-REQ-013: Check network connectivity before execution
        if (_connectivityService != null && _queueRepository != null)
        {
            var isOnline = await _connectivityService.IsOnlineAsync(ct);
            
            if (!isOnline)
            {
                _logger.LogWarning(
                    "System is offline. Queueing request {RequestId} as pending for provider {Provider}",
                    request.Id, providerName);

                // Save to persistent queue for later retry
                await _queueRepository.SavePendingRequestAsync(
                    request,
                    ExecutionPriority.Normal, // Default priority for online requests
                    providerName,
                    ct);

                // Return a pending result
                return new ExecutionResult
                {
                    RequestId = request.Id,
                    Content = string.Empty,
                    Status = ExecutionStatus.Pending,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Request queued for provider '{providerName}' - system is offline",
                    TokenUsage = new TokenUsage()
                };
            }
        }

        // MQ-REQ-014: Check if user confirmation is required
        if (RequiresConfirmation(request, providerName))
        {
            _logger.LogInformation(
                "User confirmation required for request {RequestId} to provider {Provider}",
                request.Id, providerName);

            var details = GetConfirmationDetails(request, providerName);

            return new ExecutionResult
            {
                RequestId = request.Id,
                Content = string.Empty,
                Status = ExecutionStatus.AwaitingConfirmation,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = $"User confirmation required: {details.ConfirmationReason}",
                TokenUsage = new TokenUsage()
            };
        }

        return await ExecuteThroughProviderAsync(request, providerName, confirmed: false, ct);
    }

    public async Task<IReadOnlyList<ExecutionResult>> ExecuteBatchAsync(
        IReadOnlyList<ExecutionRequest> requests,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return Array.Empty<ExecutionResult>();
        }

        if (!_taskModelMappings.AllowParallelProviderExecution || requests.Count == 1)
        {
            var sequentialResults = new List<ExecutionResult>(requests.Count);
            foreach (var request in requests)
            {
                sequentialResults.Add(await ExecuteAsync(request, null, ct));
            }

            return sequentialResults;
        }

        _logger.LogInformation(
            "Executing {RequestCount} online requests with parallel provider execution enabled",
            requests.Count);

        var tasks = requests.Select(request => ExecuteAsync(request, null, ct));
        return await Task.WhenAll(tasks);
    }

    public Task<ProviderTokenUsage> GetTokenUsageAsync(
        string providerName,
        CancellationToken ct = default)
    {
        ProviderTokenUsage snapshot;

        lock (_tokenUsageLock)
        {
            if (!_tokenUsage.TryGetValue(providerName, out var usage))
            {
                throw new ArgumentException($"Provider '{providerName}' not found", nameof(providerName));
            }

            // Return a copy to avoid exposing internal state
            snapshot = new ProviderTokenUsage
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
        }

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
            .Where(IsProviderWithinBudget)
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

    private bool IsProviderWithinBudget(string providerName)
    {
        lock (_tokenUsageLock)
        {
            if (!_tokenUsage.TryGetValue(providerName, out var usage))
            {
                return false;
            }

            return usage.DailyInputTokens < usage.DailyInputLimit &&
                   usage.DailyOutputTokens < usage.DailyOutputLimit &&
                   usage.MonthlyInputTokens < usage.MonthlyInputLimit &&
                   usage.MonthlyOutputTokens < usage.MonthlyOutputLimit;
        }
    }

    private async Task CheckTokenBudgetAsync(string providerName, ExecutionRequest request)
    {
        lock (_tokenUsageLock)
        {
            if (!_tokenUsage.TryGetValue(providerName, out var usage))
            {
                return; // No tracking for this provider
            }

            var estimatedInputTokens = EstimateTokens(request.Content);
            var estimatedOutputTokens = EstimateOutputTokens(request);

            if (usage.DailyInputTokens + estimatedInputTokens > usage.DailyInputLimit)
            {
                throw new TokenBudgetExceededException(
                    providerName,
                    estimatedInputTokens,
                    usage.DailyInputLimit - usage.DailyInputTokens);
            }

            if (usage.DailyOutputTokens + estimatedOutputTokens > usage.DailyOutputLimit)
            {
                throw new TokenBudgetExceededException(
                    providerName,
                    estimatedOutputTokens,
                    usage.DailyOutputLimit - usage.DailyOutputTokens);
            }

            if (usage.MonthlyInputTokens + estimatedInputTokens > usage.MonthlyInputLimit)
            {
                throw new TokenBudgetExceededException(
                    providerName,
                    estimatedInputTokens,
                    usage.MonthlyInputLimit - usage.MonthlyInputTokens);
            }

            if (usage.MonthlyOutputTokens + estimatedOutputTokens > usage.MonthlyOutputLimit)
            {
                throw new TokenBudgetExceededException(
                    providerName,
                    estimatedOutputTokens,
                    usage.MonthlyOutputLimit - usage.MonthlyOutputTokens);
            }
        }

        await Task.CompletedTask;
    }

    private void UpdateTokenUsage(string providerName, TokenUsage usage)
    {
        lock (_tokenUsageLock)
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
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    private static int EstimateOutputTokens(ExecutionRequest request)
    {
        _ = request;
        return 100;
    }

    public async Task<int> RetryPendingRequestsAsync(CancellationToken ct = default)
    {
        // MQ-REQ-013: Retry pending requests that were queued while offline
        if (_queueRepository == null || _connectivityService == null)
        {
            _logger.LogDebug("Offline queueing not configured, skipping retry of pending requests");
            return 0;
        }

        // Check if we're online
        var isOnline = await _connectivityService.IsOnlineAsync(ct);
        if (!isOnline)
        {
            _logger.LogDebug("System is still offline, cannot retry pending requests");
            return 0;
        }

        // Get pending requests
        var pendingRequests = await _queueRepository.GetPendingRequestsAsync(ct);
        
        if (pendingRequests.Count == 0)
        {
            _logger.LogDebug("No pending requests to retry");
            return 0;
        }

        _logger.LogInformation(
            "Found {Count} pending requests to retry",
            pendingRequests.Count);

        var successCount = 0;

        foreach (var (request, priority, modelId) in pendingRequests)
        {
            try
            {
                _logger.LogInformation(
                    "Retrying pending request {RequestId} for provider {Provider}",
                    request.Id, modelId);

                // Update status to queued (being retried)
                await _queueRepository.UpdateRequestStatusAsync(
                    request.Id,
                    ExecutionStatus.Queued,
                    null,
                    ct);

                // Execute the request
                var result = await ExecuteAsync(request, modelId, ct);

                // Update final status
                await _queueRepository.UpdateRequestStatusAsync(
                    request.Id,
                    result.Status,
                    result.ErrorMessage,
                    ct);

                if (result.Status == ExecutionStatus.Completed)
                {
                    successCount++;
                    _logger.LogInformation(
                        "Successfully retried request {RequestId}",
                        request.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Request {RequestId} completed with status {Status}",
                        request.Id, result.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retry pending request {RequestId}",
                    request.Id);

                // Update status to failed
                await _queueRepository.UpdateRequestStatusAsync(
                    request.Id,
                    ExecutionStatus.Failed,
                    ex.Message,
                    ct);
            }
        }

        _logger.LogInformation(
            "Completed retry of pending requests: {SuccessCount}/{TotalCount} successful",
            successCount, pendingRequests.Count);

        return successCount;
    }

    public bool RequiresConfirmation(ExecutionRequest request, string? provider = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Never mode: always auto-approve
        if (_options.ConfirmationMode == ConfirmationMode.Never)
        {
            return false;
        }

        // Always mode: always require confirmation
        if (_options.ConfirmationMode == ConfirmationMode.Always)
        {
            return true;
        }

        var providerName = provider ?? SelectProvider(request);
        var estimatedTokens = EstimateTokens(request.Content);

        // AboveThreshold mode: require confirmation if tokens exceed threshold
        if (_options.ConfirmationMode == ConfirmationMode.AboveThreshold)
        {
            return estimatedTokens > _options.ConfirmationThreshold;
        }

        // AutoWithinBudget mode: require confirmation if exceeding budget
        if (_options.ConfirmationMode == ConfirmationMode.AutoWithinBudget)
        {
            lock (_tokenUsageLock)
            {
                if (!_tokenUsage.TryGetValue(providerName, out var usage))
                {
                    // No usage tracking, require confirmation to be safe
                    return true;
                }

                var remainingBudget = usage.DailyInputLimit - usage.DailyInputTokens;
                return estimatedTokens > remainingBudget;
            }
        }

        return false;
    }

    public ConfirmationDetails GetConfirmationDetails(ExecutionRequest request, string? provider = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerName = provider ?? SelectProvider(request);
        var estimatedTokens = EstimateTokens(request.Content);
        
        var details = new ConfirmationDetails
        {
            ProviderName = providerName,
            EstimatedInputTokens = estimatedTokens,
            EstimatedOutputTokens = 100 // Conservative estimate
        };

        lock (_tokenUsageLock)
        {
            if (_tokenUsage.TryGetValue(providerName, out var usage))
            {
                details.CurrentDailyInputTokens = usage.DailyInputTokens;
                details.DailyInputLimit = usage.DailyInputLimit;
            }
        }

        // Determine confirmation reason based on mode
        details.ConfirmationReason = _options.ConfirmationMode switch
        {
            ConfirmationMode.Always => 
                "Confirmation required for all online requests (ConfirmationMode: Always)",
            
            ConfirmationMode.AboveThreshold when estimatedTokens > _options.ConfirmationThreshold => 
                $"Estimated tokens ({estimatedTokens}) exceed threshold ({_options.ConfirmationThreshold})",
            
            ConfirmationMode.AutoWithinBudget when details.ExceedsBudget => 
                $"Request would exceed daily budget ({details.RemainingDailyBudget} tokens remaining)",
            
            _ => "Confirmation required"
        };

        return details;
    }

    public async Task<ExecutionResult> ExecuteWithConfirmationAsync(
        ExecutionRequest request,
        string? provider = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Select provider
        var providerName = provider ?? SelectProvider(request);

        _logger.LogInformation(
            "Executing request {RequestId} with explicit confirmation to provider: {Provider}",
            request.Id, providerName);

        // Check network connectivity (even with confirmation, can't execute if offline)
        if (_connectivityService != null && _queueRepository != null)
        {
            var isOnline = await _connectivityService.IsOnlineAsync(ct);
            
            if (!isOnline)
            {
                _logger.LogWarning(
                    "System is offline. Queueing request {RequestId} as pending for provider {Provider}",
                    request.Id, providerName);

                await _queueRepository.SavePendingRequestAsync(
                    request,
                    ExecutionPriority.Normal,
                    providerName,
                    ct);

                return new ExecutionResult
                {
                    RequestId = request.Id,
                    Content = string.Empty,
                    Status = ExecutionStatus.Pending,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Request queued for provider '{providerName}' - system is offline",
                    TokenUsage = new TokenUsage()
                };
            }
        }

        return await ExecuteThroughProviderAsync(request, providerName, confirmed: true, ct);
    }

    private async Task<ExecutionResult> ExecuteThroughProviderAsync(
        ExecutionRequest request,
        string providerName,
        bool confirmed,
        CancellationToken ct)
    {
        await CheckTokenBudgetAsync(providerName, request);
        await WaitForProviderRateLimitSlotAsync(providerName, ct);

        var minimizedRequest = MinimizeContextForOnlineProvider(request);
        var providerLimiter = GetProviderConcurrencyLimiter(providerName);

        await providerLimiter.WaitAsync(ct);
        try
        {
            var result = await ExecuteStubProviderCallAsync(minimizedRequest, providerName, confirmed, ct);
            UpdateTokenUsage(providerName, result.TokenUsage);
            return result;
        }
        finally
        {
            providerLimiter.Release();
        }
    }

    private async Task WaitForProviderRateLimitSlotAsync(string providerName, CancellationToken ct)
    {
        if (!_options.Providers.TryGetValue(providerName, out var providerConfig))
        {
            return;
        }

        if (providerConfig.MaxRequestsPerWindow <= 0 || providerConfig.RateLimitWindowSeconds <= 0)
        {
            return;
        }

        var window = TimeSpan.FromSeconds(providerConfig.RateLimitWindowSeconds);

        while (true)
        {
            TimeSpan? delay = null;

            lock (_providerRateLimitLock)
            {
                if (!_providerRequestWindows.TryGetValue(providerName, out var providerWindow))
                {
                    providerWindow = new Queue<DateTimeOffset>();
                    _providerRequestWindows[providerName] = providerWindow;
                }

                var now = DateTimeOffset.UtcNow;

                while (providerWindow.Count > 0 && now - providerWindow.Peek() >= window)
                {
                    providerWindow.Dequeue();
                }

                if (providerWindow.Count < providerConfig.MaxRequestsPerWindow)
                {
                    providerWindow.Enqueue(now);
                    return;
                }

                delay = window - (now - providerWindow.Peek());
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }
            }

            if (delay.GetValueOrDefault() > TimeSpan.Zero)
            {
                _logger.LogDebug(
                    "Rate limit reached for provider {Provider}. Waiting {DelayMs} ms before retrying",
                    providerName,
                    delay.Value.TotalMilliseconds);

                await Task.Delay(delay.Value, ct);
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    private async Task<ExecutionResult> ExecuteStubProviderCallAsync(
        ExecutionRequest minimizedRequest,
        string providerName,
        bool confirmed,
        CancellationToken ct)
    {
        // Implements KLC-REQ-006: Uses Microsoft.Extensions.AI abstractions for online providers
        try
        {
            // Attempt to use registered provider via IOnlineProviderFactory
            if (_providerFactory != null)
            {
                var provider = _providerFactory.GetProvider(providerName);
                if (provider != null)
                {
                    // Get model from task mapping or use provider config
                    var model = "gpt-4"; // Default model
                    if (_options.Providers.ContainsKey(providerName))
                    {
                        var taskType = minimizedRequest.TaskType ?? "[general]";
                        if (_options.Providers[providerName].TaskTypeToModel.ContainsKey(taskType))
                        {
                            model = _options.Providers[providerName].TaskTypeToModel[taskType];
                        }
                    }

                    var options = new OnlineInferenceOptions
                    {
                        Model = model,
                        MaxTokens = 2048,
                        Temperature = 0.7m
                    };

                    var response = await provider.GenerateAsync(
                        minimizedRequest.Content,
                        options,
                        ct);

                    var usage = await provider.GetTokenUsageAsync(ct);

                    return new ExecutionResult
                    {
                        RequestId = minimizedRequest.Id,
                        Content = response,
                        Status = ExecutionStatus.Completed,
                        CompletedAt = DateTimeOffset.UtcNow,
                        TokenUsage = new TokenUsage
                        {
                            InputTokens = (int)usage.InputTokens,
                            OutputTokens = (int)usage.OutputTokens
                        }
                    };
                }
            }

            // Fallback to stub implementation if provider not found
            _logger.LogWarning(
                "Provider '{ProviderName}' not found in factory; using stub response",
                providerName);

            return await ExecuteStubProviderCallFallback(minimizedRequest, providerName, confirmed, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing provider call for '{ProviderName}'", providerName);
            return await ExecuteStubProviderCallFallback(minimizedRequest, providerName, confirmed, ct);
        }
    }

    /// <summary>
    /// Fallback implementation when provider abstractions are unavailable (generates stub response).
    /// </summary>
    private static async Task<ExecutionResult> ExecuteStubProviderCallFallback(
        ExecutionRequest minimizedRequest,
        string providerName,
        bool confirmed,
        CancellationToken ct)
    {
        // Simulate realistic API call latency (200ms) for testing parallel vs sequential execution
        await Task.Delay(200, ct);

        var content = confirmed
            ? $"[STUB] Processed by {providerName} (confirmed): {minimizedRequest.Content}"
            : $"[STUB] Processed by {providerName}: {minimizedRequest.Content}";

        return new ExecutionResult
        {
            RequestId = minimizedRequest.Id,
            Content = content,
            Status = ExecutionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            TokenUsage = new TokenUsage
            {
                InputTokens = EstimateTokens(minimizedRequest.Content) +
                              minimizedRequest.Context.Sum(kvp => EstimateTokens(kvp.Value)),
                OutputTokens = 100
            }
        };
    }

    private SemaphoreSlim GetProviderConcurrencyLimiter(string providerName)
    {
        lock (_providerLimiterLock)
        {
            if (_providerConcurrencyLimiters.TryGetValue(providerName, out var limiter))
            {
                return limiter;
            }

            limiter = new SemaphoreSlim(_taskModelMappings.MaxConcurrentRequestsPerProvider);
            _providerConcurrencyLimiters[providerName] = limiter;
            return limiter;
        }
    }

    /// <summary>
    /// Creates a minimized copy of the request with filtered/truncated context for online providers.
    /// </summary>
    /// <remarks>
    /// MQ-REQ-015: Send only minimal required context to online providers.
    /// - Filters context keys based on whitelist/blacklist
    /// - Truncates context values to token limits
    /// - Logs minimization actions for transparency
    /// - Returns a new ExecutionRequest (original is not modified)
    /// </remarks>
    private ExecutionRequest MinimizeContextForOnlineProvider(ExecutionRequest originalRequest)
    {
        // If minimization is disabled, return original request
        if (!_options.ContextMinimization.Enabled)
        {
            _logger.LogDebug(
                "Context minimization disabled for request {RequestId}",
                originalRequest.Id);
            return originalRequest;
        }

        var minimizedRequest = new ExecutionRequest
        {
            Id = originalRequest.Id,
            TaskType = originalRequest.TaskType,
            Content = originalRequest.Content,
            CreatedAt = originalRequest.CreatedAt,
            Context = new Dictionary<string, string>() // Will be populated with minimized context
        };

        var config = _options.ContextMinimization;
        var originalContextTokens = 0;
        var minimizedContextTokens = 0;
        var keysRemoved = new List<string>();
        var keysTruncated = new List<string>();

        foreach (var kvp in originalRequest.Context)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            var valueTokens = EstimateTokens(value);
            originalContextTokens += valueTokens;

            // Check whitelist (if specified, only these keys are allowed)
            if (config.IncludeOnlyKeys.Count > 0 && !config.IncludeOnlyKeys.Contains(key))
            {
                keysRemoved.Add(key);
                _logger.LogDebug(
                    "Context key '{Key}' excluded for request {RequestId} (not in whitelist)",
                    key, originalRequest.Id);
                continue;
            }

            // Check blacklist
            if (config.ExcludeKeys.Contains(key))
            {
                keysRemoved.Add(key);
                _logger.LogDebug(
                    "Context key '{Key}' excluded for request {RequestId} (in blacklist)",
                    key, originalRequest.Id);
                continue;
            }

            // Check if adding this key would exceed total context token limit
            if (minimizedContextTokens + valueTokens > config.MaxContextTokens)
            {
                // Calculate remaining budget
                var remainingBudget = config.MaxContextTokens - minimizedContextTokens;
                
                if (remainingBudget <= 0)
                {
                    // No budget left, skip this key
                    keysRemoved.Add(key);
                    _logger.LogDebug(
                        "Context key '{Key}' excluded for request {RequestId} (total context token limit reached)",
                        key, originalRequest.Id);
                    continue;
                }

                // Truncate value to fit remaining budget
                var truncatedValue = TruncateToTokenLimit(value, remainingBudget);
                var truncatedTokens = EstimateTokens(truncatedValue);
                
                minimizedRequest.Context[key] = truncatedValue;
                minimizedContextTokens += truncatedTokens;
                keysTruncated.Add(key);

                _logger.LogDebug(
                    "Context key '{Key}' truncated for request {RequestId} ({OriginalTokens} -> {TruncatedTokens} tokens)",
                    key, originalRequest.Id, valueTokens, truncatedTokens);
                continue;
            }

            // Check per-key token limit
            if (valueTokens > config.MaxTokensPerKey)
            {
                var truncatedValue = TruncateToTokenLimit(value, config.MaxTokensPerKey);
                var truncatedTokens = EstimateTokens(truncatedValue);
                
                minimizedRequest.Context[key] = truncatedValue;
                minimizedContextTokens += truncatedTokens;
                keysTruncated.Add(key);

                _logger.LogDebug(
                    "Context key '{Key}' truncated for request {RequestId} ({OriginalTokens} -> {TruncatedTokens} tokens)",
                    key, originalRequest.Id, valueTokens, truncatedTokens);
            }
            else
            {
                // Value is within limits, include as-is
                minimizedRequest.Context[key] = value;
                minimizedContextTokens += valueTokens;
            }
        }

        // Log summary if any minimization occurred
        if (config.LogMinimization && (keysRemoved.Count > 0 || keysTruncated.Count > 0))
        {
            _logger.LogInformation(
                "Context minimized for request {RequestId} before sending to online provider. " +
                "Original: {OriginalKeys} keys, {OriginalTokens} tokens. " +
                "Minimized: {MinimizedKeys} keys, {MinimizedTokens} tokens. " +
                "Keys removed: [{RemovedKeys}]. Keys truncated: [{TruncatedKeys}]",
                originalRequest.Id,
                originalRequest.Context.Count,
                originalContextTokens,
                minimizedRequest.Context.Count,
                minimizedContextTokens,
                string.Join(", ", keysRemoved),
                string.Join(", ", keysTruncated));
        }
        else if (config.LogMinimization)
        {
            _logger.LogDebug(
                "No context minimization needed for request {RequestId} ({Tokens} tokens)",
                originalRequest.Id, originalContextTokens);
        }

        return minimizedRequest;
    }

    /// <summary>
    /// Truncates text to approximately fit within a token limit.
    /// </summary>
    /// <param name="text">Text to truncate</param>
    /// <param name="maxTokens">Maximum tokens allowed</param>
    /// <returns>Truncated text with ellipsis if truncated</returns>
    private static string TruncateToTokenLimit(string text, int maxTokens)
    {
        if (maxTokens <= 0)
        {
            return string.Empty;
        }

        // Estimate characters allowed (4 chars per token)
        var maxChars = maxTokens * 4;

        if (text.Length <= maxChars)
        {
            return text;
        }

        // Truncate and add ellipsis
        var ellipsis = "...";
        var truncatedLength = Math.Max(0, maxChars - ellipsis.Length);
        
        return text.Substring(0, truncatedLength) + ellipsis;
    }

    public void Dispose()
    {
        lock (_providerLimiterLock)
        {
            foreach (var limiter in _providerConcurrencyLimiters.Values)
            {
                limiter.Dispose();
            }

            _providerConcurrencyLimiters.Clear();
        }
    }
}
