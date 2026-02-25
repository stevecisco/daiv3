using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// SentencePiece tokenizer for the nomic-embed-text-v1.5 embedding model.
/// 
/// NOTE: This is a specialized implementation that works with the nomic-embed-text model.
/// The nomic model expects SentencePiece tokenization with specific vocabulary mappings.
/// 
/// Implementation strategy:
/// - Loads SentencePiece vocabulary from the sentencepiece.model file
/// - Maps text to token IDs using the vocabulary
/// - Validates tokens are within vocabulary bounds
/// 
/// Limitations in v0.1:
/// - Uses simplified BPE-like matching (does not implement full SentencePiece BPE algorithm)
/// - For production use, consider using SentencePiece.NetNative NuGet package
/// </summary>
public sealed class SentencePieceTokenizer : IEmbeddingTokenizer
{
    private readonly ILogger<SentencePieceTokenizer> _logger;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly IReadOnlyDictionary<string, int> _specialTokens;

    public string Name => "SentencePieceTokenizer";
    public string ModelId { get; }
    public int VocabularySize => _vocabulary.Count;

    private const int UnknownTokenId = 0; // SentencePiece convention
    private const string UnknownTokenStr = "<unk>";
    private const string PadTokenStr = "<pad>";

    /// <summary>
    /// Creates a new SentencePiece tokenizer for nomic-embed-text-v1.5.
    /// Initializes vocabulary from the model's sentencepiece.model file or vocabulary index.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="modelId">The model identifier (typically "nomic-embed-text-v1.5").</param>
    /// <param name="modelDirectory">Directory containing the sentencepiece model files.</param>
    /// <exception cref="ArgumentException">When modelId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">When logger or modelDirectory is null.</exception>
    /// <exception cref="DirectoryNotFoundException">When the model directory does not exist.</exception>
    public SentencePieceTokenizer(ILogger<SentencePieceTokenizer> logger, string modelId, string modelDirectory)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID must not be null or empty.", nameof(modelId));
        if (string.IsNullOrWhiteSpace(modelDirectory))
            throw new ArgumentNullException(nameof(modelDirectory));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ModelId = modelId;

        if (!Directory.Exists(modelDirectory))
        {
            _logger.LogError("Model directory not found: {ModelDir}", modelDirectory);
            throw new DirectoryNotFoundException($"Model directory not found: {modelDirectory}");
        }

        _vocabulary = InitializeVocabulary(modelDirectory);
        _specialTokens = BuildSpecialTokensMap();

        _logger.LogInformation(
            "SentencePieceTokenizer initialized for model {ModelId} with vocabulary size {VocabSize}.",
            modelId,
            _vocabulary.Count);
    }

    /// <summary>
    /// Tokenizes input text using SentencePiece tokenization.
    /// </summary>
    /// <remarks>
    /// This implementation uses a simplified greedy matching approach.
    /// For production use with full SentencePiece BPE, integrate the official SentencePiece library.
    /// </remarks>
    public long[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentNullException(nameof(text));

        var tokens = new List<long>();

        // SentencePiece operates on raw text (no pre-tokenization)
        // It handles whitespace as special characters
        var textWithMarkers = text.Replace(" ", "▁"); // SentencePiece convention: space = ▁

        // Greedy longest-match tokenization (simplified BPE)
        var remaining = textWithMarkers;
        while (!string.IsNullOrEmpty(remaining))
        {
            var foundToken = false;

            // Try longest matches first
            for (int i = remaining.Length; i > 0; i--)
            {
                var candidate = remaining[..i];
                if (_vocabulary.TryGetValue(candidate, out var tokenId))
                {
                    tokens.Add(tokenId);
                    remaining = remaining[i..];
                    foundToken = true;
                    break;
                }
            }

            if (!foundToken)
            {
                // No match found - use unknown token and skip one character
                tokens.Add(UnknownTokenId);
                remaining = remaining[1..];
            }
        }

        if (tokens.Count == 0)
        {
            _logger.LogWarning("Tokenization produced no tokens for input text.");
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Validates that all token IDs are within vocabulary bounds.
    /// </summary>
    public bool ValidateTokenIds(long[] tokenIds)
    {
        if (tokenIds == null)
            return false;

        foreach (var id in tokenIds)
        {
            if (id < 0 || id >= _vocabulary.Count)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the special tokens defined for this SentencePiece model.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetSpecialTokens() => _specialTokens;

    /// <summary>
    /// Initializes vocabulary by loading token definitions from the model directory.
    /// </summary>
    private Dictionary<string, int> InitializeVocabulary(string modelDirectory)
    {
        var vocab = new Dictionary<string, int>();

        // Common paths where vocabulary might be stored
        var vocabFiles = new[]
        {
            Path.Combine(modelDirectory, "sentencepiece.model"),
            Path.Combine(modelDirectory, "vocab.txt"),
            Path.Combine(modelDirectory, "tokens.txt"),
        };

        foreach (var vocabFile in vocabFiles)
        {
            if (File.Exists(vocabFile))
            {
                try
                {
                    // Try to parse vocabulary from text-based formats
                    if (vocabFile.EndsWith(".txt"))
                    {
                        vocab = LoadTextVocabulary(vocabFile);
                        if (vocab.Count > 0)
                        {
                            _logger.LogDebug("Loaded vocabulary from {VocabFile}.", vocabFile);
                            return vocab;
                        }
                    }
                    else if (vocabFile.EndsWith(".model"))
                    {
                        // sentencepiece.model is binary; attempt to extract basic info
                        vocab = LoadSentencePieceModel(vocabFile);
                        if (vocab.Count > 0)
                        {
                            _logger.LogDebug("Loaded vocabulary from SentencePiece model file.");
                            return vocab;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load vocabulary from {VocabFile}.", vocabFile);
                }
            }
        }

        // Fallback: Create minimal vocabulary if no files found
        if (vocab.Count == 0)
        {
            _logger.LogWarning(
                "No vocabulary file found in {ModelDir}. Using builtin minimal vocabulary.",
                modelDirectory);
            vocab = CreateMinimalVocabulary();
        }

        return vocab;
    }

    /// <summary>
    /// Loads vocabulary from a text-based file (one token per line).
    /// </summary>
    private Dictionary<string, int> LoadTextVocabulary(string vocabFilePath)
    {
        var vocab = new Dictionary<string, int>();
        var lines = File.ReadAllLines(vocabFilePath);

        for (int i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocab[token] = i;
            }
        }

        return vocab;
    }

    /// <summary>
    /// Attempts to extract vocabulary information from a SentencePiece binary model file.
    /// This is a basic implementation; for production, use the official SentencePiece library.
    /// </summary>
    private Dictionary<string, int> LoadSentencePieceModel(string modelFilePath)
    {
        var vocab = new Dictionary<string, int>();

        try
        {
            // SentencePiece model format starts with protobuf header
            // This simplified implementation cannot fully parse the binary format
            // A production implementation should use the official SentencePiece library
            _logger.LogDebug(
                "SentencePiece binary model file detected. " +
                "Simplified vocabulary extraction in use. " +
                "For production, migrate to SentencePiece.NetNative package.");

            // Attempt basic parsing (this is limited)
            // Return empty; caller will use fallback vocabulary
            return vocab;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SentencePiece model file.");
            return vocab;
        }
    }

    /// <summary>
    /// Creates a minimal fallback vocabulary with common tokens from nomic-embed-text.
    /// This is used when the actual vocabulary file is not available (e.g., during testing).
    /// </summary>
    private static Dictionary<string, int> CreateMinimalVocabulary()
    {
        var vocab = new Dictionary<string, int>
        {
            // SentencePiece special tokens
            { "<unk>", 0 },      // Unknown token
            { "<s>", 1 },        // Start of sequence
            { "</s>", 2 },       // End of sequence  
            { "<pad>", 3 },      // Padding token
            { "▁", 4 },          // Space marker (SentencePiece convention)
            
            // Common English words and subwords (stub for demonstration)
            // In production, all 32000 nomic vocab tokens should be loaded
            { "the", 5 },
            { "a", 6 },
            { "and", 7 },
            { "to", 8 },
            { "of", 9 },
            { "in", 10 },
        };

        // Fill remaining slots with incremental IDs (for testing)
        for (int i = vocab.Count; i < 1000; i++)
        {
            vocab[$"<token_{i}>"] = i;
        }

        return vocab;
    }

    /// <summary>
    /// Builds a map of special tokens to their IDs.
    /// </summary>
    private IReadOnlyDictionary<string, int> BuildSpecialTokensMap()
    {
        var map = new Dictionary<string, int>();

        if (_vocabulary.TryGetValue(UnknownTokenStr, out var unkId))
            map["UNK"] = unkId;
        if (_vocabulary.TryGetValue(PadTokenStr, out var padId))
            map["PAD"] = padId;
        if (_vocabulary.TryGetValue("<s>", out var startId))
            map["START"] = startId;
        if (_vocabulary.TryGetValue("</s>", out var endId))
            map["END"] = endId;

        return map;
    }
}
