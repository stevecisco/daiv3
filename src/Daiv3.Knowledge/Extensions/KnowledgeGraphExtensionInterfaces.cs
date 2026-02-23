namespace Daiv3.Knowledge.Extensions;

/// <summary>
/// Optional service for enriching search results with knowledge graph information.
/// Implements decorator pattern - can be registered or omitted without affecting core search behavior.
/// 
/// This interface is reserved for future Knowledge Graph integration.
/// When KG is implemented, this service can be registered to enhance search results
/// with graph-based relevance or semantic relationships without changing ITwoTierIndexService.
/// </summary>
public interface ISearchEnhancer
{
    /// <summary>
    /// Optional post-processing hook to enhance search results with graph information.
    /// This method is called AFTER primary two-tier search completes.
    /// If this enhancer is not registered, search behavior is unchanged.
    /// </summary>
    /// <param name="baselineResults">The search results from the baseline two-tier search.</param>
    /// <param name="queryEmbedding">The original query embedding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enhanced search results, potentially with additional metadata or reordering.</returns>
    /// <remarks>
    /// Implementation MUST:
    /// - Return results compatible with TwoTierSearchResults structure
    /// - Not throw exceptions (use logging instead)
    /// - Complete within reasonable time (~10-20ms absolute maximum)
    /// 
    /// This interface enables KG features like:
    /// - Graph-based relevance scoring
    /// - Semantic relationship enrichment
    /// - Entity disambiguation
    /// - Result reordering based on graph structure
    /// </remarks>
    Task<TwoTierSearchResults> EnhanceSearchResultsAsync(
        TwoTierSearchResults baselineResults,
        float[] queryEmbedding,
        CancellationToken ct = default);

    /// <summary>
    /// Whether this enhancer should be applied.
    /// Allows conditional activation based on configuration or system state.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Name of this enhancer for logging and debugging.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Optional service for indexing knowledge graph relationships during document processing.
/// Implements post-processing hook pattern - can be registered or omitted without affecting core indexing.
/// 
/// This interface is reserved for future Knowledge Graph integration.
/// When KG is implemented, this service can be registered to extract and index graph relationships
/// without changing IKnowledgeDocumentProcessor.
/// </summary>
public interface IIndexEnhancer
{
    /// <summary>
    /// Optional post-processing hook called AFTER a document has been indexed in Tier 1 and Tier 2.
    /// This method allows KG service to extract relationships and index them.
    /// If this enhancer is not registered, indexing proceeds with standard vector data only.
    /// </summary>
    /// <param name="docId">Document ID that was indexed.</param>
    /// <param name="summaryText">The generated summary text (Tier 1).</param>
    /// <param name="chunks">The generated chunks (Tier 2).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Completed task on success.</returns>
    /// <remarks>
    /// Implementation MUST:
    /// - Not throw exceptions (use logging instead)
    /// - Use try-catch to prevent failures from cascading
    /// - Complete indexing in reasonable time (should not exceed document processing time by >50%)
    /// 
    /// This interface enables KG features like:
    /// - Entity extraction and linking
    /// - Relationship graph construction
    /// - Semantic clustering
    /// - Knowledge hierarchy building
    /// </remarks>
    Task EnhanceIndexAsync(
        string docId,
        string summaryText,
        IReadOnlyList<string> chunks,
        CancellationToken ct = default);

    /// <summary>
    /// Whether this enhancer should be applied.
    /// Allows conditional activation based on configuration or system state.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Name of this enhancer for logging and debugging.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Marker interface for Knowledge Graph-specific query parameters.
/// Reserved for future extension to support graph-aware search queries.
/// 
/// When KG is implemented, this interface can be extended with:
/// - Graph traversal depth
/// - Relationship type filters
/// - Entity type constraints
/// - Semantic clustering parameters
/// </summary>
public interface IKnowledgeGraphQuery
{
    // Reserved for future graph-specific query parameters
    // Example (future):
    // - int? GraphTraversalDepth { get; set; }
    // - string[]? AllowedRelationshipTypes { get; set; }
    // - string[]? EntityTypeFilters { get; set; }
}
