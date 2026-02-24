namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Converts HTML content to Markdown format.
/// Used for normalizing HTML documents for indexing in the knowledge pipeline.
/// </summary>
public interface IHtmlToMarkdownConverter
{
    /// <summary>
    /// Converts HTML string to Markdown string.
    /// </summary>
    /// <param name="html">HTML content to convert</param>
    /// <returns>Markdown representation of the HTML content</returns>
    /// <exception cref="ArgumentNullException">Thrown when html is null</exception>
    string ConvertHtmlToMarkdown(string html);
}
