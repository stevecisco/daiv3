namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Generates embeddings for input text.
/// </summary>
public interface IEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
