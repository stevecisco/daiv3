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
    private readonly PromotionRepository? _promotionRepository;
    private readonly RevertPromotionRepository? _revertPromotionRepository;
    private readonly PromotionMetricRepository? _promotionMetricRepository;
    private readonly ILogger<LearningStorageService> _logger;
    private readonly ILearningObserver? _metricsCollector;

    public LearningStorageService(
        LearningRepository repository,
        ILogger<LearningStorageService> logger,
        ILearningObserver? metricsCollector = null,
        PromotionRepository? promotionRepository = null,
        RevertPromotionRepository? revertPromotionRepository = null,
        PromotionMetricRepository? promotionMetricRepository = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsCollector = metricsCollector;
        _promotionRepository = promotionRepository;
        _revertPromotionRepository = revertPromotionRepository;
        _promotionMetricRepository = promotionMetricRepository;
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
    /// Implements KBP-DATA-001: Records promotion with source task/session.
    /// Implements KBP-DATA-002: Records target scope and timestamp.
    /// </summary>
    /// <param name="learningId">The learning ID to promote.</param>
    /// <param name="promotedBy">User or agent performing the promotion (default: 'user').</param>
    /// <param name="sourceTaskId">Optional task/session ID triggering the promotion.</param>
    /// <param name="sourceAgent">Optional agent triggering the promotion.</param>
    /// <param name="notes">Optional notes about why the promotion occurred.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new scope after promotion, or null if learning not found or already at Global.</returns>
    public async Task<string?> PromoteLearningAsync(
        string learningId,
        string promotedBy = "user",
        string? sourceTaskId = null,
        string? sourceAgent = null,
        string? notes = null,
        CancellationToken ct = default)
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
        
        // KBP-DATA-001/002: Record promotion history with source task/session, target scope, and timestamp
        if (_promotionRepository != null)
        {
            var promotion = new Promotion
            {
                PromotionId = Guid.NewGuid().ToString(),
                LearningId = learningId,
                FromScope = oldScope,
                ToScope = newScope,
                PromotedAt = updatedAtUnix,
                PromotedBy = promotedBy,
                SourceTaskId = sourceTaskId,
                SourceAgent = sourceAgent,
                Notes = notes
            };

            await _promotionRepository.AddAsync(promotion, ct).ConfigureAwait(false);
        }
        
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
    /// Promotes multiple learnings from a completed task to specified target scopes in batch.
    /// Implements KBP-REQ-002: Allow users to select promotion targets when task completes.
    /// </summary>
    /// <param name="taskId">The task ID whose learnings are being promoted.</param>
    /// <param name="promotions">List of learning IDs and their target scopes.</param>
    /// <param name="promotedBy">User or agent performing the promotions (default: 'user').</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing successful and failed promotion counts.</returns>
    public async Task<PromotionBatchResult> PromoteLearningsFromTaskAsync(
        string taskId,
        IReadOnlyList<LearningPromotionSelection> promotions,
        string promotedBy = "user",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(promotions);

        var result = new PromotionBatchResult();

        if (promotions.Count == 0)
        {
            _logger.LogInformation("No promotions requested for task {TaskId}", taskId);
            return result;
        }

        // Get all learnings from this task
        var learnings = await GetLearningsBySourceTaskAsync(taskId, ct).ConfigureAwait(false);
        var learningMap = learnings.ToDictionary(l => l.LearningId);

        _logger.LogInformation(
            "Processing {PromotionCount} promotions from task {TaskId}",
            promotions.Count, taskId);

        foreach (var selection in promotions)
        {
            try
            {
                // Skip invalid learning IDs
                if (!learningMap.TryGetValue(selection.LearningId, out var learning))
                {
                    _logger.LogWarning(
                        "Learning {LearningId} not found from task {TaskId}, skipping promotion",
                        selection.LearningId, taskId);
                    result.FailedPromotions.Add(
                        selection,
                        new PromotionError(
                            "LearningNotFound",
                            $"Learning {selection.LearningId} not found from task {taskId}"));
                    continue;
                }

                // Validate target scope
                var validScopes = new[] { "Skill", "Agent", "Project", "Domain", "Global" };
                if (!validScopes.Contains(selection.TargetScope, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Invalid target scope '{Scope}' for learning {LearningId}",
                        selection.TargetScope, selection.LearningId);
                    result.FailedPromotions.Add(
                        selection,
                        new PromotionError(
                            "InvalidTargetScope",
                            $"Target scope '{selection.TargetScope}' is not valid"));
                    continue;
                }

                // Promote the learning directly to the target scope
                var normalizedTargetScope = char.ToUpperInvariant(selection.TargetScope[0]) + selection.TargetScope[1..].ToLowerInvariant();
                
                // Check if already at or beyond target scope
                var scopeHierarchy = new[] { "Skill", "Agent", "Project", "Domain", "Global" };
                var currentIndex = Array.IndexOf(scopeHierarchy, learning.Scope);
                var targetIndex = Array.IndexOf(scopeHierarchy, normalizedTargetScope);

                if (currentIndex >= targetIndex)
                {
                    _logger.LogInformation(
                        "Learning {LearningId} is already at {CurrentScope} which is at or beyond {TargetScope}, skipping promotion",
                        selection.LearningId, learning.Scope, normalizedTargetScope);
                    result.FailedPromotions.Add(
                        selection,
                        new PromotionError(
                            "AlreadyAtOrBeyondTargetScope",
                            $"Learning is already at {learning.Scope} which is at or beyond {normalizedTargetScope}"));
                    continue;
                }

                // Update the learning scope directly
                var oldScope = learning.Scope;
                learning.Scope = normalizedTargetScope;
                var updatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                learning.UpdatedAt = updatedAtUnix;
                await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);
                
                // Record promotion history with source task
                if (_promotionRepository != null)
                {
                    var promotion = new Promotion
                    {
                        PromotionId = Guid.NewGuid().ToString(),
                        LearningId = selection.LearningId,
                        FromScope = oldScope,
                        ToScope = normalizedTargetScope,
                        PromotedAt = updatedAtUnix,
                        PromotedBy = promotedBy,
                        SourceTaskId = taskId,
                        Notes = selection.Notes
                    };

                    await _promotionRepository.AddAsync(promotion, ct).ConfigureAwait(false);
                }
                
                result.SuccessfulPromotions.Add(selection);
                _logger.LogInformation(
                    "Promoted learning {LearningId} from {OldScope} to {NewScope} as part of task {TaskId}",
                    selection.LearningId, oldScope, normalizedTargetScope, taskId);
                
                // Fire observability event
                if (_metricsCollector != null)
                {
                    await _metricsCollector.OnLearningPromotedAsync(selection.LearningId, oldScope, normalizedTargetScope, updatedAtUnix).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error promoting learning {LearningId} from task {TaskId}",
                    selection.LearningId, taskId);
                result.FailedPromotions.Add(
                    selection,
                    new PromotionError("PromotionException", ex.Message));
            }
        }

        _logger.LogInformation(
            "Promotion batch completed: {SuccessCount} successful, {FailureCount} failed",
            result.SuccessfulPromotions.Count, result.FailedPromotions.Count);

        return result;
    }

    /// <summary>
    /// Reverts a promotion, restoring the learning to its previous scope.
    /// Implements KBP-NFR-001: Promotions SHOULD be reversible.
    /// </summary>
    /// <param name="promotionId">The promotion ID to revert.</param>
    /// <param name="revertedBy">User or agent performing the revert (default: 'user').</param>
    /// <param name="notes">Optional notes about why the promotion was reverted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successfully reverted, false if promotion not found or already reverted.</returns>
    public async Task<bool> RevertPromotionAsync(
        string promotionId,
        string revertedBy = "user",
        string? notes = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionId);

        if (_promotionRepository == null || _revertPromotionRepository == null)
        {
            _logger.LogWarning("Revert promotion requested but repositories not configured");
            return false;
        }

        // Get the promotion record
        var promotion = await _promotionRepository.GetByIdAsync(promotionId, ct).ConfigureAwait(false);
        if (promotion == null)
        {
            _logger.LogWarning("Attempted to revert non-existent promotion {PromotionId}", promotionId);
            return false;
        }

        // Check if already reverted
        var existingRevert = await _revertPromotionRepository.GetByPromotionIdAsync(promotionId, ct).ConfigureAwait(false);
        if (existingRevert != null)
        {
            _logger.LogWarning("Promotion {PromotionId} has already been reverted (Revert ID: {RevertId})", promotionId, existingRevert.RevertId);
            return false;
        }

        // Get the learning
        var learning = await _repository.GetByIdAsync(promotion.LearningId, ct).ConfigureAwait(false);
        if (learning == null)
        {
            _logger.LogError("Learning {LearningId} not found when reverting promotion {PromotionId}", promotion.LearningId, promotionId);
            return false;
        }

        // Restore learning to previous scope
        var previousScope = learning.Scope;
        learning.Scope = promotion.FromScope;
        var revertedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        learning.UpdatedAt = revertedAtUnix;
        await _repository.UpdateAsync(learning, ct).ConfigureAwait(false);

        // Record the revert
        var revert = new RevertPromotion
        {
            RevertId = Guid.NewGuid().ToString(),
            PromotionId = promotionId,
            LearningId = promotion.LearningId,
            RevertedAt = revertedAtUnix,
            RevertedBy = revertedBy,
            RevertedFromScope = previousScope,
            RevertedToScope = promotion.FromScope,
            Notes = notes
        };

        await _revertPromotionRepository.AddAsync(revert, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Reverted promotion {PromotionId} for learning {LearningId} from {FromScope} to {ToScope}",
            promotionId, promotion.LearningId, previousScope, promotion.FromScope);

        // Record revert metric
        if (_promotionMetricRepository != null)
        {
            var metric = new PromotionMetric
            {
                MetricId = Guid.NewGuid().ToString(),
                MetricName = "revert_events",
                MetricValue = 1.0,
                RecordedAt = revertedAtUnix,
                Context = $"promotion:{promotionId}"
            };
            await _promotionMetricRepository.AddAsync(metric, ct).ConfigureAwait(false);
        }

        // Fire observability event
        if (_metricsCollector != null)
        {
            await _metricsCollector.OnLearningPromotedAsync(promotion.LearningId, previousScope, promotion.FromScope, revertedAtUnix).ConfigureAwait(false);
        }

        return true;
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
/// <summary>
/// Represents a user's selection to promote a learning to a specific scope.
/// Used in batch promotion workflows (KBP-REQ-002).
/// </summary>
public class LearningPromotionSelection
{
    /// <summary>
    /// The learning ID to promote.
    /// </summary>
    public required string LearningId { get; set; }

    /// <summary>
    /// The target scope to promote to (e.g., 'Project', 'Global').
    /// </summary>
    public required string TargetScope { get; set; }

    /// <summary>
    /// Optional notes about why this learning is being promoted.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Result of a batch promotion operation.
/// Tracks successful and failed promotions.
/// </summary>
public class PromotionBatchResult
{
    /// <summary>
    /// Selections that were successfully promoted.
    /// </summary>
    public List<LearningPromotionSelection> SuccessfulPromotions { get; } = new();

    /// <summary>
    /// Selections that failed and their error details.
    /// </summary>
    public Dictionary<LearningPromotionSelection, PromotionError> FailedPromotions { get; } = new();

    /// <summary>
    /// Total count of promotions requested.
    /// </summary>
    public int TotalCount => SuccessfulPromotions.Count + FailedPromotions.Count;

    /// <summary>
    /// Whether all promotions succeeded.
    /// </summary>
    public bool AllSucceeded => FailedPromotions.Count == 0;
}

/// <summary>
/// Error details for a failed promotion.
/// </summary>
public class PromotionError
{
    public PromotionError(string errorCode, string message)
    {
        ErrorCode = errorCode;
        Message = message;
    }

    /// <summary>
    /// Error classification code (e.g., 'LearningNotFound', 'InvalidTargetScope').
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }
}