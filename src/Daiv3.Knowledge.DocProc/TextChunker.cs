using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Daiv3.Knowledge.DocProc;

public sealed class TextChunker : ITextChunker
{
    private readonly ILogger<TextChunker> _logger;
    private readonly ITokenizerProvider _tokenizerProvider;
    private readonly TokenizationOptions _options;

    public TextChunker(
        ILogger<TextChunker> logger,
        ITokenizerProvider tokenizerProvider,
        IOptions<TokenizationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenizerProvider = tokenizerProvider ?? throw new ArgumentNullException(nameof(tokenizerProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<TextChunk>();
        }

        _options.Validate();

        var tokenizer = _tokenizerProvider.GetTokenizer();
        var tokens = tokenizer.EncodeToTokens(
            text,
            out _,
            _options.ConsiderPreTokenization,
            _options.ConsiderNormalization);
        if (tokens.Count == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        var maxTokens = _options.MaxTokensPerChunk;
        var overlap = _options.OverlapTokens;
        var startTokenIndex = 0;

        while (startTokenIndex < tokens.Count)
        {
            var endTokenIndex = Math.Min(startTokenIndex + maxTokens, tokens.Count) - 1;
            var startOffset = tokens[startTokenIndex].Offset.Item1;
            var endOffset = tokens[endTokenIndex].Offset.Item1 + tokens[endTokenIndex].Offset.Item2;

            if (endOffset < startOffset)
            {
                _logger.LogWarning("Tokenizer offsets were invalid for chunk starting at token {TokenIndex}.", startTokenIndex);
                break;
            }

            var length = endOffset - startOffset;
            if (length > 0)
            {
                var chunkText = text.Substring(startOffset, length);
                var tokenCount = endTokenIndex - startTokenIndex + 1;
                chunks.Add(new TextChunk(chunkText, startOffset, length, tokenCount));
            }

            if (endTokenIndex >= tokens.Count - 1)
            {
                break;
            }

            var nextStart = endTokenIndex - overlap + 1;
            if (nextStart <= startTokenIndex)
            {
                nextStart = startTokenIndex + 1;
            }

            startTokenIndex = nextStart;
        }

        _logger.LogDebug("Chunked text into {ChunkCount} chunks using {EncodingName}.",
            chunks.Count,
            _options.EncodingName);

        return chunks;
    }
}
