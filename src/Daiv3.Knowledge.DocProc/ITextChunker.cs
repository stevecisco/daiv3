namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Splits text into token-based chunks.
/// </summary>
public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text);
}
