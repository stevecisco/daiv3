using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// WordPiece tokenizer for BERT-based embedding models (e.g., all-MiniLM-L6-v2).
/// Implements the WordPiece tokenization algorithm used by BERT and related models.
/// </summary>
public sealed class BertWordPieceTokenizer : IEmbeddingTokenizer
{
    private readonly ILogger<BertWordPieceTokenizer> _logger;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly IReadOnlyDictionary<string, int> _specialTokens;

    public string Name => "BertWordPieceTokenizer";
    public string ModelId { get; }
    public int VocabularySize => _vocabulary.Count;

    private const string UnknownToken = "[UNK]";
    private const string ClassToken = "[CLS]";
    private const string SeparatorToken = "[SEP]";
    private const string PaddingToken = "[PAD]";
    private const string MaskToken = "[MASK]";

    /// <summary>
    /// Creates a new BERT WordPiece tokenizer and loads vocabulary from the specified file.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="modelId">The model identifier this tokenizer is for.</param>
    /// <param name="vocabFilePath">Path to the vocabulary file (one token per line).</param>
    /// <exception cref="ArgumentException">When modelId is null or empty.</exception>
    /// <exception cref="ArgumentNullException">When logger or vocabFilePath is null.</exception>
    /// <exception cref="FileNotFoundException">When the vocabulary file does not exist.</exception>
    /// <exception cref="InvalidOperationException">When vocabulary file is empty or malformed.</exception>
    public BertWordPieceTokenizer(ILogger<BertWordPieceTokenizer> logger, string modelId, string vocabFilePath)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID must not be null or empty.", nameof(modelId));
        if (string.IsNullOrWhiteSpace(vocabFilePath))
            throw new ArgumentNullException(nameof(vocabFilePath));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ModelId = modelId;

        if (!File.Exists(vocabFilePath))
        {
            _logger.LogError("Vocabulary file not found: {VocabPath}", vocabFilePath);
            throw new FileNotFoundException($"Vocabulary file not found: {vocabFilePath}");
        }

        _vocabulary = LoadVocabulary(vocabFilePath);
        _specialTokens = BuildSpecialTokensMap();

        _logger.LogInformation(
            "BertWordPieceTokenizer loaded for model {ModelId} with vocabulary size {VocabSize}.",
            modelId,
            _vocabulary.Count);
    }

    /// <summary>
    /// Tokenizes input text using BERT-style WordPiece tokenization.
    /// </summary>
    public long[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentNullException(nameof(text));

        var tokens = new List<long>();
        
        // Basic whitespace tokenization
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            // Convert to lowercase (BERT standard)
            var lowerWord = word.ToLowerInvariant();

            // Remove punctuation attached to words (simplified approach)
            // In production, use proper BERT preprocessing
            var cleanWord = CleanWord(lowerWord);
            if (string.IsNullOrEmpty(cleanWord))
                continue;

            // WordPiece tokenization: greedily match longest tokens in vocabulary
            var wordTokens = TokenizeWord(cleanWord);
            tokens.AddRange(wordTokens);
        }

        if (tokens.Count == 0)
        {
            _logger.LogWarning("Tokenization produced no tokens for input text: {Text}", text);
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
    /// Returns the special tokens defined for BERT.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetSpecialTokens() => _specialTokens;

    /// <summary>
    /// Loads vocabulary from a text file (one token per line).
    /// </summary>
    private Dictionary<string, int> LoadVocabulary(string vocabFilePath)
    {
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            var lines = File.ReadAllLines(vocabFilePath);
            if (lines.Length == 0)
            {
                _logger.LogError("Vocabulary file is empty: {VocabPath}", vocabFilePath);
                throw new InvalidOperationException("Vocabulary file is empty.");
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var token = lines[i].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    vocab[token] = i; // Vocabulary index == line number
                }
            }

            _logger.LogDebug("Loaded vocabulary with {TokenCount} tokens.", vocab.Count);
            return vocab;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading vocabulary file: {VocabPath}", vocabFilePath);
            throw new InvalidOperationException($"Failed to read vocabulary file: {vocabFilePath}", ex);
        }
    }

    /// <summary>
    /// Builds a map of special tokens (like [UNK], [CLS], etc.) to their IDs.
    /// </summary>
    private IReadOnlyDictionary<string, int> BuildSpecialTokensMap()
    {
        var map = new Dictionary<string, int>();

        // Look up each special token in the vocabulary
        if (_vocabulary.TryGetValue(UnknownToken, out var unkId))
            map["UNK"] = unkId;
        if (_vocabulary.TryGetValue(ClassToken, out var clsId))
            map["CLS"] = clsId;
        if (_vocabulary.TryGetValue(SeparatorToken, out var sepId))
            map["SEP"] = sepId;
        if (_vocabulary.TryGetValue(PaddingToken, out var padId))
            map["PAD"] = padId;
        if (_vocabulary.TryGetValue(MaskToken, out var maskId))
            map["MASK"] = maskId;

        return map;
    }

    /// <summary>
    /// Removes common punctuation from word boundaries (simplified preprocessing).
    /// </summary>
    private static string CleanWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return string.Empty;

        // Remove leading/trailing punctuation
        const string punctuation = ".,!?;:'\"-()[]{}";
        var cleaned = word.Trim(punctuation.ToCharArray());
        return cleaned;
    }

    /// <summary>
    /// Tokenizes a single word using the WordPiece algorithm.
    /// Greedily matches the longest token in vocabulary, prefixing subword pieces with ##.
    /// </summary>
    private List<long> TokenizeWord(string word)
    {
        var tokens = new List<long>();
        var remaining = word;
        bool isFirstToken = true;

        while (!string.IsNullOrEmpty(remaining))
        {
            // Try to find the longest token in the vocabulary
            var foundLongestToken = false;

            for (int i = remaining.Length; i > 0; i--)
            {
                var candidate = remaining[..i];
                var tokensToCheck = isFirstToken ? candidate : $"##{candidate}";

                if (_vocabulary.TryGetValue(tokensToCheck, out var tokenId))
                {
                    tokens.Add(tokenId);
                    remaining = remaining[i..];
                    foundLongestToken = true;
                    isFirstToken = false;
                    break;
                }
            }

            if (!foundLongestToken)
            {
                // Token not found in vocabulary - use [UNK] token
                var unkId = _specialTokens.TryGetValue("UNK", out var id) ? id : 100;
                tokens.Add(unkId);
                remaining = remaining[1..]; // Skip one character and continue
                isFirstToken = false;
            }
        }

        return tokens;
    }
}
