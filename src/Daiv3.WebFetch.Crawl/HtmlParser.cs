using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Implements HTML parsing and content extraction using AngleSharp library.
/// </summary>
public class HtmlParser : IHtmlParser
{
    private readonly ILogger<HtmlParser> _logger;
    private readonly HtmlParsingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlParser"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The HTML parsing options.</param>
    public HtmlParser(ILogger<HtmlParser> logger, HtmlParsingOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new HtmlParsingOptions();
    }

    /// <summary>
    /// Parses HTML content and returns a parsed document representation.
    /// </summary>
    /// <param name="htmlContent">The HTML content to parse.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when htmlContent is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when content exceeds maximum size.</exception>
    public async Task<IHtmlDocument> ParseAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        if (htmlContent == null)
            throw new ArgumentNullException(nameof(htmlContent));

        // Check content size
        var contentSize = System.Text.Encoding.UTF8.GetByteCount(htmlContent);
        if (contentSize > _options.MaxContentSizeBytes)
        {
            _logger.LogWarning(
                "HTML content size ({ContentSize} bytes) exceeds maximum allowed ({MaxSize} bytes)",
                contentSize,
                _options.MaxContentSizeBytes);
            throw new InvalidOperationException(
                $"HTML content size ({contentSize} bytes) exceeds maximum allowed ({_options.MaxContentSizeBytes} bytes)");
        }

        try
        {
            _logger.LogDebug("Parsing HTML content ({ContentSize} bytes)", contentSize);

            // Create AngleSharp context with default configuration
            // Note: Context is not disposed here because the returned document needs it to remain valid
            var context = BrowsingContext.New(Configuration.Default);

            // Parse HTML with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutMs);

            var document = await context.OpenAsync(
                req => req.Content(htmlContent),
                cts.Token);

            _logger.LogDebug("Successfully parsed HTML document (Title: '{Title}')", document.Title ?? "(no title)");

            return new AngleSharpHtmlDocument(
                document,
                _options,
                () =>
                {
                    (document as IDisposable)?.Dispose();
                    context.Dispose();
                });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "HTML parsing operation timed out after {TimeoutMs} ms", _options.TimeoutMs);
            throw new InvalidOperationException($"HTML parsing timed out after {_options.TimeoutMs} ms", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML content");
            throw;
        }
    }

    /// <summary>
    /// Extracts text content from an HTML document.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <returns>The extracted text content.</returns>
    public string ExtractText(IHtmlDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (document is not AngleSharpHtmlDocument angleSharpDoc)
            throw new ArgumentException("Document must be an AngleSharpHtmlDocument instance", nameof(document));

        return angleSharpDoc.InternalDocument.Body?.TextContent ?? string.Empty;
    }

    /// <summary>
    /// Extracts all links from an HTML document.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <returns>A collection of links found in the document.</returns>
    public IEnumerable<HtmlLink> ExtractLinks(IHtmlDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (document is not AngleSharpHtmlDocument angleSharpDoc)
            throw new ArgumentException("Document must be an AngleSharpHtmlDocument instance", nameof(document));

        var links = new List<HtmlLink>();
        var anchorElements = angleSharpDoc.InternalDocument.QuerySelectorAll("a");

        foreach (var anchor in anchorElements.OfType<AngleSharp.Dom.IElement>())
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
                continue;

            links.Add(new HtmlLink
            {
                Url = href,
                Text = anchor.TextContent?.Trim() ?? string.Empty,
                Title = anchor.GetAttribute("title")
            });
        }

        return links;
    }

    /// <summary>
    /// Extracts specific elements from an HTML document using CSS selectors.
    /// </summary>
    /// <param name="document">The parsed HTML document.</param>
    /// <param name="selector">The CSS selector for elements to extract.</param>
    /// <returns>A collection of extracted elements.</returns>
    public IEnumerable<IHtmlElement> SelectElements(IHtmlDocument document, string selector)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("CSS selector cannot be null or empty", nameof(selector));

        return document.QuerySelectorAll(selector);
    }
}

/// <summary>
/// AngleSharp implementation of IHtmlDocument.
/// </summary>
internal sealed class AngleSharpHtmlDocument : IHtmlDocument
{
    private readonly AngleSharp.Dom.IDocument _document;
    private readonly Action _disposeResources;
    private readonly HtmlParsingOptions _options;
    private readonly string _cachedTitle;
    private readonly string _cachedHtml;
    private bool _disposed;

    /// <summary>
    /// Gets the internal AngleSharp document.
    /// </summary>
    public AngleSharp.Dom.IDocument InternalDocument => _document;

    public string? Title => _cachedTitle;

    public string Html => _cachedHtml;

    public AngleSharpHtmlDocument(AngleSharp.Dom.IDocument document, HtmlParsingOptions options, Action disposeResources)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeResources = disposeResources ?? throw new ArgumentNullException(nameof(disposeResources));
        
        // Eagerly cache properties that may be accessed after document operations
        _cachedTitle = document.Title ?? string.Empty;
        _cachedHtml = document.DocumentElement?.OuterHtml ?? string.Empty;
    }

    public IEnumerable<IHtmlElement> QuerySelectorAll(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("CSS selector cannot be null or empty", nameof(selector));

        return _document.QuerySelectorAll(selector)
            .OfType<AngleSharp.Dom.IElement>()
            .Select(el => new AngleSharpHtmlElement(el, _options))
            .Cast<IHtmlElement>();
    }

    public IHtmlElement? QuerySelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("CSS selector cannot be null or empty", nameof(selector));

        var element = _document.QuerySelector(selector) as AngleSharp.Dom.IElement;
        return element != null ? new AngleSharpHtmlElement(element, _options) : null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposeResources();
        _disposed = true;
    }
}

/// <summary>
/// AngleSharp implementation of IHtmlElement.
/// </summary>
internal class AngleSharpHtmlElement : IHtmlElement
{
    private readonly AngleSharp.Dom.IElement _element;
    private readonly HtmlParsingOptions _options;

    public string TagName => _element.TagName;

    public string TextContent => _element.TextContent ?? string.Empty;

    public string InnerHtml => _element.InnerHtml ?? string.Empty;

    public AngleSharpHtmlElement(AngleSharp.Dom.IElement element, HtmlParsingOptions options)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string? GetAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Attribute name cannot be null or empty", nameof(name));

        return _element.GetAttribute(name);
    }

    public IEnumerable<IHtmlElement> QuerySelectorAll(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("CSS selector cannot be null or empty", nameof(selector));

        return _element.QuerySelectorAll(selector)
            .OfType<AngleSharp.Dom.IElement>()
            .Select(el => new AngleSharpHtmlElement(el, _options))
            .Cast<IHtmlElement>();
    }
}
