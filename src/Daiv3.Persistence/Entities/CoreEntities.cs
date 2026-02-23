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
