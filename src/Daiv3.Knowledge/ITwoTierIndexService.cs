namespace Daiv3.Knowledge;

/// <summary>
/// Manages two-tier vector search across the knowledge base.
/// Tier 1: Fast coarse search using topic embeddings (one per document).
/// Tier 2: Fine-grained search using chunk embeddings for top candidates from Tier 1.
/// </summary>
public interface ITwoTierIndexService
{
    /// <summary>
    /// Initializes the search index, optionally loading all Tier 1 embeddings into memory.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Searches both tiers with a query embedding.
    /// Returns top candidates from Tier 1, then searches Tier 2 for those documents.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector for the query.</param>
    /// <param name="tier1TopK">Number of top documents to retrieve from Tier 1.</param>
    /// <param name="tier2TopK">Number of top chunks to retrieve from Tier 2 for each Tier 1 result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Combined search results from both tiers.</returns>
    Task<TwoTierSearchResults> SearchAsync(
        float[] queryEmbedding,
        int tier1TopK = 10,
        int tier2TopK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about the current index state.
    /// </summary>
    Task<IndexStatistics> GetStatisticsAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears memory-cached embeddings (useful for memory management).
    /// </summary>
    Task ClearCacheAsync();
}

/// <summary>
/// Statistics about the knowledge index.
/// </summary>
public class IndexStatistics
{
    /// <summary>
    /// Total number of documents indexed.
    /// </summary>
    public int DocumentCount { get; set; }

    /// <summary>
    /// Total number of chunks in Tier 2.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Number of topic embeddings currently in memory.
    /// </summary>
    public int CachedTopicEmbeddings { get; set; }

    /// <summary>
    /// Estimated memory usage in bytes.
    /// </summary>
    public long EstimatedMemoryBytes { get; set; }
}
