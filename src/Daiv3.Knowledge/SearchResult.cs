namespace Daiv3.Knowledge;

/// <summary>
/// Represents the result of a two-tier search operation.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// The ID of the matched document.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// The similarity score (0-1) from Tier 1 or Tier 2.
    /// </summary>
    public float SimilarityScore { get; set; }

    /// <summary>
    /// The text content (summary for Tier 1, chunk for Tier 2).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The source path of the document.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Which tier this result came from (1 or 2).
    /// </summary>
    public int TierLevel { get; set; }

    /// <summary>
    /// For Tier 2 results, the chunk order within the document.
    /// </summary>
    public int? ChunkOrder { get; set; }
}

/// <summary>
/// Represents a batch of search results from both tiers.
/// </summary>
public class TwoTierSearchResults
{
    /// <summary>
    /// Top candidates from Tier 1 search (by document summary).
    /// </summary>
    public IReadOnlyList<SearchResult> Tier1Results { get; set; } = new List<SearchResult>();

    /// <summary>
    /// Refined results from Tier 2 search (specific chunks from top Tier 1 documents).
    /// </summary>
    public IReadOnlyList<SearchResult> Tier2Results { get; set; } = new List<SearchResult>();

    /// <summary>
    /// Total execution time for the search in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}
