using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Registry for embedding tokenizer implementations.
/// Maps model identifiers to their corresponding tokenizer implementations.
/// </summary>
public sealed class EmbeddingTokenizerRegistry
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Func<IEmbeddingTokenizer>> _tokenizers = new();

    public EmbeddingTokenizerRegistry(ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
            throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger("EmbeddingTokenizerRegistry");
    }

    /// <summary>
    /// Registers a tokenizer factory for a specific model ID.
    /// </summary>
    /// <param name="modelId">The embedding model identifier (e.g., "nomic-embed-text-v1.5", "all-MiniLM-L6-v2").</param>
    /// <param name="tokenizerfactory">Factory function that creates a new tokenizer instance.</param>
    public void Register(string modelId, Func<IEmbeddingTokenizer> tokenizerfactory)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID must not be null or empty.", nameof(modelId));
        if (tokenizerfactory == null)
            throw new ArgumentNullException(nameof(tokenizerfactory));

        _tokenizers[modelId] = tokenizerfactory;
        _logger.LogDebug("Registered tokenizer for model {ModelId}.", modelId);
    }

    /// <summary>
    /// Gets a tokenizer instance for the specified model ID.
    /// </summary>
    /// <param name="modelId">The embedding model identifier.</param>
    /// <returns>A tokenizer instance configured for the specified model.</returns>
    /// <exception cref="KeyNotFoundException">When no tokenizer is registered for the model ID.</exception>
    public IEmbeddingTokenizer GetTokenizer(string modelId)
    {
        if (!_tokenizers.TryGetValue(modelId, out var factory))
        {
            _logger.LogError("No tokenizer registered for model {ModelId}.", modelId);
            throw new KeyNotFoundException($"No tokenizer registered for embedding model '{modelId}'.");
        }

        var tokenizer = factory();
        _logger.LogDebug("Retrieved tokenizer {TokenizerName} for model {ModelId}.", tokenizer.Name, modelId);
        return tokenizer;
    }

    /// <summary>
    /// Checks if a tokenizer is registered for the specified model ID.
    /// </summary>
    public bool IsRegistered(string modelId) => !string.IsNullOrWhiteSpace(modelId) && _tokenizers.ContainsKey(modelId);

    /// <summary>
    /// Gets all registered model IDs.
    /// </summary>
    public IEnumerable<string> RegisteredModels => _tokenizers.Keys;
}
