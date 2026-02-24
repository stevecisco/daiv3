namespace Daiv3.Knowledge.DocProc;

public interface ITextExtractor
{
    Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
}
