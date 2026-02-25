namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Interface for model-specific tokenizers used during embedding generation.
/// Each embedding model requires its own tokenization strategy (e.g., SentencePiece for nomic, WordPiece for BERT).
/// </summary>
public interface IEmbeddingTokenizer
{
    /// <summary>
    /// Gets the human-readable name of the tokenizer (e.g., "SentencePieceTokenizer", "BertTokenizer").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the model identifier this tokenizer is configured for (e.g., "nomic-embed-text-v1.5", "all-MiniLM-L6-v2").
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// Gets the vocabulary size (number of tokens in the model's vocabulary).
    /// </summary>
    int VocabularySize { get; }

    /// <summary>
    /// Tokenizes the input text into token IDs.
    /// Token IDs must be within [0, VocabularySize).
    /// </summary>
    /// <param name="text">The input text to tokenize.</param>
    /// <returns>Array of token IDs.</returns>
    /// <exception cref="ArgumentNullException">When text is null.</exception>
    /// <exception cref="InvalidOperationException">When tokenization fails or produces invalid token IDs.</exception>
    long[] Tokenize(string text);

    /// <summary>
    /// Validates that all token IDs are within the vocabulary bounds [0, VocabularySize).
    /// </summary>
    /// <param name="tokenIds">Token IDs to validate.</param>
    /// <returns>True if all token IDs are valid; false otherwise.</returns>
    bool ValidateTokenIds(long[] tokenIds);

    /// <summary>
    /// Gets the special token IDs used by this tokenizer (e.g., [PAD], [UNK], [CLS], [SEP]).
    /// Returns a read-only dictionary mapping special token names to their IDs.
    /// </summary>
    /// <returns>Dictionary of special tokens; empty if none defined.</returns>
    IReadOnlyDictionary<string, int> GetSpecialTokens();
}
