using ReverseMarkdown;

namespace Daiv3.Knowledge.DocProc;

/// <summary>
/// Converts HTML content to Markdown using ReverseMarkdown library.
/// Used for normalizing HTML documents for indexing.
/// </summary>
public class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly ReverseMarkdown.Converter _converter;

    public HtmlToMarkdownConverter()
    {
        var config = new ReverseMarkdown.Config
        {
            // Preserve GitHub Flavored Markdown features
            GithubFlavored = true
        };

        _converter = new ReverseMarkdown.Converter(config);
    }

    /// <summary>
    /// Converts HTML string to Markdown string.
    /// </summary>
    /// <param name="html">HTML content to convert</param>
    /// <returns>Markdown representation of the HTML content</returns>
    /// <exception cref="ArgumentNullException">Thrown when html is null</exception>
    public string ConvertHtmlToMarkdown(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        try
        {
            var markdown = _converter.Convert(html);
            
            // Clean up excess whitespace that ReverseMarkdown may introduce
            var normalized = NormalizeWhitespace(markdown);
            return normalized;
        }
        catch (Exception ex)
        {
            // If conversion fails, return original text stripped of obvious HTML
            throw new InvalidOperationException("Failed to convert HTML to Markdown", ex);
        }
    }

    /// <summary>
    /// Normalizes excessive whitespace in the converted Markdown.
    /// </summary>
    private static string NormalizeWhitespace(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return markdown;
        }

        // Replace multiple blank lines with exactly two (for paragraph separation)
        var singleSpaceLines = System.Text.RegularExpressions.Regex.Replace(
            markdown, 
            @"\n\n\n+", 
            "\n\n");

        // Trim leading/trailing whitespace
        return singleSpaceLines.Trim();
    }
}
