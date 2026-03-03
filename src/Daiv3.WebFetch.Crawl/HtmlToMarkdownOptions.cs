namespace Daiv3.WebFetch.Crawl;

/// <summary>
/// Configuration options for HTML to Markdown conversion.
/// </summary>
public class HtmlToMarkdownOptions
{
    /// <summary>
    /// Gets or sets a list of HTML tag names to exclude during conversion.
    /// Common exclusions: script, style, nav, header, footer, aside, form, button.
    /// </summary>
    public ISet<string> ExcludeTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "script",
        "style",
        "noscript",
        "meta",
        "link",
        "title",
        "head",
        "nav",
        "header",
        "footer",
        "aside",
        "form",
        "button",
        "input",
        "iframe"
    };

    /// <summary>
    /// Gets or sets CSS selectors for elements to exclude (typically ads and navigation).
    /// Selectors like ".ads", ".sidebar", ".navigation", ".cookie-banner" are useful.
    /// </summary>
    public ISet<string> ExcludeSelectors { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".ads",
        ".advertisement",
        ".sidebar",
        ".navigation",
        ".cookie-banner",
        ".cookie-consent",
        ".popup",
        ".modal",
        "[role='complementary']",
        "[role='navigation']"
    };

    /// <summary>
    /// Gets or sets a value indicating whether to remove empty lines from the output.
    /// Default is true to produce cleaner Markdown.
    /// </summary>
    public bool RemoveEmptyLines { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to keep hyperlinks in the Markdown output.
    /// Default is true to preserve links for context and navigation.
    /// </summary>
    public bool KeepLinks { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to keep image references in the Markdown output.
    /// Default is false to reduce content size and complexity.
    /// </summary>
    public bool KeepImages { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to keep code blocks with syntax highlighting.
    /// Default is true to preserve code examples.
    /// </summary>
    public bool KeepCodeBlocks { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to preserve inline formatting (bold, italic, etc.).
    /// Default is true to maintain document structure.
    /// </summary>
    public bool PreserveInlineFormatting { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to remove HTML attributes and styling.
    /// Default is true to clean up the content.
    /// </summary>
    public bool StripAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to normalize whitespace (multiple spaces/tabs to single).
    /// Default is true to clean up formatting.
    /// </summary>
    public bool NormalizeWhitespace { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum content length in characters. Default is 5MB.
    /// Conversion is skipped for content exceeding this limit.
    /// </summary>
    public int MaxContentLength { get; set; } = 5 * 1024 * 1024; // 5MB
}
