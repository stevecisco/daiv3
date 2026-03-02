using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;

namespace Daiv3.Orchestration;

/// <summary>
/// Service for retrieving relevant learnings for agent execution per LM-REQ-005.
/// Implements semantic search using embedding similarity for learning injection into agent prompts.
/// </summary>
public class LearningRetrievalService : ILearningRetrievalService
{
    private readonly ILogger<LearningRetrievalService> _logger;
    private readonly ILearningStorageService _storageService;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IVectorSimilarityService _vectorSimilarity;

    public LearningRetrievalService(
        ILogger<LearningRetrievalService> logger,
        ILearningStorageService storageService,
        IEmbeddingGenerator embeddingGenerator,
        IVectorSimilarityService vectorSimilarity)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _vectorSimilarity = vectorSimilarity ?? throw new ArgumentNullException(nameof(vectorSimilarity));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RetrievedLearning>> RetrieveLearningsAsync(
        LearningRetrievalContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.TaskGoal);

        if (context.MinConfidence < 0.0 || context.MinConfidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "MinConfidence must be between 0.0 and 1.0");
        }

        if (context.MinSimilarity < 0.0 || context.MinSimilarity > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "MinSimilarity must be between 0.0 and 1.0");
        }

        if (context.MaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "MaxResults must be greater than 0");
        }

        if (context.MaxRetrievalTimeMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "MaxRetrievalTimeMs must be greater than 0");
        }

        if (context.SlowRetrievalWarningMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "SlowRetrievalWarningMs must be greater than 0");
        }

        if (context.MaxCandidatesToScore <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "MaxCandidatesToScore must be greater than 0");
        }

        var retrievalStopwatch = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(context.MaxRetrievalTimeMs));
        var retrievalToken = timeoutCts.Token;

        try
        {

            _logger.LogInformation(
                "Retrieving learnings for task: {TaskGoal} (MinConfidence: {MinConfidence}, MinSimilarity: {MinSimilarity}, MaxResults: {MaxResults}, TimeoutMs: {TimeoutMs}, MaxCandidates: {MaxCandidates})",
                context.TaskGoal, context.MinConfidence, context.MinSimilarity, context.MaxResults, context.MaxRetrievalTimeMs, context.MaxCandidatesToScore);

            // Get all learnings with embeddings
            var allLearnings = await _storageService.GetEmbeddedLearningsAsync(retrievalToken).ConfigureAwait(false);

            if (allLearnings.Count == 0)
            {
                _logger.LogInformation("No learnings with embeddings found in storage");
                return Array.Empty<RetrievedLearning>();
            }

            // Filter by status (only active learnings)
            var activeLearnings = allLearnings
                .Where(l => string.Equals(l.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (activeLearnings.Count == 0)
            {
                _logger.LogInformation("No active learnings with embeddings found");
                return Array.Empty<RetrievedLearning>();
            }

            // Filter by scope if specified
            if (!string.IsNullOrWhiteSpace(context.Scope))
            {
                activeLearnings = activeLearnings
                    .Where(l => string.Equals(l.Scope, context.Scope, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogDebug(
                    "Filtered to {Count} learnings with scope '{Scope}'",
                    activeLearnings.Count, context.Scope);
            }

            // Filter by agent if specified (include both agent-specific and global learnings)
            if (!string.IsNullOrWhiteSpace(context.AgentId))
            {
                activeLearnings = activeLearnings
                    .Where(l =>
                        string.IsNullOrWhiteSpace(l.SourceAgent) ||
                        string.Equals(l.SourceAgent, context.AgentId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(l.Scope, "Global", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogDebug(
                    "Filtered to {Count} learnings for agent '{AgentId}' or global scope",
                    activeLearnings.Count, context.AgentId);
            }

            // Filter by confidence threshold
            activeLearnings = activeLearnings
                .Where(l => l.Confidence >= context.MinConfidence)
                .OrderByDescending(l => l.Confidence)
                .ThenByDescending(l => l.UpdatedAt)
                .Take(context.MaxCandidatesToScore)
                .ToList();

            if (activeLearnings.Count == 0)
            {
                _logger.LogInformation(
                    "No learnings meet confidence threshold {MinConfidence}",
                    context.MinConfidence);
                return Array.Empty<RetrievedLearning>();
            }

            _logger.LogDebug(
                "After filtering and candidate cap: {Count} active learnings meet criteria",
                activeLearnings.Count);

            // Generate query embedding for task context
            var queryText = BuildQueryText(context);
            float[] queryEmbedding;

            try
            {
                queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(queryText, retrievalToken).ConfigureAwait(false);

                if (queryEmbedding.Length == 0)
                {
                    _logger.LogWarning("Failed to generate query embedding for task: {TaskGoal}", context.TaskGoal);
                    return Array.Empty<RetrievedLearning>();
                }

                _logger.LogDebug(
                    "Generated query embedding with {Dimensions} dimensions",
                    queryEmbedding.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating query embedding for task: {TaskGoal}", context.TaskGoal);
                return Array.Empty<RetrievedLearning>();
            }

            // Calculate similarity scores
            var scoredLearnings = CalculateSimilarityScores(activeLearnings, queryEmbedding);

            // Filter by minimum similarity threshold
            var relevantLearnings = scoredLearnings
                .Where(sl => sl.Score >= context.MinSimilarity)
                .ToList();

            if (relevantLearnings.Count == 0)
            {
                _logger.LogInformation(
                    "No learnings meet similarity threshold {MinSimilarity}",
                    context.MinSimilarity);
                return Array.Empty<RetrievedLearning>();
            }

            // Sort by similarity score (descending) and take top N
            var topLearnings = relevantLearnings
                .OrderByDescending(sl => sl.Score)
                .Take(context.MaxResults)
                .Select((sl, index) => new RetrievedLearning
                {
                    Learning = sl.Learning,
                    SimilarityScore = sl.Score,
                    Rank = index + 1
                })
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} relevant learnings for task (top similarity: {TopScore:F3})",
                topLearnings.Count,
                topLearnings.Count > 0 ? topLearnings[0].SimilarityScore : 0.0);

            // Update TimesApplied counter for retrieved learnings asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                foreach (var retrieved in topLearnings)
                {
                    try
                    {
                        retrieved.Learning.TimesApplied++;
                        await _storageService.UpdateLearningAsync(retrieved.Learning, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to update TimesApplied for learning {LearningId}",
                            retrieved.Learning.LearningId);
                    }
                }
            }, CancellationToken.None);

            return topLearnings;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Learning retrieval timed out after {ElapsedMs}ms (budget: {BudgetMs}ms). Returning empty result to avoid blocking foreground execution.",
                retrievalStopwatch.ElapsedMilliseconds,
                context.MaxRetrievalTimeMs);

            return Array.Empty<RetrievedLearning>();
        }
        finally
        {
            retrievalStopwatch.Stop();

            if (retrievalStopwatch.ElapsedMilliseconds >= context.SlowRetrievalWarningMs)
            {
                _logger.LogWarning(
                    "Learning retrieval latency {ElapsedMs}ms exceeded warning threshold {WarningMs}ms",
                    retrievalStopwatch.ElapsedMilliseconds,
                    context.SlowRetrievalWarningMs);
            }
            else
            {
                _logger.LogDebug(
                    "Learning retrieval completed in {ElapsedMs}ms",
                    retrievalStopwatch.ElapsedMilliseconds);
            }
        }
    }

    /// <summary>
    /// Builds the query text for embedding generation from context.
    /// </summary>
    private static string BuildQueryText(LearningRetrievalContext context)
    {
        var queryText = context.TaskGoal;

        if (context.AdditionalContext != null && context.AdditionalContext.Count > 0)
        {
            var contextParts = context.AdditionalContext
                .Select(kvp => $"{kvp.Key}: {kvp.Value}")
                .ToList();

            queryText = $"{queryText}\n{string.Join("\n", contextParts)}";
        }

        return queryText;
    }

    /// <summary>
    /// Calculates similarity scores between query embedding and all learning embeddings.
    /// Uses batch cosine similarity for efficient computation.
    /// </summary>
    private List<(Learning Learning, double Score)> CalculateSimilarityScores(
        List<Learning> learnings,
        float[] queryEmbedding)
    {
        var queryDimensions = queryEmbedding.Length;

        // Group learnings by embedding dimensions for batch processing
        var dimensionGroups = learnings
            .GroupBy(l => l.EmbeddingDimensions ?? 0)
            .Where(g => g.Key == queryDimensions) // Only process learnings with matching dimensions
            .ToList();

        var results = new List<(Learning Learning, double Score)>();

        foreach (var group in dimensionGroups)
        {
            var groupLearnings = group.ToList();
            var dimensions = group.Key;

            if (dimensions == 0 || groupLearnings.Count == 0)
            {
                continue;
            }

            // Prepare batch target vectors
            var vectorCount = groupLearnings.Count;
            var targetVectorsArray = ArrayPool<float>.Shared.Rent(vectorCount * dimensions);
            var similarityScores = ArrayPool<float>.Shared.Rent(vectorCount);

            try
            {
                // Copy learning embeddings into flat array
                for (int i = 0; i < groupLearnings.Count; i++)
                {
                    var learning = groupLearnings[i];
                    if (learning.EmbeddingBlob == null || learning.EmbeddingBlob.Length == 0)
                    {
                        continue;
                    }

                    var learningEmbedding = ConvertFromByteArray(learning.EmbeddingBlob);
                    
                    if (learningEmbedding.Length != dimensions)
                    {
                        _logger.LogWarning(
                            "Learning {LearningId} has mismatched embedding dimensions: expected {Expected}, got {Actual}",
                            learning.LearningId, dimensions, learningEmbedding.Length);
                        continue;
                    }

                    Array.Copy(learningEmbedding, 0, targetVectorsArray, i * dimensions, dimensions);
                }

                // Calculate batch similarity scores
                _vectorSimilarity.BatchCosineSimilarity(
                    queryEmbedding,
                    targetVectorsArray.AsSpan(0, vectorCount * dimensions),
                    vectorCount,
                    dimensions,
                    similarityScores.AsSpan(0, vectorCount));

                // Build results
                for (int i = 0; i < groupLearnings.Count; i++)
                {
                    var score = (double)similarityScores[i];
                    
                    // Clamp score to valid range [0, 1]
                    score = Math.Clamp(score, 0.0, 1.0);
                    
                    results.Add((groupLearnings[i], score));
                }

                _logger.LogDebug(
                    "Calculated similarity scores for {Count} learnings with {Dimensions} dimensions",
                    groupLearnings.Count, dimensions);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(targetVectorsArray);
                ArrayPool<float>.Shared.Return(similarityScores);
            }
        }

        return results;
    }

    /// <summary>
    /// Converts byte array back to float array.
    /// </summary>
    private static float[] ConvertFromByteArray(byte[] bytes)
    {
        var floatCount = bytes.Length / sizeof(float);
        var floats = new float[floatCount];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
