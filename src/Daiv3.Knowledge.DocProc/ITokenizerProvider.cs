using Microsoft.ML.Tokenizers;

namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Provides tokenizers configured for document processing.
/// </summary>
public interface ITokenizerProvider
{
    Tokenizer GetTokenizer();
}
