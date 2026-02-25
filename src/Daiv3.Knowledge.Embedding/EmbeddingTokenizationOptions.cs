namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Configuration for embedding tokenization.
/// </summary>
public sealed class EmbeddingTokenizationOptions
{
    public string EncodingName { get; set; } = "r50k_base";

    public int MaxTokens { get; set; } = 512;

    public bool ConsiderPreTokenization { get; set; } = true;

    public bool ConsiderNormalization { get; set; }

    public bool UseLowerCaseNormalizer { get; set; }

    public IReadOnlyDictionary<string, int>? ExtraSpecialTokens { get; set; }

    public int PadTokenId { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EncodingName))
        {
            throw new InvalidOperationException("Embedding tokenizer encoding name must be configured.");
        }

        if (MaxTokens <= 0)
        {
            throw new InvalidOperationException("MaxTokens must be greater than zero.");
        }
    }
}
