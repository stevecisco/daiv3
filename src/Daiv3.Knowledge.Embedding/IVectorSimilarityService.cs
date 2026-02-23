namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Provides vector similarity operations for semantic search.
/// </summary>
public interface IVectorSimilarityService
{
    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// </summary>
    /// <param name="vector1">First vector.</param>
    /// <param name="vector2">Second vector.</param>
    /// <returns>Cosine similarity score in range [-1, 1].</returns>
    /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
    float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2);

    /// <summary>
    /// Computes cosine similarity between a query vector and multiple target vectors in batch.
    /// This is optimized for Tier 1 search where a single query is compared against all topic embeddings.
    /// </summary>
    /// <param name="queryVector">The query vector.</param>
    /// <param name="targetVectors">Flattened array of target vectors. Length must be vectorCount * dimensions.</param>
    /// <param name="vectorCount">Number of vectors in the target array.</param>
    /// <param name="dimensions">Dimensionality of each vector.</param>
    /// <param name="results">Output array for similarity scores. Must have length >= vectorCount.</param>
    /// <exception cref="ArgumentException">Thrown when dimensions don't match or arrays are incorrectly sized.</exception>
    void BatchCosineSimilarity(
        ReadOnlySpan<float> queryVector,
        ReadOnlySpan<float> targetVectors,
        int vectorCount,
        int dimensions,
        Span<float> results);

    /// <summary>
    /// Normalizes a vector to unit length (L2 normalization).
    /// </summary>
    /// <param name="vector">Input vector.</param>
    /// <param name="normalized">Output normalized vector. Must have same length as input.</param>
    void Normalize(ReadOnlySpan<float> vector, Span<float> normalized);
}
