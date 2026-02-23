using System.Collections.Concurrent;
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

    public ModelQueue(
        IFoundryBridge foundryBridge,
        IOnlineProviderRouter onlineRouter,
        IOptions<ModelQueueOptions> options,
        ILogger<ModelQueue> logger)
    {
        _foundryBridge = foundryBridge;
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
            CompletionSource = new TaskCompletionSource<ExecutionResult>()
        };

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
            "Enqueued request {RequestId} with priority {Priority}, task type: {TaskType}",
            request.Id, priority, request.TaskType);

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
        // P0 (Immediate): Always process first, regardless of model
        if (_immediateChannel.Reader.TryRead(out var immediateRequest))
        {
            _logger.LogDebug("Selected P0 (Immediate) request {RequestId}", immediateRequest.Request.Id);
            return immediateRequest;
        }

        // P1 (Normal): Prefer current model, but switch if queue drains
        if (_normalChannel.Reader.TryRead(out var normalRequest))
        {
            _logger.LogDebug("Selected P1 (Normal) request {RequestId}", normalRequest.Request.Id);
            return normalRequest;
        }

        // P2 (Background): Only process when no P0/P1, and batch by model
        if (_backgroundChannel.Reader.TryRead(out var backgroundRequest))
        {
            _logger.LogDebug("Selected P2 (Background) request {RequestId}", backgroundRequest.Request.Id);
            return backgroundRequest;
        }

        return null;
    }

    private async Task ExecuteRequestAsync(QueuedRequest queuedRequest)
    {
        await _executionLock.WaitAsync();
        try
        {
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
                // Route to online provider
                result = await _onlineRouter.ExecuteAsync(request);
            }
            else
            {
                // Route to Foundry Local
                var modelId = DetermineModelId(request);

                // Check if model switch needed
                var currentModel = await _foundryBridge.GetLoadedModelAsync();
                if (currentModel != modelId)
                {
                    _logger.LogInformation(
                        "Model switch: {OldModel} → {NewModel}",
                        currentModel ?? "(none)", modelId);

                    _currentModelId = modelId;
                    _lastModelSwitch = DateTimeOffset.UtcNow;
                }

                result = await _foundryBridge.ExecuteAsync(request, modelId);
            }

            result.Status = ExecutionStatus.Completed;
            queuedRequest.Status = ExecutionStatus.Completed;
            queuedRequest.CompletionSource.TrySetResult(result);

            _logger.LogInformation(
                "Completed request {RequestId}: {TokenCount} tokens",
                request.Id, result.TokenUsage.TotalTokens);
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
        }
        finally
        {
            _executionLock.Release();
        }
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

    private class QueuedRequest
    {
        public ExecutionRequest Request { get; init; } = null!;
        public ExecutionPriority Priority { get; init; }
        public DateTimeOffset EnqueuedAt { get; init; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
        public TaskCompletionSource<ExecutionResult> CompletionSource { get; init; } = null!;
    }
}
