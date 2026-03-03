namespace Daiv3.Persistence.Entities;

/// <summary>
/// Represents a document in the knowledge base.
/// Tracks source files and change detection.
/// </summary>
public class Document
{
    public string DocId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long LastModified { get; set; }
    public string Status { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Represents a project in the system.
/// Projects are scoped knowledge bases.
/// </summary>
public class Project
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RootPaths { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
}

/// <summary>
/// Represents a task in a project.
/// Stores dependency metadata for orchestrated execution.
/// </summary>
public class ProjectTask
{
    public string TaskId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public long? ScheduledAt { get; set; }
    public long? NextRunAt { get; set; }
    public long? LastRunAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? DependenciesJson { get; set; }
    public string? ResultJson { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}

/// <summary>
/// Represents a topic index entry.
/// Tier 1 - One embedding per document.
/// </summary>
public class TopicIndex
{
    public string DocId { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public byte[] EmbeddingBlob { get; set; } = Array.Empty<byte>();
    public int EmbeddingDimensions { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long IngestedAt { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Represents a chunk index entry.
/// Tier 2 - Multiple embeddings per document.
/// </summary>
public class ChunkIndex
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocId { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public byte[] EmbeddingBlob { get; set; } = Array.Empty<byte>();
    public int EmbeddingDimensions { get; set; }
    public int ChunkOrder { get; set; }
    public string? TopicTags { get; set; }
    public long CreatedAt { get; set; }
}

/// <summary>
/// Represents a model queue entry for persistent queue state.
/// Used for offline queueing and request tracking.
/// </summary>
public class ModelQueueEntry
{
    public string RequestId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long? StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents an agent definition.
/// Stores agent configuration in user-editable JSON format.
/// </summary>
public class Agent
{
    /// <summary>
    /// Agent unique identifier (UUID as string).
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Agent name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Agent purpose/description.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// List of enabled skill names (stored as JSON array).
    /// </summary>
    public string? EnabledSkillsJson { get; set; }

    /// <summary>
    /// Agent-specific configuration (stored as JSON object).
    /// User-editable configuration parameters.
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>
    /// Agent status: active, archived, deleted.
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// When the agent was created (Unix timestamp).
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// When the agent was last updated (Unix timestamp).
    /// </summary>
    public long UpdatedAt { get; set; }
}

/// <summary>
/// Represents a learning record in the learning memory system.
/// Learnings capture corrections, improvements, and insights for future agent execution.
/// Includes provenance tracking and timestamps per LM-DATA-001.
/// </summary>
public class Learning
{
    /// <summary>
    /// Unique identifier for the learning (UUID as string).
    /// </summary>
    public string LearningId { get; set; } = string.Empty;

    /// <summary>
    /// Short human-readable summary of what was learned.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full explanation: what happened, what was wrong, what the correct approach is.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Trigger type: UserFeedback, SelfCorrection, CompilationError, ToolFailure, KnowledgeConflict, Explicit.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Scope where this applies: Global, Agent, Skill, Project, Domain.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The agent or skill that generated this learning (nullable).
    /// Provenance field per LM-DATA-001.
    /// </summary>
    public string? SourceAgent { get; set; }

    /// <summary>
    /// The task or session in which the learning occurred (nullable).
    /// Provenance field per LM-DATA-001 for traceability.
    /// </summary>
    public string? SourceTaskId { get; set; }

    /// <summary>
    /// Vector embedding of the description for semantic retrieval (nullable until generated).
    /// </summary>
    public byte[]? EmbeddingBlob { get; set; }

    /// <summary>
    /// Dimensionality of the embedding vector (e.g., 384, 768).
    /// </summary>
    public int? EmbeddingDimensions { get; set; }

    /// <summary>
    /// Comma-separated tags for filtering (e.g., 'csharp', 'file-io', 'prompt-format').
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Confidence score 0.0-1.0.
    /// High confidence = injected automatically, low confidence = injected as suggestion.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Status: Active, Suppressed, Superseded, Archived.
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Retrieval count. Surfaces high-value learnings and identifies stale ones.
    /// </summary>
    public int TimesApplied { get; set; }

    /// <summary>
    /// When the learning was created (Unix timestamp).
    /// Timestamp field per LM-DATA-001.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// When the learning was last updated (Unix timestamp).
    /// Timestamp field per LM-DATA-001.
    /// </summary>
    public long UpdatedAt { get; set; }

    /// <summary>
    /// Agent ID or 'user' if manually entered.
    /// Provenance field per LM-DATA-001.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Represents a learning promotion record in the knowledge back-propagation system.
/// Tracks when learnings are promoted across hierarchical scopes for audit trail and provenance.
/// Implements KBP-DATA-001 (source task/session IDs) and KBP-DATA-002 (target scope and timestamps).
/// </summary>
public class Promotion
{
    /// <summary>
    /// Unique identifier for the promotion action (UUID as string).
    /// </summary>
    public string PromotionId { get; set; } = string.Empty;

    /// <summary>
    /// The learning that was promoted.
    /// </summary>
    public string LearningId { get; set; } = string.Empty;

    /// <summary>
    /// The scope before promotion (e.g., 'Skill', 'Agent').
    /// </summary>
    public string FromScope { get; set; } = string.Empty;

    /// <summary>
    /// The scope after promotion (e.g., 'Agent', 'Project', 'Domain', 'Global').
    /// Implements KBP-DATA-002.
    /// </summary>
    public string ToScope { get; set; } = string.Empty;

    /// <summary>
    /// When the promotion occurred (Unix timestamp).
    /// Implements KBP-DATA-002.
    /// </summary>
    public long PromotedAt { get; set; }

    /// <summary>
    /// Agent ID or 'user' who performed the promotion.
    /// </summary>
    public string PromotedBy { get; set; } = string.Empty;

    /// <summary>
    /// The task or session in which the promotion was triggered (nullable).
    /// Implements KBP-DATA-001 for traceability.
    /// </summary>
    public string? SourceTaskId { get; set; }

    /// <summary>
    /// The agent that triggered or requested the promotion (nullable).
    /// </summary>
    public string? SourceAgent { get; set; }

    /// <summary>
    /// Optional human-readable notes about why the promotion occurred (nullable).
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Represents an agent-proposed learning promotion that requires user confirmation.
/// Implements KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
/// </summary>
public class AgentPromotionProposal
{
    /// <summary>
    /// Unique identifier for the proposal (UUID as string).
    /// </summary>
    public string ProposalId { get; set; } = string.Empty;

    /// <summary>
    /// The learning that the agent is proposing for promotion.
    /// </summary>
    public string LearningId { get; set; } = string.Empty;

    /// <summary>
    /// The agent that proposed the promotion.
    /// </summary>
    public string ProposingAgent { get; set; } = string.Empty;

    /// <summary>
    /// The task or session in which the proposal was generated.
    /// </summary>
    public string? SourceTaskId { get; set; }

    /// <summary>
    /// Current scope of the learning being proposed for promotion.
    /// </summary>
    public string FromScope { get; set; } = string.Empty;

    /// <summary>
    /// The scope the agent is proposing promotion to (e.g., 'Agent', 'Project', 'Domain', 'Global').
    /// </summary>
    public string SuggestedTargetScope { get; set; } = string.Empty;

    /// <summary>
    /// The agent's reasoning for why this learning should be promoted (nullable).
    /// Provides justification visible to the user for decision-making.
    /// </summary>
    public string? Justification { get; set; }

    /// <summary>
    /// Recommendation score (0.0-1.0) from the agent on how important this promotion is.
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Status: Pending, Approved, Rejected.
    /// Tracks the confirmation workflow.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When the proposal was created (Unix timestamp).
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// When the proposal was last updated/responded to (Unix timestamp).
    /// </summary>
    public long UpdatedAt { get; set; }

    /// <summary>
    /// User or system entity that confirmed or rejected the proposal (nullable until confirmed/rejected).
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Timestamp when the decision was made (nullable until confirmed/rejected).
    /// </summary>
    public long? ReviewedAt { get; set; }

    /// <summary>
    /// Optional reason for rejection, if the proposal was rejected (nullable).
    /// </summary>
    public string? RejectionReason { get; set; }
}
/// <summary>
/// Represents a reverted promotion (undo of a learning promotion).
/// Tracks when promotions are undone for reversibility and audit trail.
/// Implements KBP-NFR-001: Promotions SHOULD be transparent and reversible.
/// </summary>
public class RevertPromotion
{
    /// <summary>
    /// Unique identifier for the revert action (UUID as string).
    /// </summary>
    public string RevertId { get; set; } = string.Empty;

    /// <summary>
    /// The promotion that was reverted (foreign key to promotions table).
    /// </summary>
    public string PromotionId { get; set; } = string.Empty;

    /// <summary>
    /// The learning that was affected by the revert.
    /// </summary>
    public string LearningId { get; set; } = string.Empty;

    /// <summary>
    /// When the revert occurred (Unix timestamp).
    /// Implements transparency requirement of KBP-NFR-001.
    /// </summary>
    public long RevertedAt { get; set; }

    /// <summary>
    /// User or agent who performed the revert.
    /// </summary>
    public string RevertedBy { get; set; } = string.Empty;

    /// <summary>
    /// The scope the learning had after the original promotion (what it was reverted FROM).
    /// </summary>
    public string RevertedFromScope { get; set; } = string.Empty;

    /// <summary>
    /// The scope the learning was restored to (the FromScope of the original promotion).
    /// </summary>
    public string RevertedToScope { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about why the promotion was reverted (nullable).
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Represents instrumentation metrics for promotion operations.
/// Tracks performance and usage metrics for transparency (KBP-NFR-001).
/// </summary>
public class PromotionMetric
{
    /// <summary>
    /// Unique identifier for the metric record (UUID as string).
    /// </summary>
    public string MetricId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the metric (e.g., 'promotion_latency_ms', 'total_promotions', 'revert_count').
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// The measured value for this metric.
    /// </summary>
    public double MetricValue { get; set; }

    /// <summary>
    /// When the metric was recorded (Unix timestamp).
    /// </summary>
    public long RecordedAt { get; set; }

    /// <summary>
    /// Optional start of the period this metric covers (for aggregates).
    /// </summary>
    public long? PeriodStart { get; set; }

    /// <summary>
    /// Optional end of the period this metric covers (for aggregates).
    /// </summary>
    public long? PeriodEnd { get; set; }

    /// <summary>
    /// Optional context/tags for the metric (nullable).
    /// Example: "gauge:promotion_queue_depth" or "histogram:promotion_duration".
    /// </summary>
    public string? Context { get; set; }
}

/// <summary>
/// Represents web fetch metadata for fetched web content.
/// Tracks source URL, fetch date, and content hash for change detection.
/// Implements WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.
/// </summary>
public class WebFetch
{
    /// <summary>
    /// Unique identifier for the web fetch record (UUID as string).
    /// </summary>
    public string WebFetchId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the document this web fetch created.
    /// Foreign key to documents table.
    /// </summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>
    /// The URL that was fetched.
    /// Implements WFC-DATA-001.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the fetched content for change detection.
    /// Implements WFC-DATA-001.
    /// Can be used to detect if content has changed on refetch.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// When the content was fetched (Unix timestamp).
    /// Implements WFC-DATA-001.
    /// Used to determine if content is stale.
    /// </summary>
    public long FetchDate { get; set; }

    /// <summary>
    /// The title extracted from the fetched page (nullable).
    /// Used for display and metadata.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// A brief description extracted from the page or provided by the fetcher (nullable).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Status of the web fetch: active, stale, error, deleted.
    /// active = currently tracked, stale = needs refetch, error = last fetch failed.
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Optional error message if the last fetch failed (nullable).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When this web fetch record was created (Unix timestamp).
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// When this web fetch record was last updated (Unix timestamp).
    /// Updated when content is refetched or status changes.
    /// </summary>
    public long UpdatedAt { get; set; }
}