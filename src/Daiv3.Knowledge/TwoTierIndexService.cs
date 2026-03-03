using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Daiv3.Knowledge;

/// <summary>
/// Implementation of two-tier vector search with in-memory Tier 1 caching for performance.
/// </summary>
public class TwoTierIndexService : ITwoTierIndexService
{
    private const int Tier2CandidateDocumentLimit = 3;

    private readonly IVectorStoreService _vectorStore;
    private readonly IVectorSimilarityService _vectorSimilarity;
    private readonly ILogger<TwoTierIndexService> _logger;

    // In-memory cache of Tier 1 embeddings for fast batch similarity computation
    private float[]? _cachedTier1Embeddings;
    private string[]? _cachedTier1DocIds;
    private int _tier1Dimensions;
    private long _lastCacheUpdateTime;

    public TwoTierIndexService(
        IVectorStoreService vectorStore,
        IVectorSimilarityService vectorSimilarity,
        ILogger<TwoTierIndexService> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _vectorSimilarity = vectorSimilarity ?? throw new ArgumentNullException(nameof(vectorSimilarity));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Initializing TwoTierIndexService");

            // Load all Tier 1 embeddings into memory for fast batch similarity
            var topics = await _vectorStore.GetAllTopicIndicesAsync(ct).ConfigureAwait(false);

            if (topics.Count == 0)
            {
                _logger.LogInformation("No topic indices found; index is empty");
                _cachedTier1Embeddings = null;
                _cachedTier1DocIds = null;
                _tier1Dimensions = 0;
                return;
            }

            // Flatten embeddings into contiguous float array for batch operations
            var firstTopic = topics[0];
            _tier1Dimensions = firstTopic.EmbeddingDimensions;

            _cachedTier1Embeddings = new float[topics.Count * _tier1Dimensions];
            _cachedTier1DocIds = new string[topics.Count];

            for (int i = 0; i < topics.Count; i++)
            {
                _cachedTier1DocIds[i] = topics[i].DocId;

                // Convert bytes back to float array
                var embedding = ConvertBytesToEmbedding(topics[i].EmbeddingBlob, _tier1Dimensions);
                Array.Copy(embedding, 0, _cachedTier1Embeddings, i * _tier1Dimensions, _tier1Dimensions);
            }

            _lastCacheUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _logger.LogInformation(
                "Loaded {DocumentCount} topic embeddings ({Dimensions} dims) into memory (~{MemoryMB}MB)",
                topics.Count,
                _tier1Dimensions,
                (_cachedTier1Embeddings.Length * sizeof(float)) / (1024 * 1024));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing TwoTierIndexService");
            throw;
        }
    }

    public async Task<TwoTierSearchResults> SearchAsync(
        float[] queryEmbedding,
        int tier1TopK = 10,
        int tier2TopK = 5,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_cachedTier1Embeddings == null || _cachedTier1DocIds == null)
            {
                _logger.LogWarning("Tier 1 cache not initialized; returning empty results");
                return new TwoTierSearchResults
                {
                    Tier1Results = new List<SearchResult>(),
                    Tier2Results = new List<SearchResult>(),
                    ExecutionTimeMs = 0
                };
            }

            ArgumentNullException.ThrowIfNull(queryEmbedding);

            if (queryEmbedding.Length != _tier1Dimensions)
            {
                throw new ArgumentException(
                    $"Query embedding dimensions ({queryEmbedding.Length}) don't match index dimensions ({_tier1Dimensions})",
                    nameof(queryEmbedding));
            }

            // Tier 1: Batch similarity against all topic embeddings
            var tier1Scores = new float[_cachedTier1DocIds.Length];
            _vectorSimilarity.BatchCosineSimilarity(
                queryEmbedding.AsSpan(),
                _cachedTier1Embeddings,
                _cachedTier1DocIds.Length,
                _tier1Dimensions,
                tier1Scores);

            // Get top K results from Tier 1
            var tier1Results = GetTopResults(tier1Scores, _cachedTier1DocIds, null, tier1TopK, tier: 1);

            _logger.LogDebug("Tier 1 search found {ResultCount} candidates", tier1Results.Count);

            // Tier 2: Search chunks from top Tier 1 candidates
            var tier2Results = new List<SearchResult>();

            foreach (var tier1Result in tier1Results.Take(Tier2CandidateDocumentLimit))
            {
                var chunks = await _vectorStore.GetChunksByDocumentAsync(tier1Result.DocumentId, ct)
                    .ConfigureAwait(false);

                if (chunks.Count == 0)
                    continue;

                var chunkScores = new float[chunks.Count];

                // Search chunks from this document
                var flattenedChunkEmbeddings = FlattenEmbeddings(chunks, out int chunkDimensions);

                if (queryEmbedding.Length != chunkDimensions)
                {
                    _logger.LogWarning(
                        "Skipping Tier 2 search for document {DocId}: query dimensions ({QueryDimensions}) do not match chunk dimensions ({ChunkDimensions})",
                        tier1Result.DocumentId,
                        queryEmbedding.Length,
                        chunkDimensions);
                    continue;
                }

                _vectorSimilarity.BatchCosineSimilarity(
                    queryEmbedding.AsSpan(),
                    flattenedChunkEmbeddings,
                    chunks.Count,
                    chunkDimensions,
                    chunkScores);

                // Get top K results from this document's chunks
                var docChunkResults = GetTopChunkResults(
                    chunkScores,
                    chunks,
                    tier1Result.SourcePath,
                    Math.Min(tier2TopK, chunks.Count));

                tier2Results.AddRange(docChunkResults);
            }

            // Sort Tier 2 results by similarity
            tier2Results = tier2Results.OrderByDescending(r => r.SimilarityScore).ToList();

            stopwatch.Stop();

            _logger.LogDebug(
                "Two-tier search completed: {Tier1Count} Tier1, {Tier2Count} Tier2 in {MS}ms",
                tier1Results.Count,
                tier2Results.Count,
                stopwatch.ElapsedMilliseconds);

            return new TwoTierSearchResults
            {
                Tier1Results = tier1Results,
                Tier2Results = tier2Results,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during two-tier search");
            throw;
        }
    }

    public async Task<IndexStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var docCount = await _vectorStore.GetTopicIndexCountAsync(ct).ConfigureAwait(false);
        var chunkCount = await _vectorStore.GetChunkIndexCountAsync(ct).ConfigureAwait(false);
        var cachedCount = _cachedTier1DocIds?.Length ?? 0;
        var memoryBytes = (_cachedTier1Embeddings?.Length ?? 0) * sizeof(float);

        return new IndexStatistics
        {
            DocumentCount = docCount,
            ChunkCount = chunkCount,
            CachedTopicEmbeddings = cachedCount,
            EstimatedMemoryBytes = memoryBytes
        };
    }

    public async Task ClearCacheAsync()
    {
        _logger.LogInformation("Clearing Tier 1 embedding cache");
        _cachedTier1Embeddings = null;
        _cachedTier1DocIds = null;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Extracts top K results from similarity scores.
    /// </summary>
    private List<SearchResult> GetTopResults(
        float[] scores,
        string[] docIds,
        string[]? sourcePaths,
        int topK,
        int tier)
    {
        var results = new List<(int Index, float Score)>();

        for (int i = 0; i < scores.Length; i++)
        {
            results.Add((i, scores[i]));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select(r => new SearchResult
            {
                DocumentId = docIds[r.Index],
                SimilarityScore = r.Score,
                TierLevel = tier,
                SourcePath = sourcePaths?[r.Index] ?? string.Empty,
                Content = string.Empty
            })
            .ToList();
    }

    /// <summary>
    /// Extracts top chunk results from similarity scores.
    /// </summary>
    private List<SearchResult> GetTopChunkResults(
        float[] scores,
        IReadOnlyList<Daiv3.Persistence.Entities.ChunkIndex> chunks,
        string sourcePath,
        int topK)
    {
        return Enumerable.Range(0, scores.Length)
            .Select(i => new { Index = i, Score = scores[i], Chunk = chunks[i] })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new SearchResult
            {
                DocumentId = x.Chunk.DocId,
                SimilarityScore = x.Score,
                Content = x.Chunk.ChunkText,
                SourcePath = sourcePath,
                TierLevel = 2,
                ChunkOrder = x.Chunk.ChunkOrder
            })
            .ToList();
    }

    /// <summary>
    /// Converts a byte array back to float embedding.
    /// </summary>
    private static float[] ConvertBytesToEmbedding(byte[] bytes, int dimensions)
    {
        var embedding = new float[dimensions];
        System.Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    /// <summary>
    /// Flattens chunk embeddings for batch processing.
    /// </summary>
    private static float[] FlattenEmbeddings(IReadOnlyList<Daiv3.Persistence.Entities.ChunkIndex> chunks, out int dimensions)
    {
        if (chunks.Count == 0)
            throw new ArgumentException("No chunks provided", nameof(chunks));

        dimensions = chunks[0].EmbeddingDimensions;
        var flattened = new float[chunks.Count * dimensions];

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedding = ConvertBytesToEmbedding(chunks[i].EmbeddingBlob, dimensions);
            Array.Copy(embedding, 0, flattened, i * dimensions, dimensions);
        }

        return flattened;
    }
}
