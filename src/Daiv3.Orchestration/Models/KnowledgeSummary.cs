using System;
using System.Collections.Generic;

namespace Daiv3.Orchestration.Models;

/// <summary>
/// Summary of knowledge promotions triggered per KBP-REQ-004.
/// Provides human-readable summary of what knowledge was promoted for audit trail and transparency.
/// </summary>
public class KnowledgeSummary
{
    /// <summary>
    /// Human-readable summary text describing what knowledge was promoted.
    /// </summary>
    public required string SummaryText { get; set; }

    /// <summary>
    /// IDs of learnings involved in the promotion.
    /// </summary>
    public required IReadOnlyList<string> LearningIds { get; set; }

    /// <summary>
    /// Source scope(s) of the promoted learnings.
    /// </summary>
    public required IReadOnlyList<string> SourceScopes { get; set; }

    /// <summary>
    /// Target scope(s) of the promotion.
    /// </summary>
    public required IReadOnlyList<string> TargetScopes { get; set; }

    /// <summary>
    /// Number of learnings successfully promoted.
    /// </summary>
    public int PromotedCount { get; set; }

    /// <summary>
    /// Source task ID if promotion was triggered from task completion.
    /// </summary>
    public string? SourceTaskId { get; set; }

    /// <summary>
    /// User or agent who triggered the promotion.
    /// </summary>
    public required string PromotedBy { get; set; }

    /// <summary>
    /// Timestamp when summary was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Detailed learning information (title, description, confidence) for each promoted learning.
    /// </summary>
    public IReadOnlyList<PromotedLearningDetail>? Details { get; set; }
}

/// <summary>
/// Detailed information about a single promoted learning.
/// </summary>
public class PromotedLearningDetail
{
    /// <summary>
    /// Learning ID.
    /// </summary>
    public required string LearningId { get; set; }

    /// <summary>
    /// Learning title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Learning description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Learning confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Source scope before promotion.
    /// </summary>
    public required string SourceScope { get; set; }

    /// <summary>
    /// Target scope after promotion.
    /// </summary>
    public required string TargetScope { get; set; }

    /// <summary>
    /// Trigger type that created this learning.
    /// </summary>
    public required string TriggerType { get; set; }
}
