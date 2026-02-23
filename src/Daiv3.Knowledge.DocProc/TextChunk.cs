namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Represents a token-based text chunk extracted from a document.
/// </summary>
public sealed record TextChunk(string Text, int StartOffset, int Length, int TokenCount);
