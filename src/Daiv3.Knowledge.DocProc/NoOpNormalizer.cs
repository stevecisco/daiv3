using Microsoft.ML.Tokenizers;

namespace Daiv3.Knowledge.DocProc;

internal sealed class NoOpNormalizer : Normalizer
{
    public static readonly NoOpNormalizer Instance = new();

    public override string Normalize(string original)
    {
        return original;
    }

    public override string Normalize(ReadOnlySpan<char> original)
    {
        return new string(original);
    }
}
