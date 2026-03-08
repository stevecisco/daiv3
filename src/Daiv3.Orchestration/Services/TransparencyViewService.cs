namespace Daiv3.Orchestration.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.Knowledge;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;

/// <summary>
/// Implementation of <see cref="ITransparencyViewService"/>.
/// Aggregates system transparency data across model usage, indexing, queue, and agent activity.
/// Implements ES-REQ-004: The system SHALL expose a transparency view that shows model usage,
/// indexing status, queue state, and agent activity.
/// </summary>
public sealed class TransparencyViewService : ITransparencyViewService
{
    private readonly ILogger<TransparencyViewService> _logger;
    private readonly IModelQueue? _modelQueue;
    private readonly IIndexingStatusService? _indexingStatusService;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IDatabaseContext? _databaseContext;

    public TransparencyViewService(
        ILogger<TransparencyViewService> logger,
        IModelQueue? modelQueue = null,
        IIndexingStatusService? indexingStatusService = null,
        IServiceScopeFactory? scopeFactory = null,
        IDatabaseContext? databaseContext = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelQueue = modelQueue;
        _indexingStatusService = indexingStatusService;
        _scopeFactory = scopeFactory;
        _databaseContext = databaseContext;

        _logger.LogInformation(
            "TransparencyViewService initialized with: ModelQueue={HasQueue}, IndexingStatus={HasIndexing}, AgentManager={HasAgent}, Database={HasDb}",
            _modelQueue != null,
            _indexingStatusService != null,
            _scopeFactory != null,
            _databaseContext != null);
    }

    /// <inheritdoc />
    public async Task<TransparencyViewData> GetTransparencyViewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new TransparencyViewData
            {
                CollectedAt = DateTimeOffset.UtcNow
            };

            // Collect all data sources in parallel to minimize latency
            var modelUsageTask = GetModelUsageAsync(cancellationToken);
            var indexingTask = GetIndexingStatusAsync(cancellationToken);
            var queueStateTask = GetQueueStateAsync(cancellationToken);
            var agentActivityTask = GetAgentActivityAsync(cancellationToken);

            await Task.WhenAll(modelUsageTask, indexingTask, queueStateTask, agentActivityTask)
                .ConfigureAwait(false);

            data.ModelUsage = await modelUsageTask;
            data.IndexingStatus = await indexingTask;
            data.QueueState = await queueStateTask;
            data.AgentActivity = await agentActivityTask;

            _logger.LogDebug(
                "Transparency view collected: Models={Models}, Queue={PendingCount}, Indexing={FilesIndexed}, Agents={ActiveAgents}",
                data.ModelUsage.CurrentModel ?? "none",
                data.QueueState.PendingCount,
                data.IndexingStatus.FilesIndexed,
                data.AgentActivity.ActiveAgentCount);

            return data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting transparency view data");
            return new TransparencyViewData
            {
                CollectedAt = DateTimeOffset.UtcNow,
                CollectionError = $"Error collecting transparency view: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<ModelUsageStatus> GetModelUsageAsync(CancellationToken cancellationToken = default)
    {
        if (_modelQueue == null)
        {
            return new ModelUsageStatus();
        }

        try
        {
            var queueStatus = await _modelQueue.GetQueueStatusAsync().ConfigureAwait(false);
            var metrics = await _modelQueue.GetMetricsAsync().ConfigureAwait(false);

            var status = new ModelUsageStatus
            {
                CurrentModel = queueStatus?.CurrentModelId,
                TotalExecutions = metrics?.TotalCompleted ?? 0,
                AverageExecutionMs = metrics?.AverageExecutionDurationMs ?? 0,
                ModelSwitchCount = 0, // Placeholder - model switch count not exposed in QueueStatus
                LastModelSwitchAt = queueStatus?.LastModelSwitch,
            };

            // Calculate active model load duration
            if (queueStatus?.LastModelSwitch != null && queueStatus.LastModelSwitch != default)
            {
                status.ActiveModelLoadDurationMs = (long)(DateTimeOffset.UtcNow - queueStatus.LastModelSwitch).TotalMilliseconds;
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting model usage status");
            return new ModelUsageStatus();
        }
    }

    /// <inheritdoc />
    public async Task<IndexingStatusExtended> GetIndexingStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_indexingStatusService == null)
        {
            return new IndexingStatusExtended();
        }

        try
        {
            // Get indexing statistics from the service
            var stats = await _indexingStatusService.GetIndexingStatisticsAsync(cancellationToken)
                .ConfigureAwait(false);

            // Get knowledge base statistics (document and chunk counts)
            int totalDocuments = 0;
            int totalChunks = 0;

            if (_databaseContext != null)
            {
                try
                {
                    using var connection = await _databaseContext.GetConnectionAsync(cancellationToken)
                        .ConfigureAwait(false);

                    // Get document count
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM documents";
                        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        totalDocuments = result is int count ? count : 0;
                    }

                    // Get chunk count
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM chunk_index";
                        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        totalChunks = result is int count ? count : 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error querying knowledge base statistics");
                }
            }

            var estimatedStorage = stats.TotalEmbeddingStorageBytes;

            var status = new IndexingStatusExtended
            {
                IsIndexing = stats.IsWatcherActive && stats.TotalInProgress > 0,
                FilesQueued = stats.TotalNotIndexed,
                FilesIndexed = stats.TotalIndexed,
                FilesInProgress = stats.TotalInProgress,
                FilesWithErrors = stats.TotalErrors,
                ProgressPercentage = stats.TotalDiscovered > 0
                    ? (stats.TotalIndexed * 100.0) / stats.TotalDiscovered
                    : 0.0,
                ErrorDetailsFormatted = FormatErrorDetails(stats),
                LastScanTime = stats.LastScanTime.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(stats.LastScanTime.Value)
                    : null,
                TotalDocumentsStored = totalDocuments,
                TotalChunksStored = totalChunks,
                EstimatedStorageBytesUsed = estimatedStorage
            };

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting indexing status");
            return new IndexingStatusExtended();
        }
    }

    /// <inheritdoc />
    public async Task<QueueStateExtended> GetQueueStateAsync(CancellationToken cancellationToken = default)
    {
        if (_modelQueue == null)
        {
            return new QueueStateExtended();
        }

        try
        {
            var queueStatus = await _modelQueue.GetQueueStatusAsync().ConfigureAwait(false);
            var metrics = await _modelQueue.GetMetricsAsync().ConfigureAwait(false);

            var pendingCount = (queueStatus?.ImmediateCount ?? 0)
                + (queueStatus?.NormalCount ?? 0)
                + (queueStatus?.BackgroundCount ?? 0);

            var state = new QueueStateExtended
            {
                PendingCount = pendingCount,
                CompletedCount = (int)(metrics?.TotalCompleted ?? 0),
                ImmediateCount = queueStatus?.ImmediateCount ?? 0,
                NormalCount = queueStatus?.NormalCount ?? 0,
                BackgroundCount = queueStatus?.BackgroundCount ?? 0,
                AverageTaskDurationMs = metrics?.AverageExecutionDurationMs ?? 0,
                EstimatedWaitMs = metrics?.AverageQueueWaitMs ?? 0,
                ModelUtilizationPercent = metrics?.InFlightExecutions > 0
                    ? (int)Math.Min(100, (metrics.InFlightExecutions / 4.0) * 100.0)
                    : 0,
                TopPendingTasks = [] // Populated from additional task details if available
            };

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting queue state");
            return new QueueStateExtended();
        }
    }

    /// <inheritdoc />
    public async Task<AgentActivityExtended> GetAgentActivityAsync(CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            return new AgentActivityExtended();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agentManager = scope.ServiceProvider.GetService<IAgentManager>();

            if (agentManager == null)
            {
                return new AgentActivityExtended();
            }

            var activities = new List<IndividualAgentActivityExtended>();
            var activeExecutions = agentManager.GetActiveExecutions();

            foreach (var executionControl in activeExecutions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Resolve agent details
                Agent? agent = null;
                try
                {
                    agent = await agentManager.GetAgentAsync(executionControl.AgentId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not resolve agent {AgentId}", executionControl.AgentId);
                }

                var state = executionControl.IsStopped ? "Stopped"
                          : executionControl.IsPaused ? "Paused"
                          : "Running";

                var shortId = executionControl.AgentId.ToString("N")[..8];

                activities.Add(new IndividualAgentActivityExtended
                {
                    AgentId = executionControl.AgentId.ToString(),
                    AgentName = agent?.Name ?? $"Agent-{shortId}",
                    CurrentTask = null, // Will be populated when execution carries task goal
                    State = state,
                    IterationCount = 0, // Will be populated from metrics collector
                    TokensUsed = 0, // Will be populated from metrics collector
                    StartedAt = DateTimeOffset.UtcNow,
                    ElapsedTime = TimeSpan.Zero,
                    ErrorCount = 0,
                    LastErrorMessage = null
                });
            }

            return new AgentActivityExtended
            {
                ActiveAgentCount = activities.Count,
                TotalAgentsExecuted = activities.Count,
                TotalIterations = activities.Sum(a => a.IterationCount),
                TotalTokensUsed = activities.Sum(a => a.TokensUsed),
                Activities = [..activities]
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting agent activity");
            return new AgentActivityExtended();
        }
    }

    /// <summary>
    /// Formats error details into human-readable descriptions.
    /// </summary>
    private static string[] FormatErrorDetails(IndexingStatistics stats)
    {
        var errors = new List<string>();

        if (stats.TotalErrors > 0)
        {
            errors.Add($"{stats.TotalErrors} file(s) with indexing errors");
        }

        if (stats.TotalWarnings > 0)
        {
            errors.Add($"{stats.TotalWarnings} file(s) with warnings (partial processing)");
        }

        return [..errors];
    }
}
