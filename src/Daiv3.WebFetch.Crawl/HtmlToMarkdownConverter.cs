using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;
using System.Text.RegularExpressions;

namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Converts HTML content to Markdown while stripping styling, navigation, and ads.
/// </summary>
public class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly ILogger<HtmlToMarkdownConverter> _logger;
    private readonly HtmlToMarkdownOptions _options;
    private readonly IBrowsingContext _browsingContext;
    private readonly Converter _reverseMarkdownConverter;

    public HtmlToMarkdownConverter(
        ILogger<HtmlToMarkdownConverter> logger,
        HtmlToMarkdownOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _browsingContext = BrowsingContext.New();
        _reverseMarkdownConverter = new Converter();
    }

    public async Task<string> ConvertAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        var result = await ConvertWithDetailsAsync(htmlContent, cancellationToken);
        return result.MarkdownContent;
    }

    public async Task<HtmlToMarkdownResult> ConvertWithDetailsAsync(
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                throw new ArgumentException("HTML content cannot be null or empty.", nameof(htmlContent));
            }

            if (htmlContent.Length > _options.MaxContentLength)
            {
                _logger.LogWarning(
                    "HTML content exceeds maximum length ({ActualLength} > {MaxLength}). Skipping conversion.",
                    htmlContent.Length,
                    _options.MaxContentLength);
                throw new HtmlToMarkdownConversionException(
                    $"HTML content exceeds maximum length of {_options.MaxContentLength} characters.");
            }

            var originalLength = htmlContent.Length;
            _logger.LogDebug("Starting HTML to Markdown conversion. Original content length: {Length}", originalLength);

            // Parse and clean HTML
            var cleanedHtml = await CleanHtmlAsync(htmlContent, cancellationToken);

            // Convert to Markdown using ReverseMarkdown
            string markdown = _reverseMarkdownConverter.Convert(cleanedHtml);

            // Apply post-processing
            markdown = ApplyPostProcessing(markdown);

            // Gather conversion statistics
            var stats = GatherConversionStatistics(htmlContent, markdown);

            var result = new HtmlToMarkdownResult(
                markdown,
                stats.ElementsStripped,
                stats.LinksExtracted,
                stats.ImagesReferenced,
                stats.CodeBlocksFound,
                originalLength);

            _logger.LogDebug(
                "HTML to Markdown conversion completed. Original: {OriginalLength} chars, " +
                "Markdown: {MarkdownLength} chars, Elements stripped: {ElementsStripped}, " +
                "Links: {Links}, Images: {Images}, Code blocks: {CodeBlocks}",
                originalLength,
                markdown.Length,
                stats.ElementsStripped,
                stats.LinksExtracted,
                stats.ImagesReferenced,
                stats.CodeBlocksFound);

            return result;
        }
        catch (ArgumentException)
        {
            throw; // Let argument validation exceptions pass through
        }
        catch (HtmlToMarkdownConversionException)
        {
            throw; // Let our own conversion exceptions pass through
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation exceptions pass through
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting HTML to Markdown");
            throw new HtmlToMarkdownConversionException("Failed to convert HTML to Markdown.", ex);
        }
    }

    private async Task<string> CleanHtmlAsync(string htmlContent, CancellationToken cancellationToken)
    {
        // Parse the HTML document using AngleSharp
        var document = await _browsingContext.OpenAsync(req => req.Content(htmlContent), cancellationToken);

        // Remove excluded tags
        RemoveExcludedTags(document);

        // Remove elements matching excluded selectors
        RemoveExcludedSelectors(document);

        // Remove empty or whitespace-only elements
        RemoveEmptyElements(document);

        // Clean up attributes if configured
        if (_options.StripAttributes)
        {
            StripAttributes(document);
        }

        return document.DocumentElement?.InnerHtml ?? string.Empty;
    }

    private void RemoveExcludedTags(IDocument document)
    {
        foreach (var tagName in _options.ExcludeTags)
        {
            var elements = document.QuerySelectorAll(tagName).ToList();
            foreach (var element in elements)
            {
                element.Remove();
            }
        }
    }

    private void RemoveExcludedSelectors(IDocument document)
    {
        foreach (var selector in _options.ExcludeSelectors)
        {
            try
            {
                var elements = document.QuerySelectorAll(selector).ToList();
                foreach (var element in elements)
                {
                    element.Remove();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to remove elements matching selector '{Selector}'",
                    selector);
            }
        }
    }

    private void RemoveEmptyElements(IDocument document)
    {
        // Remove elements that contain only whitespace (except specific tags)
        var preserveTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "br", "hr", "img" };

        var allElements = document.QuerySelectorAll("*").ToList();
        foreach (var element in allElements)
        {
            if (preserveTags.Contains(element.TagName))
                continue;

            var innerText = element.TextContent?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(innerText) && element.Children.Length == 0)
            {
                element.Remove();
            }
        }
    }

    private void StripAttributes(IDocument document)
    {
        var allElements = document.QuerySelectorAll("*").ToList();
        foreach (var element in allElements)
        {
            // Keep only essential attributes
            var attributesToRemove = new List<string>();
            var essentialAttributes = new[] { "href", "src", "alt", "title" };

            foreach (var attr in element.Attributes)
            {
                if (!essentialAttributes.Contains(attr.Name.ToLowerInvariant()))
                {
                    attributesToRemove.Add(attr.Name);
                }
            }

            foreach (var attrName in attributesToRemove)
            {
                element.RemoveAttribute(attrName);
            }
        }
    }

    private string ApplyPostProcessing(string markdown)
    {
        var result = markdown;

        // Normalize whitespace if configured
        if (_options.NormalizeWhitespace)
        {
            result = NormalizeWhitespace(result);
        }

        // Remove empty lines if configured
        if (_options.RemoveEmptyLines)
        {
            result = RemoveExcessiveEmptyLines(result);
        }

        // Clean up line endings
        result = result.Replace("\r\n", "\n").Replace("\r", "\n");

        // Trim leading and trailing whitespace
        result = result.Trim();

        return result;
    }

    private string NormalizeWhitespace(string text)
    {
        // Replace multiple spaces with a single space
        text = Regex.Replace(text, @"[ \t]+", " ");

        // Replace multiple newlines (more than 2) with exactly 2 newlines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text;
    }

    private string RemoveExcessiveEmptyLines(string text)
    {
        // Replace multiple empty lines with a single empty line (max 2 consecutive newlines)
        return Regex.Replace(text, @"\n\n+", "\n\n");
    }

    private class ConversionStats
    {
        public int ElementsStripped { get; set; }
        public int LinksExtracted { get; set; }
        public int ImagesReferenced { get; set; }
        public int CodeBlocksFound { get; set; }
    }

    private ConversionStats GatherConversionStatistics(string originalHtml, string markdown)
    {
        var stats = new ConversionStats();

        // Count links in original HTML
        var linkMatches = Regex.Matches(originalHtml, @"<a\s+[^>]*href=", RegexOptions.IgnoreCase);
        stats.LinksExtracted = linkMatches.Count;

        // Count image references
        var imgMatches = Regex.Matches(originalHtml, @"<img\s+", RegexOptions.IgnoreCase);
        stats.ImagesReferenced = imgMatches.Count;

        // Count code blocks in markdown
        var codeBlockMatches = Regex.Matches(markdown, @"```", RegexOptions.IgnoreCase);
        stats.CodeBlocksFound = codeBlockMatches.Count / 2; // Opening and closing markers

        // Estimate elements stripped based on HTML tag removal
        var htmlTagMatches = Regex.Matches(originalHtml, @"<[^>]+>");
        var markdownCodeBlockTags = Regex.Matches(markdown, @"```[^`]*```", RegexOptions.Singleline);
        var codeBlockContent = markdownCodeBlockTags.Cast<Match>().Sum(m => m.Value.Count(c => c == '<'));
        var trimmedHtmlTags = htmlTagMatches.Count - (codeBlockContent > 0 ? 1 : 0);
        var markdownTags = Regex.Matches(markdown, @"[*_`~\[\]()]").Count;
        stats.ElementsStripped = Math.Max(0, trimmedHtmlTags - (markdownTags / 4)); // Rough estimate

        return stats;
    }
}
