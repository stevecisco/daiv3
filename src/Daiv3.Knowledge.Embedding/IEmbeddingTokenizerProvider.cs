namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Provides tokenizers configured for embedding models.
/// Selects the appropriate tokenizer based on the active embedding model.
/// </summary>
public interface IEmbeddingTokenizerProvider
{
    /// <summary>
    /// Gets a tokenizer instance configured for the current embedding model.
    /// The tokenizer type is determined based on the model path or configured model ID.
    /// </summary>
    /// <returns>An IEmbeddingTokenizer instance appropriate for the active model.</returns>
    /// <exception cref="InvalidOperationException">When no tokenizer is available for the current model.</exception>
    IEmbeddingTokenizer GetEmbeddingTokenizer();
}
