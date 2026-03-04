using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration;

/// <summary>
/// Service for generating summaries of knowledge promotions per KBP-REQ-004.
/// Creates human-readable summaries for audit trail and user transparency.
/// </summary>
/// <remarks>
/// Current implementation uses template-based summarization with structured metadata.
/// Future enhancement: Integrate SLM-based summarization for richer narrative summaries.
/// </remarks>
public class KnowledgeSummaryService : IKnowledgeSummaryService
{
    private readonly ILogger<KnowledgeSummaryService> _logger;

    public KnowledgeSummaryService(ILogger<KnowledgeSummaryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<KnowledgeSummary> GenerateSummaryAsync(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes,
        string? sourceTaskId,
        string promotedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(promotedLearnings);
        ArgumentNullException.ThrowIfNull(targetScopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(promotedBy);

        if (promotedLearnings.Count == 0)
        {
            _logger.LogWarning("GenerateSummaryAsync called with no promoted learnings");
            return Task.FromResult(new KnowledgeSummary
            {
                SummaryText = "No learnings were promoted.",
                LearningIds = Array.Empty<string>(),
                SourceScopes = Array.Empty<string>(),
                TargetScopes = Array.Empty<string>(),
                PromotedCount = 0,
                SourceTaskId = sourceTaskId,
                PromotedBy = promotedBy,
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        ct.ThrowIfCancellationRequested();

        // Build detailed information for each learning
        var details = BuildLearningDetails(promotedLearnings, targetScopes);

        // Generate summary text
        var summaryText = GenerateSummaryText(promotedLearnings, targetScopes, sourceTaskId, promotedBy);

        // Collect metadata
        var sourceScopes = promotedLearnings
            .Select(l => l.Scope)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var uniqueTargetScopes = targetScopes.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = new KnowledgeSummary
        {
            SummaryText = summaryText,
            LearningIds = promotedLearnings.Select(l => l.LearningId).ToList(),
            SourceScopes = sourceScopes,
            TargetScopes = uniqueTargetScopes,
            PromotedCount = promotedLearnings.Count,
            SourceTaskId = sourceTaskId,
            PromotedBy = promotedBy,
            GeneratedAt = DateTimeOffset.UtcNow,
            Details = details
        };

        _logger.LogInformation(
            "Generated knowledge summary for {Count} promotions from {SourceScopes} to {TargetScopes}",
            promotedLearnings.Count,
            string.Join(", ", sourceScopes),
            string.Join(", ", uniqueTargetScopes));

        return Task.FromResult(summary);
    }

    /// <summary>
    /// Generates human-readable summary text from promoted learnings.
    /// </summary>
    private string GenerateSummaryText(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes,
        string? sourceTaskId,
        string promotedBy)
    {
        var sb = new StringBuilder();

        // Header
        var count = promotedLearnings.Count;
        var plural = count == 1 ? "learning" : "learnings";
        sb.Append($"Promoted {count} {plural}");

        // Source task context
        if (!string.IsNullOrWhiteSpace(sourceTaskId))
        {
            sb.Append($" from task '{sourceTaskId}'");
        }

        sb.AppendLine(":");
        sb.AppendLine();

        // Group by target scope for clearer summary
        var groupedByTargetScope = promotedLearnings
            .GroupBy(l => targetScopes.TryGetValue(l.LearningId, out var target) ? target : "Unknown")
            .OrderBy(g => GetScopeHierarchyOrder(g.Key));

        foreach (var scopeGroup in groupedByTargetScope)
        {
            var targetScope = scopeGroup.Key;
            var learningsInScope = scopeGroup.ToList();

            sb.AppendLine($"**To {targetScope} scope ({learningsInScope.Count} {(learningsInScope.Count == 1 ? "item" : "items")})**");

            // List learnings with key details
            foreach (var learning in learningsInScope.Take(5)) // Show up to 5 per scope
            {
                sb.AppendLine($"  • {learning.Title}");
                sb.AppendLine($"    Source: {learning.Scope}, Confidence: {learning.Confidence:F2}, Trigger: {learning.TriggerType}");

                // Include shortened description
                var description = learning.Description.Length > 100
                    ? learning.Description.Substring(0, 97) + "..."
                    : learning.Description;
                sb.AppendLine($"    Description: {description}");
            }

            // Show count if there are more learnings
            if (learningsInScope.Count > 5)
            {
                sb.AppendLine($"  ... and {learningsInScope.Count - 5} more");
            }

            sb.AppendLine();
        }

        // Footer with metadata
        sb.AppendLine($"Promoted by: {promotedBy}");
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        // Summary statistics
        var avgConfidence = promotedLearnings.Average(l => l.Confidence);
        var triggerTypes = promotedLearnings
            .GroupBy(l => l.TriggerType)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        sb.AppendLine();
        sb.AppendLine("**Statistics:**");
        sb.AppendLine($"  Average confidence: {avgConfidence:F2}");
        sb.AppendLine($"  Trigger types: {string.Join(", ", triggerTypes)}");

        return sb.ToString();
    }

    /// <summary>
    /// Builds detailed information for each promoted learning.
    /// </summary>
    private List<PromotedLearningDetail> BuildLearningDetails(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes)
    {
        return promotedLearnings.Select(learning => new PromotedLearningDetail
        {
            LearningId = learning.LearningId,
            Title = learning.Title,
            Description = learning.Description,
            Confidence = learning.Confidence,
            SourceScope = learning.Scope,
            TargetScope = targetScopes.TryGetValue(learning.LearningId, out var target) ? target : "Unknown",
            TriggerType = learning.TriggerType
        }).ToList();
    }

    /// <summary>
    /// Returns hierarchy order for scope sorting (lower = narrower scope).
    /// </summary>
    private static int GetScopeHierarchyOrder(string scope)
    {
        return scope.ToLowerInvariant() switch
        {
            "skill" => 1,
            "agent" => 2,
            "project" => 3,
            "domain" => 4,
            "global" => 5,
            _ => 99
        };
    }
}
