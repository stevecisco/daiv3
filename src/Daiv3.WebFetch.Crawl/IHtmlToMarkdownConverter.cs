namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Converts HTML content to Markdown format while stripping styling, navigation, and ads.
/// </summary>
public interface IHtmlToMarkdownConverter
{
    /// <summary>
    /// Converts HTML content to Markdown.
    /// </summary>
    /// <param name="htmlContent">The HTML content to convert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns the converted Markdown content.</returns>
    /// <exception cref="ArgumentException">Thrown when htmlContent is null or empty.</exception>
    /// <exception cref="HtmlToMarkdownConversionException">Thrown when conversion fails.</exception>
    Task<string> ConvertAsync(string htmlContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts HTML content to Markdown with detailed conversion result information.
    /// </summary>
    /// <param name="htmlContent">The HTML content to convert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns detailed conversion result.</returns>
    /// <exception cref="ArgumentException">Thrown when htmlContent is null or empty.</exception>
    /// <exception cref="HtmlToMarkdownConversionException">Thrown when conversion fails.</exception>
    Task<HtmlToMarkdownResult> ConvertWithDetailsAsync(string htmlContent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of an HTML to Markdown conversion operation.
/// </summary>
public class HtmlToMarkdownResult
{
    /// <summary>
    /// Gets the converted Markdown content.
    /// </summary>
    public string MarkdownContent { get; private set; }

    /// <summary>
    /// Gets the number of elements stripped (typically navigation, ads, styles).
    /// </summary>
    public int ElementsStripped { get; private set; }

    /// <summary>
    /// Gets the number of links extracted from the HTML.
    /// </summary>
    public int LinksExtracted { get; private set; }

    /// <summary>
    /// Gets the number of images referenced in the HTML.
    /// </summary>
    public int ImagesReferenced { get; private set; }

    /// <summary>
    /// Gets the number of code blocks found in the HTML.
    /// </summary>
    public int CodeBlocksFound { get; private set; }

    /// <summary>
    /// Gets the original HTML content length in characters.
    /// </summary>
    public int OriginalContentLength { get; private set; }

    /// <summary>
    /// Gets the resulting Markdown content length in characters.
    /// </summary>
    public int MarkdownContentLength { get; private set; }

    /// <summary>
    /// Gets the conversion timestamp (UTC).
    /// </summary>
    public DateTime ConvertedAtUtc { get; private set; }

    /// <summary>
    /// Initializes a new instance of the HtmlToMarkdownResult class.
    /// </summary>
    public HtmlToMarkdownResult(
        string markdownContent,
        int elementsStripped = 0,
        int linksExtracted = 0,
        int imagesReferenced = 0,
        int codeBlocksFound = 0,
        int originalContentLength = 0)
    {
        MarkdownContent = markdownContent ?? throw new ArgumentNullException(nameof(markdownContent));
        ElementsStripped = elementsStripped;
        LinksExtracted = linksExtracted;
        ImagesReferenced = imagesReferenced;
        CodeBlocksFound = codeBlocksFound;
        OriginalContentLength = originalContentLength;
        MarkdownContentLength = markdownContent.Length;
        ConvertedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when HTML to Markdown conversion fails.
/// </summary>
public class HtmlToMarkdownConversionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the HtmlToMarkdownConversionException class.
    /// </summary>
    public HtmlToMarkdownConversionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the HtmlToMarkdownConversionException class.
    /// </summary>
    public HtmlToMarkdownConversionException(string message, Exception innerException)
        : base(message, innerException) { }
}
