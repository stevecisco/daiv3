using Microsoft.ML.Tokenizers;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Provides tokenizers configured for embedding models.
/// </summary>
public interface IEmbeddingTokenizerProvider
{
    Tokenizer GetTokenizer();
}
