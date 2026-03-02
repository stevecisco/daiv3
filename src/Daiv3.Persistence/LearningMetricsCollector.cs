using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;

namespace Daiv3.Persistence;

/// <summary>
/// Collects and aggregates learning metrics using observer pattern.
/// Provides pub/sub for learning system events with audit trail and performance metrics.
/// Implements LM-NFR-002: Learnings SHOULD be transparent and auditable.
/// </summary>
public class LearningMetricsCollector : ILearningObserver
{
    private readonly LearningRepository _repository;
    private readonly ILogger<LearningMetricsCollector> _logger;
    private readonly ConcurrentBag<ILearningObserver> _observers = new();
    private readonly LearningObservabilityOptions _options;

    // Metrics tracking
    private long _totalLearningsCreated;
    private long _totalRetrievalOperations;
    private long _totalInjections;
    private long _totalTokensInjected;
    private int _promotionOperations;
    private int _suppressionOperations;
    private int _supersessionOperations;

    // Event audit trail (Ring buffer to prevent unbounded memory growth)
    private readonly Ring<LearningEvent> _auditTrail;

    public LearningMetricsCollector(
        LearningRepository repository,
        ILogger<LearningMetricsCollector> logger,
        LearningObservabilityOptions? options = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new LearningObservabilityOptions();
        _auditTrail = new Ring<LearningEvent>(_options.MaxAuditTrailSize);

        _logger.LogInformation(
            "LearningMetricsCollector initialized with max audit trail size: {MaxSize}, " +
            "telemetry enabled: {TelemetryEnabled}, audit enabled: {AuditEnabled}",
            _options.MaxAuditTrailSize,
            _options.EnableTelemetry,
            _options.EnableAuditTrail);
    }

    /// <summary>
    /// Registers an observer for learning events.
    /// </summary>
    public void RegisterObserver(ILearningObserver observer)
    {
        if (observer != null)
        {
            _observers.Add(observer);
            _logger.LogDebug("Registered learning observer: {ObserverType}", observer.GetType().Name);
        }
    }

    /// <summary>
    /// Gets current metrics snapshot.
    /// </summary>
    public async Task<LearningMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        try
        {
            var allLearnings = await _repository.GetAllAsync(ct).ConfigureAwait(false);
            var activeLearnings = allLearnings.Where(l => l.Status == "Active").ToList();

            var triggerCounts = allLearnings
                .GroupBy(l => l.TriggerType)
                .ToDictionary(g => g.Key, g => g.Count());

            var scopeCounts = allLearnings
                .GroupBy(l => l.Scope)
                .ToDictionary(g => g.Key, g => g.Count());

            var injectableCount = activeLearnings.Count(l =>
                l.Scope == "Global" || l.Status == "Active");

            var averageTokens = _totalInjections > 0
                ? _totalTokensInjected / (double)_totalInjections
                : 0.0;

            var metrics = new LearningMetrics
            {
                TotalLearningsCreated = (int)_totalLearningsCreated,
                ActiveLearningsCount = activeLearnings.Count,
                SuppressedLearningsCount = allLearnings.Count(l => l.Status == "Suppressed"),
                SupersededLearningsCount = allLearnings.Count(l => l.Status == "Superseded"),
                ArchivedLearningsCount = allLearnings.Count(l => l.Status == "Archived"),
                CreationsByTriggerType = triggerCounts,
                DistributionByScope = scopeCounts,
                TotalLearningsApplied = allLearnings.Sum(l => l.TimesApplied),
                AverageConfidenceScore = activeLearnings.Count > 0
                    ? activeLearnings.Average(l => l.Confidence)
                    : 0.0,
                TotalRetrievalOperations = _totalRetrievalOperations,
                TotalInjections = _totalInjections,
                AverageTokensPerInjection = averageTokens,
                PromotionOperations = _promotionOperations,
                SuppressionOperations = _suppressionOperations,
                SupersessionOperations = _supersessionOperations,
                InjectableLearningsCount = injectableCount
            };

            if (_options.EnableTelemetry)
            {
                await NotifyObserversAsync(o => o.OnMetricsCapturedAsync(metrics)).ConfigureAwait(false);
            }

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing learning metrics");
            await NotifyObserversAsync(o => o.OnLearningOperationErrorAsync(
                "GetMetrics", ex.Message, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds())).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets audit trail events (most recent first).
    /// </summary>
    public IReadOnlyList<LearningEvent> GetAuditTrail()
    {
        var snapshot = _auditTrail.GetSnapshot();
        return snapshot.AsEnumerable().Reverse().ToList();
    }

    // ILearningObserver implementation
    public async Task OnLearningCreatedAsync(
        string learningId,
        string title,
        string triggerType,
        string scope,
        double confidence,
        string? sourceAgent,
        long createdAt)
    {
        Interlocked.Increment(ref _totalLearningsCreated);

        var @event = new LearningEvent
        {
            EventType = "Created",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["title"] = title,
                ["triggerType"] = triggerType,
                ["scope"] = scope,
                ["confidence"] = confidence,
                ["sourceAgent"] = sourceAgent
            },
            Timestamp = createdAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogInformation(
                "Learning created: {LearningId} ({Title}), trigger={Trigger}, scope={Scope}, confidence={Confidence}",
                learningId, title, triggerType, scope, confidence);

            await NotifyObserversAsync(o => o.OnLearningCreatedAsync(
                learningId, title, triggerType, scope, confidence, sourceAgent, createdAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningsRetrievedAsync(
        string retrievalId,
        int count,
        string? queryText,
        double? similarityThreshold,
        long durationMs)
    {
        Interlocked.Increment(ref _totalRetrievalOperations);

        var @event = new LearningEvent
        {
            EventType = "Retrieved",
            LearningId = retrievalId,
            Metadata = new Dictionary<string, object?>
            {
                ["count"] = count,
                ["query"] = queryText,
                ["threshold"] = similarityThreshold,
                ["durationMs"] = durationMs
            },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogDebug(
                "Retrieved {Count} learnings (query={Query}, duration={Duration}ms)",
                count, queryText ?? "(similarity-based)", durationMs);

            await NotifyObserversAsync(o => o.OnLearningsRetrievedAsync(
                retrievalId, count, queryText, similarityThreshold, durationMs)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningsInjectedAsync(
        IReadOnlyList<string> learningIds,
        string agentId,
        int totalTokens,
        long injectedAt)
    {
        Interlocked.Increment(ref _totalInjections);
        Interlocked.Add(ref _totalTokensInjected, totalTokens);

        var @event = new LearningEvent
        {
            EventType = "Injected",
            LearningId = string.Join(",", learningIds),
            Metadata = new Dictionary<string, object?>
            {
                ["count"] = learningIds.Count,
                ["agentId"] = agentId,
                ["tokens"] = totalTokens
            },
            Timestamp = injectedAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogDebug(
                "Injected {Count} learnings into agent {AgentId} ({Tokens} tokens)",
                learningIds.Count, agentId, totalTokens);

            await NotifyObserversAsync(o => o.OnLearningsInjectedAsync(
                learningIds, agentId, totalTokens, injectedAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningStatusChangedAsync(
        string learningId,
        string previousStatus,
        string newStatus,
        string modificationReason,
        long modifiedAt)
    {
        var @event = new LearningEvent
        {
            EventType = "StatusChanged",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["from"] = previousStatus,
                ["to"] = newStatus,
                ["reason"] = modificationReason
            },
            Timestamp = modifiedAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogInformation(
                "Learning {LearningId} status changed: {From} → {To} ({Reason})",
                learningId, previousStatus, newStatus, modificationReason);

            await NotifyObserversAsync(o => o.OnLearningStatusChangedAsync(
                learningId, previousStatus, newStatus, modificationReason, modifiedAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningPromotedAsync(
        string learningId,
        string previousScope,
        string newScope,
        long promotedAt)
    {
        Interlocked.Increment(ref _promotionOperations);

        var @event = new LearningEvent
        {
            EventType = "Promoted",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["from"] = previousScope,
                ["to"] = newScope
            },
            Timestamp = promotedAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogInformation(
                "Learning {LearningId} promoted: {From} → {To}",
                learningId, previousScope, newScope);

            await NotifyObserversAsync(o => o.OnLearningPromotedAsync(
                learningId, previousScope, newScope, promotedAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningSuppressionAsync(
        string learningId,
        string? suppressionReason,
        long suppressedAt)
    {
        Interlocked.Increment(ref _suppressionOperations);

        var @event = new LearningEvent
        {
            EventType = "Suppressed",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["reason"] = suppressionReason
            },
            Timestamp = suppressedAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogInformation(
                "Learning {LearningId} suppressed {Reason}",
                learningId, suppressionReason != null ? $"({suppressionReason})" : "");

            await NotifyObserversAsync(o => o.OnLearningSuppressionAsync(
                learningId, suppressionReason, suppressedAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningSupersededAsync(
        string learningId,
        string? supersedingLearningId,
        long supersededAt)
    {
        Interlocked.Increment(ref _supersessionOperations);

        var @event = new LearningEvent
        {
            EventType = "Superseded",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["supersededBy"] = supersedingLearningId
            },
            Timestamp = supersededAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogInformation(
                "Learning {LearningId} superseded {By}",
                learningId, supersedingLearningId != null ? $"by {supersedingLearningId}" : "");

            await NotifyObserversAsync(o => o.OnLearningSupersededAsync(
                learningId, supersedingLearningId, supersededAt)).ConfigureAwait(false);
        }
    }

    public async Task OnLearningAppliedAsync(
        string learningId,
        string appliedBy,
        string applicationType,
        long appliedAt)
    {
        var @event = new LearningEvent
        {
            EventType = "Applied",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["appliedBy"] = appliedBy,
                ["type"] = applicationType
            },
            Timestamp = appliedAt
        };

        RecordAuditEvent(@event);

        if (_options.EnableTelemetry)
        {
            _logger.LogDebug(
                "Learning {LearningId} applied by {AppliedBy} ({Type})",
                learningId, appliedBy, applicationType);

            await NotifyObserversAsync(o => o.OnLearningAppliedAsync(
                learningId, appliedBy, applicationType, appliedAt)).ConfigureAwait(false);
        }
    }

    public async Task OnMetricsCapturedAsync(LearningMetrics metrics)
    {
        await NotifyObserversAsync(o => o.OnMetricsCapturedAsync(metrics)).ConfigureAwait(false);
    }

    public async Task OnLearningOperationErrorAsync(
        string operationType,
        string errorMessage,
        string? learningId,
        long errorTime)
    {
        var @event = new LearningEvent
        {
            EventType = "Error",
            LearningId = learningId,
            Metadata = new Dictionary<string, object?>
            {
                ["operation"] = operationType,
                ["error"] = errorMessage
            },
            Timestamp = errorTime
        };

        RecordAuditEvent(@event);

        _logger.LogError(
            "Learning operation error - {Operation}: {Error} {LearningId}",
            operationType, errorMessage, learningId ?? "(no learning id)");

        await NotifyObserversAsync(o => o.OnLearningOperationErrorAsync(
            operationType, errorMessage, learningId, errorTime)).ConfigureAwait(false);
    }

    // Private helpers
    private void RecordAuditEvent(LearningEvent @event)
    {
        if (_options.EnableAuditTrail)
        {
            _auditTrail.Add(@event);
        }
    }

    private async Task NotifyObserversAsync(Func<ILearningObserver, Task> action)
    {
        var tasks = _observers
            .AsEnumerable()
            .Where(o => o != null)
            .Select(async o =>
            {
                try
                {
                    await action(o).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error notifying observer {ObserverType}", o.GetType().Name);
                    // Continue notifying other observers even if one fails
                }
            })
            .ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}

/// <summary>
/// A learning event recorded in the audit trail.
/// </summary>
public record LearningEvent
{
    /// <summary>
    /// Type of event: Created, Retrieved, Injected, StatusChanged, Promoted, Suppressed, Superseded, Applied, Error.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// The learning ID involved (or comma-separated list for batch operations).
    /// </summary>
    public string? LearningId { get; init; }

    /// <summary>
    /// Event-specific metadata as key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Unix timestamp of when the event occurred.
    /// </summary>
    public long Timestamp { get; init; }
}

/// <summary>
/// Configuration options for learning observability and auditing.
/// </summary>
public class LearningObservabilityOptions
{
    /// <summary>
    /// Enable collection of learning telemetry metrics.
    /// Default: true
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Enable audit trail recording for all learning events.
    /// Default: true
    /// </summary>
    public bool EnableAuditTrail { get; set; } = true;

    /// <summary>
    /// Maximum number of audit trail events to keep in memory.
    /// Oldest events are discarded when limit is reached.
    /// Default: 10,000
    /// </summary>
    public int MaxAuditTrailSize { get; set; } = 10_000;

    /// <summary>
    /// Enable detailed logging of learning operations.
    /// Default: false (only errors/info logged by default)
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>
/// Ring buffer for bounded circular storage of events.
/// </summary>
internal class Ring<T>
{
    private readonly T[] _buffer;
    private int _index;
    private bool _full;
    private readonly object _lock = new();

    public Ring(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException($"Capacity must be > 0", nameof(capacity));

        _buffer = new T[capacity];
        _index = 0;
        _full = false;
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_index] = item;
            _index = (_index + 1) % _buffer.Length;
            if (_index == 0)
                _full = true;
        }
    }

    public List<T> GetSnapshot()
    {
        lock (_lock)
        {
            if (!_full)
            {
                return _buffer.Take(_index).ToList();
            }

            var result = new List<T>(_buffer.Length);
            result.AddRange(_buffer.Skip(_index));
            result.AddRange(_buffer.Take(_index));
            return result;
        }
    }
}
