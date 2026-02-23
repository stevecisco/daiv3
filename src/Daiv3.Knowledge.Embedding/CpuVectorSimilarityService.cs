using System.Numerics.Tensors;
using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// CPU-based vector similarity implementation using System.Numerics.TensorPrimitives.
/// Provides SIMD-accelerated cosine similarity calculations for semantic search.
/// </summary>
public sealed class CpuVectorSimilarityService : IVectorSimilarityService
{
    private readonly ILogger<CpuVectorSimilarityService> _logger;

    public CpuVectorSimilarityService(ILogger<CpuVectorSimilarityService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException(
                $"Vector dimensions must match. vector1: {vector1.Length}, vector2: {vector2.Length}");
        }

        if (vector1.Length == 0)
        {
            throw new ArgumentException("Vectors cannot be empty.");
        }

        try
        {
            // Compute dot product using SIMD acceleration
            float dotProduct = TensorPrimitives.Dot(vector1, vector2);

            // Compute magnitudes using SIMD acceleration
            float magnitude1 = MathF.Sqrt(TensorPrimitives.Dot(vector1, vector1));
            float magnitude2 = MathF.Sqrt(TensorPrimitives.Dot(vector2, vector2));

            // Handle zero-magnitude vectors
            if (magnitude1 == 0f || magnitude2 == 0f)
            {
                _logger.LogWarning("Zero-magnitude vector detected in cosine similarity calculation.");
                return 0f;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing cosine similarity for vectors of dimension {Dimension}", vector1.Length);
            throw;
        }
    }

    /// <inheritdoc />
    public void BatchCosineSimilarity(
        ReadOnlySpan<float> queryVector,
        ReadOnlySpan<float> targetVectors,
        int vectorCount,
        int dimensions,
        Span<float> results)
    {
        if (queryVector.Length != dimensions)
        {
            throw new ArgumentException(
                $"Query vector dimension ({queryVector.Length}) does not match expected dimensions ({dimensions}).");
        }

        if (targetVectors.Length != vectorCount * dimensions)
        {
            throw new ArgumentException(
                $"Target vectors array size ({targetVectors.Length}) does not match vectorCount ({vectorCount}) * dimensions ({dimensions}).");
        }

        if (results.Length < vectorCount)
        {
            throw new ArgumentException(
                $"Results array length ({results.Length}) is insufficient for {vectorCount} vectors.");
        }

        if (dimensions == 0 || vectorCount == 0)
        {
            throw new ArgumentException("Vector count and dimensions must be greater than zero.");
        }

        try
        {
            // Pre-compute query vector magnitude once
            float queryMagnitude = MathF.Sqrt(TensorPrimitives.Dot(queryVector, queryVector));

            if (queryMagnitude == 0f)
            {
                _logger.LogWarning("Zero-magnitude query vector detected in batch cosine similarity.");
                results.Fill(0f);
                return;
            }

            // Process each target vector
            for (int i = 0; i < vectorCount; i++)
            {
                int offset = i * dimensions;
                ReadOnlySpan<float> targetVector = targetVectors.Slice(offset, dimensions);

                // Compute dot product
                float dotProduct = TensorPrimitives.Dot(queryVector, targetVector);

                // Compute target magnitude
                float targetMagnitude = MathF.Sqrt(TensorPrimitives.Dot(targetVector, targetVector));

                // Handle zero-magnitude target
                if (targetMagnitude == 0f)
                {
                    _logger.LogWarning("Zero-magnitude target vector at index {Index}", i);
                    results[i] = 0f;
                    continue;
                }

                // Store cosine similarity
                results[i] = dotProduct / (queryMagnitude * targetMagnitude);
            }

            _logger.LogDebug(
                "Computed batch cosine similarity: {VectorCount} vectors of dimension {Dimensions}",
                vectorCount, dimensions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error computing batch cosine similarity for {VectorCount} vectors of dimension {Dimensions}",
                vectorCount, dimensions);
            throw;
        }
    }

    /// <inheritdoc />
    public void Normalize(ReadOnlySpan<float> vector, Span<float> normalized)
    {
        if (vector.Length != normalized.Length)
        {
            throw new ArgumentException(
                $"Input and output vector lengths must match. Input: {vector.Length}, Output: {normalized.Length}");
        }

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector cannot be empty.");
        }

        try
        {
            // Compute magnitude
            float magnitude = MathF.Sqrt(TensorPrimitives.Dot(vector, vector));

            if (magnitude == 0f)
            {
                _logger.LogWarning("Cannot normalize zero-magnitude vector.");
                normalized.Fill(0f);
                return;
            }

            // Normalize: divide each element by magnitude
            // Copy to output first, then divide in place
            vector.CopyTo(normalized);
            TensorPrimitives.Divide(normalized, magnitude, normalized);

            _logger.LogDebug("Normalized vector of dimension {Dimension}", vector.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing vector of dimension {Dimension}", vector.Length);
            throw;
        }
    }
}
