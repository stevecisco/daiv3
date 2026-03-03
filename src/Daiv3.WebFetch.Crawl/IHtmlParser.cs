namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Interface for parsing and extracting content from HTML documents.
/// </summary>
public interface IHtmlParser
{
    /// <summary>
    /// Parses HTML content and returns a parsed document representation.
    /// </summary>
    /// <param name="htmlContent">The HTML content to parse.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the parsed HTML document.</returns>
    Task<IHtmlDocument> ParseAsync(string htmlContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text content from an HTML document.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <returns>The extracted text content.</returns>
    string ExtractText(IHtmlDocument document);

    /// <summary>
    /// Extracts all links from an HTML document.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <returns>A collection of links found in the document.</returns>
    IEnumerable<HtmlLink> ExtractLinks(IHtmlDocument document);

    /// <summary>
    /// Extracts specific elements from an HTML document using CSS selectors.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <param name="selector">The CSS selector for elements to extract.</param>
    /// <returns>A collection of extracted elements.</returns>
    IEnumerable<IHtmlElement> SelectElements(IHtmlDocument document, string selector);
}

/// <summary>
/// Represents a parsed HTML document that can be queried and analyzed.
/// </summary>
public interface IHtmlDocument
{
    /// <summary>
    /// Gets the document title.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Gets the raw HTML content.
    /// </summary>
    string Html { get; }

    /// <summary>
    /// Queries the document for elements matching a CSS selector.
    /// </summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>A collection of matching elements.</returns>
    IEnumerable<IHtmlElement> QuerySelectorAll(string selector);

    /// <summary>
    /// Gets the first element matching a CSS selector.
    /// </summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The first matching element, or null if not found.</returns>
    IHtmlElement? QuerySelector(string selector);
}

/// <summary>
/// Represents an HTML element that can be analyzed and queried.
/// </summary>
public interface IHtmlElement
{
    /// <summary>
    /// Gets the tag name of the element.
    /// </summary>
    string TagName { get; }

    /// <summary>
    /// Gets the text content of the element.
    /// </summary>
    string TextContent { get; }

    /// <summary>
    /// Gets the inner HTML of the element.
    /// </summary>
    string InnerHtml { get; }

    /// <summary>
    /// Gets an attribute value by name.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The attribute value, or null if not found.</returns>
    string? GetAttribute(string name);

    /// <summary>
    /// Queries for child elements matching a CSS selector.
    /// </summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>A collection of matching child elements.</returns>
    IEnumerable<IHtmlElement> QuerySelectorAll(string selector);
}

/// <summary>
/// Represents an HTML hyperlink.
/// </summary>
public class HtmlLink
{
    /// <summary>
    /// Gets the URL of the link.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display text of the link.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the title attribute of the link, if present.
    /// </summary>
    public string? Title { get; init; }
}

/// <summary>
/// Configuration options for HTML parsing.
/// </summary>
public class HtmlParsingOptions
{
    /// <summary>
    /// Gets or sets the maximum size in bytes for HTML content that will be parsed.
    /// Content larger than this will be rejected. Default is 10 MB.
    /// </summary>
    public long MaxContentSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether scripts should be removed from parsed content.
    /// Default is true.
    /// </summary>
    public bool RemoveScripts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether styles should be removed from parsed content.
    /// Default is true.
    /// </summary>
    public bool RemoveStyles { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to decode HTML entities.
    /// Default is true.
    /// </summary>
    public bool DecodeEntities { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for parsing operations in milliseconds.
    /// Default is 5000 ms (5 seconds).
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
