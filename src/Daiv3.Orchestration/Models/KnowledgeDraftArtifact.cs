using System;
using System.Collections.Generic;

namespace Daiv3.Orchestration.Models;

/// <summary>
/// Represents a generated draft artifact for Internet-level knowledge promotion review.
/// Implements KBP-REQ-005.
/// </summary>
public class KnowledgeDraftArtifact
{
    /// <summary>
    /// Full path to the generated artifact file.
    /// </summary>
    public required string ArtifactPath { get; set; }

    /// <summary>
    /// Generated artifact file name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Human-readable draft title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Markdown content written to the artifact.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// UTC timestamp when the draft was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// Learning IDs represented in this draft.
    /// </summary>
    public required IReadOnlyList<string> LearningIds { get; set; }
}
