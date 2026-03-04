using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.ModelExecution;

/// <summary>
/// Priority-based model execution queue with intelligent scheduling.
/// </summary>
/// <remarks>
/// Minimizes model thrashing in Foundry Local by batching same-model requests.
/// - P0 (Immediate): Preempts current execution, switches model if needed
/// - P1 (Normal): Batches with current model, then executes
/// - P2 (Background): Drains same-model queue before switching
/// </remarks>
public class ModelQueue : IModelQueue
{
    private readonly IFoundryBridge _foundryBridge;
    private readonly IModelLifecycleManager _modelLifecycleManager;
    private readonly IOnlineProviderRouter _onlineRouter;
    private readonly ILogger<ModelQueue> _logger;
    private readonly ModelQueueOptions _options;

    private readonly Channel<QueuedRequest> _immediateChannel;
    private readonly Channel<QueuedRequest> _normalChannel;
    private readonly Channel<QueuedRequest> _backgroundChannel;

    private readonly ConcurrentDictionary<Guid, QueuedRequest> _requests = new();
    private string? _currentModelId;
    private DateTimeOffset _lastModelSwitch = DateTimeOffset.UtcNow;
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    // Preemption support for P0 requests
    private QueuedRequest? _currentlyExecuting;
    private CancellationTokenSource? _currentExecutionCts;
    private readonly object _executionStateLock = new();

    // MQ-NFR-001 observability metrics
    private long _nextSequenceNumber;
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalCompleted;
    private long _totalFailed;
    private long _totalPreempted;
    private long _totalLocalExecutions;
    private long _totalOnlineExecutions;
    private long _totalModelSwitches;
    private long _totalQueueWaitMs;
    private long _totalExecutionDurationMs;
    private long _measuredExecutionCount;
    private long _inFlightExecutions;
    private long _lastDequeuedAtUnixTimeMs;

    public ModelQueue(
        IFoundryBridge foundryBridge,
        IModelLifecycleManager modelLifecycleManager,
        IOnlineProviderRouter onlineRouter,
        IOptions<ModelQueueOptions> options,
        ILogger<ModelQueue> logger)
    {
        _foundryBridge = foundryBridge;
        _modelLifecycleManager = modelLifecycleManager;
        _onlineRouter = onlineRouter;
        _options = options.Value;
        _logger = logger;

        // Unbounded channels for each priority level
        _immediateChannel = Channel.CreateUnbounded<QueuedRequest>();
        _normalChannel = Channel.CreateUnbounded<QueuedRequest>();
        _backgroundChannel = Channel.CreateUnbounded<QueuedRequest>();

        // Start background processing
        _ = Task.Run(ProcessQueueAsync);
    }

    public async Task<Guid> EnqueueAsync(
        ExecutionRequest request,
        ExecutionPriority priority = ExecutionPriority.Normal,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queuedRequest = new QueuedRequest
        {
            Request = request,
            Priority = priority,
            EnqueuedAt = DateTimeOffset.UtcNow,
            SequenceNumber = Interlocked.Increment(ref _nextSequenceNumber),
            CompletionSource = new TaskCompletionSource<ExecutionResult>()
        };

        Interlocked.Increment(ref _totalEnqueued);
        _requests[request.Id] = queuedRequest;

        // Route to appropriate channel based on priority
        var channel = priority switch
        {
            ExecutionPriority.Immediate => _immediateChannel,
            ExecutionPriority.Normal => _normalChannel,
            ExecutionPriority.Background => _backgroundChannel,
            _ => _normalChannel
        };

        await channel.Writer.WriteAsync(queuedRequest, ct);

        _logger.LogInformation(
            "Enqueued request {RequestId} (seq={SequenceNumber}) with priority {Priority}, task type: {TaskType}",
            request.Id, queuedRequest.SequenceNumber, priority, request.TaskType);

        // P0 (Immediate) preemption: cancel any in-progress P1/P2 request
        if (priority == ExecutionPriority.Immediate)
        {
            lock (_executionStateLock)
            {
                if (_currentlyExecuting != null &&
                    _currentlyExecuting.Priority != ExecutionPriority.Immediate &&
                    _currentExecutionCts != null && !_currentExecutionCts.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "P0 request {P0RequestId} preempting in-progress {Priority} request {PreemptedRequestId}",
                        request.Id, _currentlyExecuting.Priority, _currentlyExecuting.Request.Id);

                    _currentExecutionCts.Cancel();
                }
            }
        }

        return request.Id;
    }

    public async Task<ExecutionResult> ProcessAsync(Guid requestId, CancellationToken ct = default)
    {
        if (!_requests.TryGetValue(requestId, out var queuedRequest))
        {
            throw new InvalidOperationException($"Request {requestId} not found in queue");
        }

        // Wait for completion
        using var registration = ct.Register(() => queuedRequest.CompletionSource.TrySetCanceled());

        return await queuedRequest.CompletionSource.Task;
    }

    public Task<ExecutionRequestStatus> GetStatusAsync(Guid requestId, CancellationToken ct = default)
    {
        if (!_requests.TryGetValue(requestId, out var queuedRequest))
        {
            throw new InvalidOperationException($"Request {requestId} not found");
        }

        var status = new ExecutionRequestStatus
        {
            RequestId = requestId,
            Status = queuedRequest.Status,
            QueuePosition = CalculateQueuePosition(queuedRequest),
            Result = queuedRequest.CompletionSource.Task.IsCompleted
                ? queuedRequest.CompletionSource.Task.Result
                : null
        };

        return Task.FromResult(status);
    }

    public Task<QueueStatus> GetQueueStatusAsync()
    {
        var status = new QueueStatus
        {
            ImmediateCount = _immediateChannel.Reader.Count,
            NormalCount = _normalChannel.Reader.Count,
            BackgroundCount = _backgroundChannel.Reader.Count,
            CurrentModelId = _currentModelId,
            LastModelSwitch = _lastModelSwitch
        };

        return Task.FromResult(status);
    }

    public Task<QueueMetrics> GetMetricsAsync()
    {
        var dequeued = Interlocked.Read(ref _totalDequeued);
        var measuredExecutionCount = Interlocked.Read(ref _measuredExecutionCount);

        var metrics = new QueueMetrics
        {
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalDequeued = dequeued,
            TotalCompleted = Interlocked.Read(ref _totalCompleted),
            TotalFailed = Interlocked.Read(ref _totalFailed),
            TotalPreempted = Interlocked.Read(ref _totalPreempted),
            TotalLocalExecutions = Interlocked.Read(ref _totalLocalExecutions),
            TotalOnlineExecutions = Interlocked.Read(ref _totalOnlineExecutions),
            TotalModelSwitches = Interlocked.Read(ref _totalModelSwitches),
            InFlightExecutions = Interlocked.Read(ref _inFlightExecutions),
            AverageQueueWaitMs = dequeued > 0
                ? Interlocked.Read(ref _totalQueueWaitMs) / (double)dequeued
                : 0,
            AverageExecutionDurationMs = measuredExecutionCount > 0
                ? Interlocked.Read(ref _totalExecutionDurationMs) / (double)measuredExecutionCount
                : 0,
            LastDequeuedAt = Interlocked.Read(ref _lastDequeuedAtUnixTimeMs) > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Read(ref _lastDequeuedAtUnixTimeMs))
                : null,
            QueueStatus = new QueueStatus
            {
                ImmediateCount = _immediateChannel.Reader.Count,
                NormalCount = _normalChannel.Reader.Count,
                BackgroundCount = _backgroundChannel.Reader.Count,
                CurrentModelId = _currentModelId,
                LastModelSwitch = _lastModelSwitch
            }
        };

        return Task.FromResult(metrics);
    }

    private async Task ProcessQueueAsync()
    {
        _logger.LogInformation("Model queue processing started");

        while (true)
        {
            try
            {
                // Select next request based on priority and model affinity
                var nextRequest = await SelectNextRequestAsync();

                if (nextRequest != null)
                {
                    await ExecuteRequestAsync(nextRequest);
                }
                else
                {
                    // No requests available, wait a bit
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processing loop");
                await Task.Delay(1000); // Back off on error
            }
        }
    }

    private async Task<QueuedRequest?> SelectNextRequestAsync()
    {
        if (_currentModelId == null)
        {
            _currentModelId = await _modelLifecycleManager.GetLoadedModelAsync();
        }

        // P0 (Immediate): Always process first, regardless of model
        if (_immediateChannel.Reader.TryRead(out var immediateRequest))
        {
            MarkRequestDequeued(immediateRequest);
            _logger.LogDebug("Selected P0 (Immediate) request {RequestId}", immediateRequest.Request.Id);
            return immediateRequest;
        }

        // P1 (Normal): Implement model affinity batching (MQ-REQ-004)
        // If current model is loaded and P1 requests exist, scan for matching requests
        if (_currentModelId != null && _normalChannel.Reader.Count > 0)
        {
            var scannedRequests = new List<QueuedRequest>();
            const int maxLookahead = 10; // Limit scanning to prevent delays

            // Scan up to 10 P1 requests for current model match
            for (int i = 0; i < maxLookahead && _normalChannel.Reader.TryRead(out var req); i++)
            {
                var reqModelId = DetermineModelId(req.Request);

                if (reqModelId == _currentModelId)
                {
                    // Found match! Requeue others and return this one
                    _logger.LogDebug(
                        "Found P1 request {RequestId} for current model {ModelId} after scanning {Count} requests (batching)",
                        req.Request.Id, _currentModelId, i + 1);

                    foreach (var other in scannedRequests)
                    {
                        await _normalChannel.Writer.WriteAsync(other);
                    }

                    MarkRequestDequeued(req);
                    return req;
                }

                scannedRequests.Add(req);
            }

            // No match found in lookahead. Drain remaining P1 queue and choose the model
            // with the most pending P1 work (MQ-REQ-006).
            if (scannedRequests.Count > 0)
            {
                if (_options.DominantP1SelectionWindowMs > 0)
                {
                    await Task.Delay(_options.DominantP1SelectionWindowMs);
                }

                while (_normalChannel.Reader.TryRead(out var remainingRequest))
                {
                    scannedRequests.Add(remainingRequest);
                }

                var selectedRequest = await SelectDominantP1RequestAsync(scannedRequests);

                var selectedModelId = DetermineModelId(selectedRequest.Request);
                _logger.LogInformation(
                    "No P1 requests for current model {CurrentModelId}; selected dominant P1 model {SelectedModelId}",
                    _currentModelId, selectedModelId);

                MarkRequestDequeued(selectedRequest);
                return selectedRequest;
            }
        }

        // Fallback: Simple FIFO if no current model or empty queue
        if (_normalChannel.Reader.TryRead(out var normalRequest))
        {
            MarkRequestDequeued(normalRequest);
            var modelId = DetermineModelId(normalRequest.Request);
            _logger.LogDebug(
                "Selected P1 (Normal) request {RequestId} for model {ModelId}",
                normalRequest.Request.Id, modelId);
            return normalRequest;
        }

        // P2 (Background): Implement model affinity batching (MQ-REQ-005)
        // If current model is loaded and P2 requests exist, scan for matching requests
        if (_currentModelId != null && _backgroundChannel.Reader.Count > 0)
        {
            var scannedRequests = new List<QueuedRequest>();
            const int maxLookahead = 10; // Limit scanning to prevent delays

            // Scan up to 10 P2 requests for current model match
            for (int i = 0; i < maxLookahead && _backgroundChannel.Reader.TryRead(out var req); i++)
            {
                var reqModelId = DetermineModelId(req.Request);

                if (reqModelId == _currentModelId)
                {
                    // Found match! Requeue others and return this one
                    _logger.LogDebug(
                        "Found P2 request {RequestId} for current model {ModelId} after scanning {Count} requests (batching)",
                        req.Request.Id, _currentModelId, i + 1);

                    foreach (var other in scannedRequests)
                    {
                        await _backgroundChannel.Writer.WriteAsync(other);
                    }

                    MarkRequestDequeued(req);
                    return req;
                }

                scannedRequests.Add(req);
            }

            // No match found in lookahead. Drain remaining P2 queue and choose the model
            // with the most pending P2 work to minimize model switches under steady workloads.
            if (scannedRequests.Count > 0)
            {
                if (_options.DominantP2SelectionWindowMs > 0)
                {
                    await Task.Delay(_options.DominantP2SelectionWindowMs);
                }

                while (_backgroundChannel.Reader.TryRead(out var remainingRequest))
                {
                    scannedRequests.Add(remainingRequest);
                }

                var selectedRequest = await SelectDominantP2RequestAsync(scannedRequests);

                var selectedModelId = DetermineModelId(selectedRequest.Request);
                _logger.LogInformation(
                    "No P2 requests for current model {CurrentModelId}; selected dominant P2 model {SelectedModelId}",
                    _currentModelId, selectedModelId);

                MarkRequestDequeued(selectedRequest);
                return selectedRequest;
            }
        }

        // Fallback: Simple FIFO if no current model or empty queue
        if (_backgroundChannel.Reader.TryRead(out var backgroundRequest))
        {
            MarkRequestDequeued(backgroundRequest);
            var modelId = DetermineModelId(backgroundRequest.Request);
            _logger.LogDebug(
                "Selected P2 (Background) request {RequestId} for model {ModelId}",
                backgroundRequest.Request.Id, modelId);
            return backgroundRequest;
        }

        return null;
    }

    private async Task ExecuteRequestAsync(QueuedRequest queuedRequest)
    {
        await _executionLock.WaitAsync();
        CancellationTokenSource? executionCts = null;
        var executionStopwatch = Stopwatch.StartNew();
        Interlocked.Increment(ref _inFlightExecutions);

        try
        {
            // Set up cancellation for preemption
            executionCts = new CancellationTokenSource();
            lock (_executionStateLock)
            {
                _currentlyExecuting = queuedRequest;
                _currentExecutionCts = executionCts;
            }

            queuedRequest.Status = ExecutionStatus.Processing;
            var request = queuedRequest.Request;

            _logger.LogInformation(
                "Executing request {RequestId}, task type: {TaskType}, priority: {Priority}",
                request.Id, request.TaskType, queuedRequest.Priority);

            ExecutionResult result;

            // Determine if this is a local or online request
            var isOnlineRequest = ShouldRouteOnline(request);

            if (isOnlineRequest)
            {
                Interlocked.Increment(ref _totalOnlineExecutions);
                // Route to online provider
                result = await _onlineRouter.ExecuteAsync(request, null, executionCts.Token);
            }
            else
            {
                Interlocked.Increment(ref _totalLocalExecutions);
                // Route to Foundry Local
                var modelId = DetermineModelId(request);

                // MQ-REQ-007: Explicitly switch model by unloading current and loading target
                // before local execution.
                var currentModel = await _modelLifecycleManager.GetLoadedModelAsync();
                if (currentModel != modelId)
                {
                    _logger.LogInformation(
                        "Model switch: {OldModel} → {NewModel}",
                        currentModel ?? "(none)", modelId);

                    await _modelLifecycleManager.SwitchModelAsync(modelId, executionCts.Token);
                    _lastModelSwitch = (await _modelLifecycleManager.GetLastModelSwitchAsync()) ?? DateTimeOffset.UtcNow;
                    Interlocked.Increment(ref _totalModelSwitches);
                }

                // Always update current model ID (even if no switch) for batching logic
                _currentModelId = modelId;

                result = await _foundryBridge.ExecuteAsync(request, modelId, executionCts.Token);
            }

            result.Status = ExecutionStatus.Completed;
            queuedRequest.Status = ExecutionStatus.Completed;
            queuedRequest.CompletionSource.TrySetResult(result);
            Interlocked.Increment(ref _totalCompleted);

            _logger.LogInformation(
                "Completed request {RequestId}: {TokenCount} tokens",
                request.Id, result.TokenUsage.TotalTokens);
        }
        catch (OperationCanceledException) when (executionCts?.IsCancellationRequested == true)
        {
            // Request was preempted by P0 - requeue it
            _logger.LogInformation(
                "Request {RequestId} (priority {Priority}) was preempted, requeuing",
                queuedRequest.Request.Id, queuedRequest.Priority);
            Interlocked.Increment(ref _totalPreempted);

            queuedRequest.Status = ExecutionStatus.Queued;

            // Requeue to appropriate channel based on priority
            var channel = queuedRequest.Priority switch
            {
                ExecutionPriority.Immediate => _immediateChannel,
                ExecutionPriority.Normal => _normalChannel,
                ExecutionPriority.Background => _backgroundChannel,
                _ => _normalChannel
            };

            await channel.Writer.WriteAsync(queuedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute request {RequestId}", queuedRequest.Request.Id);

            var errorResult = new ExecutionResult
            {
                RequestId = queuedRequest.Request.Id,
                Status = ExecutionStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow
            };

            queuedRequest.Status = ExecutionStatus.Failed;
            queuedRequest.CompletionSource.TrySetResult(errorResult);
            Interlocked.Increment(ref _totalFailed);
        }
        finally
        {
            executionStopwatch.Stop();
            Interlocked.Add(ref _totalExecutionDurationMs, executionStopwatch.ElapsedMilliseconds);
            Interlocked.Increment(ref _measuredExecutionCount);
            Interlocked.Decrement(ref _inFlightExecutions);

            lock (_executionStateLock)
            {
                _currentlyExecuting = null;
                _currentExecutionCts = null;
            }

            executionCts?.Dispose();
            _executionLock.Release();
        }
    }

    private void MarkRequestDequeued(QueuedRequest queuedRequest)
    {
        Interlocked.Increment(ref _totalDequeued);

        var queueWait = DateTimeOffset.UtcNow - queuedRequest.EnqueuedAt;
        if (queueWait > TimeSpan.Zero)
        {
            Interlocked.Add(ref _totalQueueWaitMs, (long)queueWait.TotalMilliseconds);
        }

        Interlocked.Exchange(ref _lastDequeuedAtUnixTimeMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private bool ShouldRouteOnline(ExecutionRequest request)
    {
        // Simple heuristic: check if task type suggests online model
        // In real implementation, would use configuration
        return request.TaskType.Contains("online", StringComparison.OrdinalIgnoreCase);
    }

    private string DetermineModelId(ExecutionRequest request)
    {
        // Simple model selection based on task type
        // In real implementation, would use configuration mapping
        return request.TaskType.ToLowerInvariant() switch
        {
            "code" => _options.CodeModelId,
            "chat" => _options.ChatModelId,
            "summarize" => _options.SummarizeModelId,
            _ => _options.DefaultModelId
        };
    }

    private int CalculateQueuePosition(QueuedRequest queuedRequest)
    {
        // Approximate position in queue
        var position = queuedRequest.Priority switch
        {
            ExecutionPriority.Immediate => _immediateChannel.Reader.Count,
            ExecutionPriority.Normal => _immediateChannel.Reader.Count + _normalChannel.Reader.Count,
            ExecutionPriority.Background => _immediateChannel.Reader.Count + _normalChannel.Reader.Count + _backgroundChannel.Reader.Count,
            _ => int.MaxValue
        };

        return Math.Max(0, position);
    }

    private async Task<QueuedRequest> SelectDominantP1RequestAsync(List<QueuedRequest> candidates)
    {
        return await SelectDominantRequestAsync(candidates, _normalChannel.Writer, "P1");
    }

    private async Task<QueuedRequest> SelectDominantP2RequestAsync(List<QueuedRequest> candidates)
    {
        return await SelectDominantRequestAsync(candidates, _backgroundChannel.Writer, "P2");
    }

    private async Task<QueuedRequest> SelectDominantRequestAsync(
        List<QueuedRequest> candidates,
        ChannelWriter<QueuedRequest> writer,
        string priorityLabel)
    {
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"Cannot select dominant {priorityLabel} request from an empty candidate set.");
        }

        var modelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstSeenIndexByModel = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < candidates.Count; index++)
        {
            var modelId = DetermineModelId(candidates[index].Request);
            modelCounts[modelId] = modelCounts.TryGetValue(modelId, out var count) ? count + 1 : 1;

            if (!firstSeenIndexByModel.ContainsKey(modelId))
            {
                firstSeenIndexByModel[modelId] = index;
            }
        }

        var dominantModelId = modelCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => firstSeenIndexByModel[pair.Key])
            .First()
            .Key;

        var selectedRequest = candidates.First(req => DetermineModelId(req.Request) == dominantModelId);

        var dominantModelRemainder = candidates
            .Where(req => req != selectedRequest && DetermineModelId(req.Request) == dominantModelId)
            .ToList();

        var otherRequests = candidates
            .Where(req => DetermineModelId(req.Request) != dominantModelId)
            .ToList();

        foreach (var req in dominantModelRemainder)
        {
            await writer.WriteAsync(req);
        }

        foreach (var req in otherRequests)
        {
            await writer.WriteAsync(req);
        }

        return selectedRequest;
    }

    private class QueuedRequest
    {
        public ExecutionRequest Request { get; init; } = null!;
        public ExecutionPriority Priority { get; init; }
        public DateTimeOffset EnqueuedAt { get; init; }
        public long SequenceNumber { get; init; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
        public TaskCompletionSource<ExecutionResult> CompletionSource { get; init; } = null!;
    }
}
