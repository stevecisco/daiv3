using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Provides model-specific tokenizers for embedding generation.
/// Maintains a registry of tokenizers and selects the appropriate one based on the active model.
/// </summary>
public sealed class EmbeddingTokenizerProvider : IEmbeddingTokenizerProvider
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EmbeddingOnnxOptions _onnxOptions;
    private readonly EmbeddingTokenizationOptions _tokenizationOptions;
    private readonly EmbeddingTokenizerRegistry _registry;
    private readonly object _lock = new();
    private IEmbeddingTokenizer? _activeTokenizer;

    /// <summary>
    /// Initializes a new instance of the EmbeddingTokenizerProvider.
    /// Builds and registers tokenizers for all supported embedding models.
    /// </summary>
    public EmbeddingTokenizerProvider(
        ILoggerFactory loggerFactory,
        IOptions<EmbeddingOnnxOptions> onnxOptions,
        IOptions<EmbeddingTokenizationOptions> tokenizationOptions)
    {
        if (loggerFactory == null)
            throw new ArgumentNullException(nameof(loggerFactory));
        
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger("EmbeddingTokenizerProvider");
        _onnxOptions = onnxOptions?.Value ?? throw new ArgumentNullException(nameof(onnxOptions));
        _tokenizationOptions = tokenizationOptions?.Value ?? throw new ArgumentNullException(nameof(tokenizationOptions));
        
        _registry = new EmbeddingTokenizerRegistry(loggerFactory);
        RegisterTokenizers();
    }

    /// <summary>
    /// Gets the tokenizer appropriate for the currently configured model.
    /// Dynamically selects between BERT (all-MiniLM-L6-v2) and SentencePiece (nomic-embed-text-v1.5).
    /// </summary>
    public IEmbeddingTokenizer GetEmbeddingTokenizer()
    {
        if (_activeTokenizer != null)
        {
            return _activeTokenizer;
        }

        lock (_lock)
        {
            if (_activeTokenizer != null)
            {
                return _activeTokenizer;
            }

            _onnxOptions.Validate();
            _tokenizationOptions.Validate();

            var modelId = DetermineModelId(_onnxOptions.ModelPath);
            
            _logger.LogDebug("Selecting tokenizer for model {ModelId} from path {ModelPath}.", modelId, _onnxOptions.ModelPath);

            if (!_registry.IsRegistered(modelId))
            {
                _logger.LogError(
                    "No tokenizer registered for model {ModelId}. Registered models: {RegisteredModels}.",
                    modelId,
                    string.Join(", ", _registry.RegisteredModels));
                throw new InvalidOperationException(
                    $"No tokenizer available for embedding model '{modelId}'. " +
                    $"Registered models: {string.Join(", ", _registry.RegisteredModels)}");
            }

            _activeTokenizer = _registry.GetTokenizer(modelId);
            _logger.LogInformation(
                "Tokenizer {TokenizerName} activated for model {ModelId}.",
                _activeTokenizer.Name,
                modelId);

            return _activeTokenizer;
        }
    }

    /// <summary>
    /// Registers all supported embedding model tokenizers.
    /// </summary>
    private void RegisterTokenizers()
    {
        // Tier 1: all-MiniLM-L6-v2 (384 dimensions) - BERT WordPiece tokenizer
        _registry.Register("all-MiniLM-L6-v2", () =>
        {
            var modelDir = GetModelDirectory("all-MiniLM-L6-v2");
            var vocabPath = Path.Combine(modelDir, "vocab.txt");
            var logger = _loggerFactory.CreateLogger<BertWordPieceTokenizer>();
            return new BertWordPieceTokenizer(logger, "all-MiniLM-L6-v2", vocabPath);
        });

        // Tier 2: nomic-embed-text-v1.5 (768 dimensions) - SentencePiece tokenizer
        _registry.Register("nomic-embed-text-v1.5", () =>
        {
            var modelDir = GetModelDirectory("nomic-embed-text-v1.5");
            var logger = _loggerFactory.CreateLogger<SentencePieceTokenizer>();
            return new SentencePieceTokenizer(logger, "nomic-embed-text-v1.5", modelDir);
        });

        _logger.LogDebug("Registered {Count} embedding tokenizers.", _registry.RegisteredModels.Count());
    }

    /// <summary>
    /// Determines the model ID from the configured model path.
    /// Extracts the model name from the path structure: ...embeddings\{modelId}\model.onnx
    /// </summary>
    private static string DetermineModelId(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new InvalidOperationException("Model path must be configured");

        // Path format: %LOCALAPPDATA%\Daiv3\models\embeddings\{modelId}\model.onnx
        var pathSegments = modelPath.Replace('/', '\\').Split('\\');
        
        // Find the segment that contains "embeddings" and get the next segment
        for (int i = 0; i < pathSegments.Length - 1; i++)
        {
            if (pathSegments[i].Equals("embeddings", StringComparison.OrdinalIgnoreCase))
            {
                var modelId = pathSegments[i + 1];
                return modelId;
            }
        }

        // Fallback: if path doesn't match expected format, try to extract from filename
        // This handles cases where only the model.onnx path is provided
        var fileName = Path.GetFileName(modelPath);
        if (fileName.Equals("model.onnx", StringComparison.OrdinalIgnoreCase))
        {
            var parentDir = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                return Path.GetFileName(parentDir);
            }
        }

        throw new InvalidOperationException(
            $"Could not determine model ID from path: {modelPath}. " +
            "Expected format: ...\\embeddings\\{{modelId}}\\model.onnx");
    }

    /// <summary>
    /// Gets the directory for a given model.
    /// </summary>
    private string GetModelDirectory(string modelId)
    {
        var baseDir = Path.GetDirectoryName(_onnxOptions.ModelPath)
            ?? throw new InvalidOperationException($"Invalid model path: {_onnxOptions.ModelPath}");
        return baseDir;
    }
}
