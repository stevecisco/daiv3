using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence;

/// <summary>
/// Service for managing learning persistence and retrieval.
/// Provides low-level storage operations for learnings in the SQLite database.
/// Implements LM-REQ-003: Storage of learnings in dedicated SQLite table.
/// Implements LM-NFR-002: Learnings SHOULD be transparent and auditable (via metrics collector).
/// Note: For learning creation with embeddings, use Orchestration.LearningService (LM-REQ-001).
/// </summary>
public class LearningStorageService : ILearningStorageService
{
    private readonly LearningRepository _repository;
    private readonly ILogger<LearningStorageService> _logger;
    private readonly ILearningObserver? _metricsCollector;

    public LearningStorageService(
        LearningRepository repository,
        ILogger<LearningStorageService> logger,
        ILearningObserver? metricsCollector = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsCollector = metricsCollector;
    }

    /// <summary>
    /// Creates and stores a new learning.
    /// </summary>
    public async Task<string> CreateLearningAsync(
        string title,
        string description,
        string triggerType,
        string scope,
        double confidence,
        string? sourceAgent = null,
        string? sourceTaskId = null,
        string? tags = null,
        string? createdBy = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0");
        }

        var createdAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var learning = new Learning
        {
            LearningId = Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            TriggerType = triggerType,
            Scope = scope,
            SourceAgent = sourceAgent,
            SourceTaskId = sourceTaskId,
            Tags = tags,
            Confidence = confidence,
            Status = "Active",
            TimesApplied = 0,
            CreatedAt = createdAtUnix,
            UpdatedAt = createdAtUnix,
            CreatedBy = createdBy ?? "system"
        };

        var learningId = await _repository.AddAsync(learning, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Created learning {LearningId} with title '{Title}' (scope: {Scope}, confidence: {Confidence})",
            learningId, title, scope, confidence);
        
        // Fire observability event
        if (_metricsCollector != null)
        {
            await _metricsCollector.OnLearningCreatedAsync(
                learningId, title, triggerType, scope, confidence, sourceAgent, createdAtUnix).ConfigureAwait(false);
        }
        
        return learningId;
    }

    /// <summary>
    /// Retrieves a learning by ID.
    /// </summary>
    public async Task<Learning?> GetLearningAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        return await _repository.GetByIdAsync(learningId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all learnings (including archived/suppressed).
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetAllLearningsAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets only active learnings.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetActiveLearningsAsync(CancellationToken ct = default)
    {
        return await _repository.GetActiveAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by scope.
    /// Common scopes: Global, Agent, Skill, Project, Domain.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetLearningsByScopeAsync(
        string scope,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return await _repository.GetByScopeAsync(scope, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by status.
    /// Common statuses: Active, Suppressed, Superseded, Archived.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetLearningsByStatusAsync(
        string status,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        return await _repository.GetByStatusAsync(status, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings from a specific source agent.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetLearningsBySourceAgentAsync(
        string sourceAgent,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAgent);
        return await _repository.GetBySourceAgentAsync(sourceAgent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings from a specific source task (for provenance tracking).
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetLearningsBySourceTaskAsync(
        string sourceTaskId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTaskId);
        return await _repository.GetBySourceTaskAsync(sourceTaskId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets active learnings that have embeddings (ready for semantic search).
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetEmbeddedLearningsAsync(CancellationToken ct = default)
    {
        return await _repository.GetWithEmbeddingsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates an existing learning.
    /// </summary>
    public async Task UpdateLearningAsync(Learning learning, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(learning);
        
        learning.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
        
        _logger.LogInformation("Updated learning {LearningId}", learning.LearningId);
    }

    /// <summary>
    /// Suppresses a learning (prevents injection into prompts).
    /// </summary>
    public async Task SuppressLearningAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        
        var learning = await GetLearningAsync(learningId, ct).ConfigureAwait(false);
        if (learning == null)
        {
            _logger.LogWarning("Attempted to suppress non-existent learning {LearningId}", learningId);
            return;
        }

        var previousStatus = learning.Status;
        learning.Status = "Suppressed";
        var updatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        learning.UpdatedAt = updatedAtUnix;
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
        
        _logger.LogInformation("Suppressed learning {LearningId}", learningId);
        
        // Fire observability event
        if (_metricsCollector != null)
        {
            await _metricsCollector.OnLearningStatusChangedAsync(
                learningId, previousStatus, "Suppressed", "User suppression", updatedAtUnix).ConfigureAwait(false);
            await _metricsCollector.OnLearningSuppressionAsync(learningId, null, updatedAtUnix).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Marks a learning as superseded (replaced by a newer learning).
    /// </summary>
    public async Task SupersedeLearningAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        
        var learning = await GetLearningAsync(learningId, ct).ConfigureAwait(false);
        if (learning == null)
        {
            _logger.LogWarning("Attempted to supersede non-existent learning {LearningId}", learningId);
            return;
        }

        var previousStatus = learning.Status;
        learning.Status = "Superseded";
        var updatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        learning.UpdatedAt = updatedAtUnix;
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
        
        _logger.LogInformation("Marked learning {LearningId} as superseded", learningId);
        
        // Fire observability event
        if (_metricsCollector != null)
        {
            await _metricsCollector.OnLearningStatusChangedAsync(
                learningId, previousStatus, "Superseded", "User supersession", updatedAtUnix).ConfigureAwait(false);
            await _metricsCollector.OnLearningSupersededAsync(learningId, null, updatedAtUnix).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Promotes a learning to a broader scope.
    /// Scope hierarchy: Skill → Agent → Project → Domain → Global.
    /// If already at Global scope, no change is made.
    /// Implements LM-REQ-008: Users SHALL promote learnings.
    /// </summary>
    /// <param name="learningId">The learning ID to promote.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new scope after promotion, or null if learning not found or already at Global.</returns>
    public async Task<string?> PromoteLearningAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        
        var learning = await GetLearningAsync(learningId, ct).ConfigureAwait(false);
        if (learning == null)
        {
            _logger.LogWarning("Attempted to promote non-existent learning {LearningId}", learningId);
            return null;
        }

        var oldScope = learning.Scope;
        var newScope = oldScope.ToUpperInvariant() switch
        {
            "SKILL" => "Agent",
            "AGENT" => "Project",
            "PROJECT" => "Domain",
            "DOMAIN" => "Global",
            "GLOBAL" => "Global", // Already at highest scope
            _ => "Global" // Unknown scope, promote to Global
        };

        if (newScope == oldScope)
        {
            _logger.LogInformation(
                "Learning {LearningId} is already at {Scope} scope, no promotion performed",
                learningId, oldScope);
            return null;
        }

        learning.Scope = newScope;
        var updatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        learning.UpdatedAt = updatedAtUnix;
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
        
        _logger.LogInformation(
            "Promoted learning {LearningId} from {OldScope} to {NewScope}",
            learningId, oldScope, newScope);
        
        // Fire observability event
        if (_metricsCollector != null)
        {
            await _metricsCollector.OnLearningPromotedAsync(learningId, oldScope, newScope, updatedAtUnix).ConfigureAwait(false);
        }
        
        return newScope;
    }

    /// <summary>
    /// Archives a learning (soft delete via status change).
    /// </summary>
    public async Task ArchiveLearningAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        await _repository.DeleteAsync(learningId, ct).ConfigureAwait(false);
        _logger.LogInformation("Archived learning {LearningId}", learningId);
    }

    /// <summary>
    /// Records that a learning was applied/injected into a prompt.
    /// Increments the times_applied counter.
    /// </summary>
    public async Task RecordLearningUsageAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        await _repository.IncrementTimesAppliedAsync(learningId, ct).ConfigureAwait(false);
        _logger.LogDebug("Recorded usage of learning {LearningId}", learningId);
    }

    /// <summary>
    /// Adds an embedding to a learning for semantic search.
    /// </summary>
    public async Task SetLearningEmbeddingAsync(
        string learningId,
        byte[] embeddingBlob,
        int embeddingDimensions,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        ArgumentNullException.ThrowIfNull(embeddingBlob);
        
        if (embeddingDimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(embeddingDimensions), "Must be greater than 0");
        }

        var learning = await GetLearningAsync(learningId, ct).ConfigureAwait(false);
        if (learning == null)
        {
            _logger.LogWarning("Attempted to set embedding for non-existent learning {LearningId}", learningId);
            return;
        }

        learning.EmbeddingBlob = embeddingBlob;
        learning.EmbeddingDimensions = embeddingDimensions;
        learning.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Set embedding for learning {LearningId} ({Dimensions} dimensions)",
            learningId, embeddingDimensions);
    }

    /// <summary>
    /// Gets learning statistics for monitoring.
    /// </summary>
    public async Task<LearningStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        
        return new LearningStatistics
        {
            TotalLearnings = all.Count,
            ActiveCount = all.Count(l => l.Status == "Active"),
            SuppressedCount = all.Count(l => l.Status == "Suppressed"),
            SupersededCount = all.Count(l => l.Status == "Superseded"),
            ArchivedCount = all.Count(l => l.Status == "Archived"),
            EmbeddedCount = all.Count(l => l.EmbeddingBlob != null),
            AverageConfidence = all.Count > 0 ? all.Average(l => l.Confidence) : 0.0,
            TotalTimesApplied = all.Sum(l => l.TimesApplied)
        };
    }
}

/// <summary>
/// Statistics about the learning memory system.
/// </summary>
public class LearningStatistics
{
    public int TotalLearnings { get; set; }
    public int ActiveCount { get; set; }
    public int SuppressedCount { get; set; }
    public int SupersededCount { get; set; }
    public int ArchivedCount { get; set; }
    public int EmbeddedCount { get; set; }
    public double AverageConfidence { get; set; }
    public int TotalTimesApplied { get; set; }
}
