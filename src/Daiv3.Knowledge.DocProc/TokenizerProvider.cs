using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;

namespace Daiv3.Knowledge.DocProc;

public sealed class TokenizerProvider : ITokenizerProvider
{
    private readonly ILogger<TokenizerProvider> _logger;
    private readonly TokenizationOptions _options;
    private readonly object _lock = new();
    private Tokenizer? _tokenizer;

    public TokenizerProvider(
        ILogger<TokenizerProvider> logger,
        IOptions<TokenizationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Tokenizer GetTokenizer()
    {
        if (_tokenizer != null)
        {
            return _tokenizer;
        }

        lock (_lock)
        {
            if (_tokenizer != null)
            {
                return _tokenizer;
            }

            _options.Validate();

            Normalizer normalizer = _options.UseLowerCaseNormalizer
                ? LowerCaseNormalizer.Instance
                : NoOpNormalizer.Instance;

            var extraTokens = _options.ExtraSpecialTokens ?? new Dictionary<string, int>();

            _tokenizer = TiktokenTokenizer.CreateForEncoding(
                _options.EncodingName,
                extraTokens,
                normalizer);

            _logger.LogInformation("Tokenizer created using encoding {EncodingName}.", _options.EncodingName);

            return _tokenizer;
        }
    }
}
