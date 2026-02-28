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
