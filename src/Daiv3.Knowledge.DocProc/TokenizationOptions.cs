namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Configuration for tokenization and chunking.
/// </summary>
public sealed class TokenizationOptions
{
    public string EncodingName { get; set; } = "r50k_base";

    public int MaxTokensPerChunk { get; set; } = 400;

    public int OverlapTokens { get; set; } = 50;

    public bool ConsiderPreTokenization { get; set; } = true;

    public bool ConsiderNormalization { get; set; } = false;

    public bool UseLowerCaseNormalizer { get; set; }

    public IReadOnlyDictionary<string, int>? ExtraSpecialTokens { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EncodingName))
        {
            throw new InvalidOperationException("Tokenizer encoding name must be configured.");
        }

        if (MaxTokensPerChunk <= 0)
        {
            throw new InvalidOperationException("MaxTokensPerChunk must be greater than zero.");
        }

        if (OverlapTokens < 0)
        {
            throw new InvalidOperationException("OverlapTokens cannot be negative.");
        }

        if (OverlapTokens >= MaxTokensPerChunk)
        {
            throw new InvalidOperationException("OverlapTokens must be less than MaxTokensPerChunk.");
        }
    }
}
