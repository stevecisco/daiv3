using System.Text;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Creates markdown draft artifacts for Internet-level promotion review.
/// Implements KBP-REQ-005.
/// </summary>
public sealed class KnowledgeInternetDraftService : IKnowledgeInternetDraftService
{
    private readonly ILogger<KnowledgeInternetDraftService> _logger;
    private readonly InternetKnowledgeDraftOptions _options;

    public KnowledgeInternetDraftService(
        ILogger<KnowledgeInternetDraftService> logger,
        IOptions<InternetKnowledgeDraftOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<KnowledgeDraftArtifact> CreateDraftArtifactAsync(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes,
        KnowledgeSummary summary,
        string? sourceTaskId,
        string promotedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(promotedLearnings);
        ArgumentNullException.ThrowIfNull(targetScopes);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(promotedBy);

        var internetLearnings = promotedLearnings
            .Where(learning => targetScopes.TryGetValue(learning.LearningId, out var target)
                && string.Equals(target, "Internet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (internetLearnings.Count == 0)
        {
            throw new ArgumentException("No Internet-level promotions found to create draft artifact.", nameof(targetScopes));
        }

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_options.OutputDirectory);

        var generatedAt = DateTimeOffset.UtcNow;
        var safeTaskToken = SanitizePathSegment(sourceTaskId ?? "manual");
        var fileName = $"internet-promotion-{generatedAt:yyyyMMdd-HHmmss}-{safeTaskToken}.md";
        var artifactPath = Path.Combine(_options.OutputDirectory, fileName);

        var title = string.IsNullOrWhiteSpace(sourceTaskId)
            ? "Knowledge Promotion Draft (Internet)"
            : $"Knowledge Promotion Draft (Internet) - Task {sourceTaskId}";

        var content = BuildMarkdownDraft(
            title,
            sourceTaskId,
            promotedBy,
            generatedAt,
            internetLearnings,
            summary);

        await File.WriteAllTextAsync(artifactPath, content, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Created Internet promotion draft artifact at {ArtifactPath} with {LearningCount} learnings",
            artifactPath,
            internetLearnings.Count);

        return new KnowledgeDraftArtifact
        {
            ArtifactPath = artifactPath,
            FileName = fileName,
            Title = title,
            Content = content,
            GeneratedAt = generatedAt,
            LearningIds = internetLearnings.Select(l => l.LearningId).ToList()
        };
    }

    private string BuildMarkdownDraft(
        string title,
        string? sourceTaskId,
        string promotedBy,
        DateTimeOffset generatedAt,
        IReadOnlyList<Learning> internetLearnings,
        KnowledgeSummary summary)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine("## Review Metadata");
        sb.AppendLine($"- Draft status: Review Required");
        sb.AppendLine($"- Generated: {generatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- Promoted by: {promotedBy}");
        sb.AppendLine($"- Source task: {(string.IsNullOrWhiteSpace(sourceTaskId) ? "N/A" : sourceTaskId)}");
        sb.AppendLine($"- Internet promotion count: {internetLearnings.Count}");
        sb.AppendLine();

        sb.AppendLine("## Proposed Public Post");
        sb.AppendLine();
        sb.AppendLine("### Key Learnings");

        foreach (var learning in internetLearnings)
        {
            var description = learning.Description;
            if (description.Length > _options.MaxDescriptionLength)
            {
                description = description[..(_options.MaxDescriptionLength - 3)] + "...";
            }

            sb.AppendLine($"- **{learning.Title}**");
            sb.AppendLine($"  - Confidence: {learning.Confidence:F2}");
            sb.AppendLine($"  - Trigger: {learning.TriggerType}");
            sb.AppendLine($"  - Summary: {description}");
        }

        sb.AppendLine();
        sb.AppendLine("## Internal Promotion Summary");
        sb.AppendLine();
        sb.AppendLine(summary.SummaryText.Trim());
        sb.AppendLine();
        sb.AppendLine("## Reviewer Checklist");
        sb.AppendLine("- [ ] Validate factual accuracy and remove sensitive details.");
        sb.AppendLine("- [ ] Confirm public-safe phrasing and scope.");
        sb.AppendLine("- [ ] Approve, revise, or reject publication draft.");

        return sb.ToString();
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value
            .Trim()
            .Select(ch => invalidChars.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned) ? "draft" : cleaned;
    }
}
