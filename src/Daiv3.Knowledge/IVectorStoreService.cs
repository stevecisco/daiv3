using Daiv3.Persistence.Entities;

namespace Daiv3.Knowledge;

/// <summary>
/// Manages storage and retrieval of embeddings and index entries in SQLite.
/// Handles both Tier 1 (topic) and Tier 2 (chunk) indices.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Stores a topic index entry (Tier 1) with its embedding.
    /// </summary>
    Task<string> StoreTopicIndexAsync(
        string docId,
        string summaryText,
        float[] embedding,
        string sourcePath,
        string fileHash,
        string? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a chunk index entry (Tier 2) with its embedding.
    /// </summary>
    Task<string> StoreChunkAsync(
        string docId,
        string chunkText,
        float[] embedding,
        int chunkOrder,
        string? topicTags = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a topic index entry by document ID.
    /// </summary>
    Task<TopicIndex?> GetTopicIndexAsync(string docId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all topic indices (for loading into memory for search).
    /// </summary>
    Task<IReadOnlyList<TopicIndex>> GetAllTopicIndicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves all chunks for a specific document.
    /// </summary>
    Task<IReadOnlyList<ChunkIndex>> GetChunksByDocumentAsync(string docId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific chunk by ID.
    /// </summary>
    Task<ChunkIndex?> GetChunkAsync(string chunkId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a topic index entry and all its associated chunks.
    /// </summary>
    Task DeleteTopicAndChunksAsync(string docId, CancellationToken ct = default);

    /// <summary>
    /// Gets the count of documents in Tier 1 index.
    /// </summary>
    Task<int> GetTopicIndexCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the count of chunks in Tier 2 index.
    /// </summary>
    Task<int> GetChunkIndexCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a topic index entry exists for a document.
    /// </summary>
    Task<bool> TopicIndexExistsAsync(string docId, CancellationToken ct = default);
}
