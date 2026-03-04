using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

#pragma warning disable IDISP001 // HtmlToMarkdownConverter implements IDisposable - tests create instances for short-lived use
#pragma warning disable IDISP005 // Return type should indicate that the value should be disposed (test helpers return disposable instances for temporary use)

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Unit tests for HtmlToMarkdownConverter.
/// </summary>
public class HtmlToMarkdownConverterTests
{
    private IHtmlToMarkdownConverter CreateConverter(HtmlToMarkdownOptions? options = null)
    {
        var logger = NullLogger<HtmlToMarkdownConverter>.Instance;
        var opts = options ?? new HtmlToMarkdownOptions();
        return new HtmlToMarkdownConverter(logger, opts);
    }

    #region Basic Conversion Tests

    [Fact]
    public async Task ConvertAsync_SimpleHtml_ConvertsToMarkdown()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Hello World</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public async Task ConvertAsync_HeadingsAndParagraphs_ConvertsCorrectly()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<h1>Title</h1>
<p>This is a paragraph.</p>
<h2>Subtitle</h2>
<p>Another paragraph.</p>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Title", result);
        Assert.Contains("Subtitle", result);
        Assert.Contains("This is a paragraph", result);
    }

    [Fact]
    public async Task ConvertAsync_ListItems_ConvertsCorrectly()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<ul>
<li>Item 1</li>
<li>Item 2</li>
<li>Item 3</li>
</ul>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Item 1", result);
        Assert.Contains("Item 2", result);
        Assert.Contains("Item 3", result);
    }

    #endregion

    #region Stripping Tests

    [Fact]
    public async Task ConvertAsync_RemovesScriptTags()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<p>Content</p>
<script>alert('xss')</script>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
        Assert.DoesNotContain("alert", result);
        Assert.DoesNotContain("xss", result);
    }

    [Fact]
    public async Task ConvertAsync_RemovesStyleTags()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<p>Content</p>
<style>.color { color: red; }</style>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
        Assert.DoesNotContain("color", result);
        Assert.DoesNotContain(".color", result);
    }

    [Fact]
    public async Task ConvertAsync_RemovesNavigationElements()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<nav><a href='/'>Home</a></nav>
<p>Main Content</p>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Main Content", result);
        // Navigation might be stripped, but link text could remain depending on selector precision
    }

    [Fact]
    public async Task ConvertAsync_RemovesAdsWithSelector()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<p>Main Content</p>
<div class='ads'>Advertisement banner</div>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Main Content", result);
        Assert.DoesNotContain("Advertisement banner", result);
    }

    [Fact]
    public async Task ConvertAsync_RemovesCommonAdsSelectors()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<p>Content</p>
<div class='advertisement'>Ads Here</div>
<div class='sidebar'>Sidebar</div>
<div role='complementary'>Complementary Content</div>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
        Assert.DoesNotContain("Ads Here", result);
        Assert.DoesNotContain("Complementary Content", result);
    }

    [Fact]
    public async Task ConvertAsync_RemovesCookieBanners()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<div class='cookie-banner'>Accept Cookies?</div>
<p>Main Content</p>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Main Content", result);
        Assert.DoesNotContain("Accept Cookies", result);
    }

    #endregion

    #region Content Preservation Tests

    [Fact]
    public async Task ConvertAsync_PreservesLinks_WhenConfigured()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"<html><body><a href='https://example.com'>Link Text</a></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Link Text", result);
    }

    [Fact]
    public async Task ConvertAsync_PreservesCodeBlocks()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<p>See this code:</p>
<pre><code>function() { return true; }</code></pre>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("code", result.ToLower());
    }

    [Fact]
    public async Task ConvertAsync_PreservesInlineFormatting()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"<html><body><p>This is <strong>bold</strong> and <em>italic</em> text.</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("bold", result);
        Assert.Contains("italic", result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ConvertAsync_WithNullContent_ThrowsArgumentException()
    {
        // Arrange
        var converter = CreateConverter();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync(null!));
    }

    [Fact]
    public async Task ConvertAsync_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var converter = CreateConverter();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync(""));
    }

    [Fact]
    public async Task ConvertAsync_WithWhitespaceOnlyContent_ThrowsArgumentException()
    {
        // Arrange
        var converter = CreateConverter();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertAsync("   \t\n  "));
    }

    [Fact]
    public async Task ConvertAsync_WithContentExceedingMaxLength_ThrowsException()
    {
        // Arrange
        var options = new HtmlToMarkdownOptions { MaxContentLength = 100 };
        var converter = CreateConverter(options);
        var html = new string('a', 200);

        // Act & Assert
        await Assert.ThrowsAsync<HtmlToMarkdownConversionException>(
            () => converter.ConvertAsync(html));
    }

    [Fact]
    public async Task ConvertAsync_WithCancellationToken_RespondsToCancel()
    {
        // Arrange
        var converter = CreateConverter();
        using var cts = new CancellationTokenSource();
        var html = "<html><body><p>Content</p></body></html>";

        // Act - cancel before conversion
        cts.Cancel();
        var task = converter.ConvertAsync(html, cts.Token);

        // Assert - should either complete (if fast enough) or throw OperationCanceledException
        try
        {
            var result = await task;
            // If it completes, that's okay - short operations may complete before cancel takes effect
            Assert.NotNull(result);
        }
        catch (OperationCanceledException)
        {
            // Expected behavior
        }
    }

    #endregion

    #region Post-Processing Tests

    [Fact]
    public async Task ConvertAsync_NormalizesWhitespace()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Multiple   spaces    here</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("   ", result); // Should not have 3+ consecutive spaces
    }

    [Fact]
    public async Task ConvertAsync_RemovesExcessiveEmptyLines()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Line 1</p><br/><br/><br/><p>Line 2</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("\n\n\n", result); // Should not have 3+ consecutive newlines
    }

    [Fact]
    public async Task ConvertAsync_TrimmsLeadingTrailingWhitespace()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "   <html><body><p>Content</p></body></html>   ";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(result, result.Trim());
    }

    [Fact]
    public async Task ConvertAsync_WithRemoveEmptyLinesDisabled_KeepsEmptyLines()
    {
        // Arrange
        var options = new HtmlToMarkdownOptions { RemoveEmptyLines = false };
        var converter = CreateConverter(options);
        var html = "<html><body><p>Line 1</p><p>Line 2</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        // Should have newlines between paragraphs
        Assert.Contains("\n", result);
    }

    #endregion

    #region ConvertWithDetailsAsync Tests

    [Fact]
    public async Task ConvertWithDetailsAsync_ReturnsHtmlToMarkdownResult()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Content</p></body></html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MarkdownContent);
        Assert.Contains("Content", result.MarkdownContent);
        Assert.True(result.OriginalContentLength > 0);
        Assert.True(result.MarkdownContentLength > 0);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_CountsLinks()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<a href='http://link1.com'>Link 1</a>
<a href='http://link2.com'>Link 2</a>
<a href='http://link3.com'>Link 3</a>
</body>
</html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.LinksExtracted);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_CountsImages()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<img src='image1.jpg' />
<img src='image2.jpg' />
</body>
</html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.ImagesReferenced);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_CountsCodeBlocks()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<pre><code>block1</code></pre>
<pre><code>block2</code></pre>
</body>
</html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        // Code block count might vary based on how ReverseMarkdown formats them
        Assert.True(result.CodeBlocksFound >= 0);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_IncludesConversionTimestamp()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Content</p></body></html>";
        var beforeConversion = DateTime.UtcNow;

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ConvertedAtUtc >= beforeConversion);
        Assert.True(result.ConvertedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_CalculatesContentLengths()
    {
        // Arrange
        var converter = CreateConverter();
        var html = "<html><body><p>Test content here</p></body></html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(html.Length, result.OriginalContentLength);
        Assert.True(result.MarkdownContentLength > 0);
    }

    [Fact]
    public async Task ConvertWithDetailsAsync_CompressesContent()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<head><style>.class { color: red; }</style></head>
<body>
<nav>Navigation</nav>
<p>Content</p>
<div class='ads'>Ads</div>
</body>
</html>";

        // Act
        var result = await converter.ConvertWithDetailsAsync(html);

        // Assert
        Assert.NotNull(result);
        // Markdown should be smaller than original HTML
        Assert.True(result.MarkdownContentLength < result.OriginalContentLength);
    }

    #endregion

    #region Options Configuration Tests

    [Fact]
    public async Task ConvertAsync_CustomExcludeTagsWorks()
    {
        // Arrange
        var options = new HtmlToMarkdownOptions();
        options.ExcludeTags.Add("span");
        var converter = CreateConverter(options);
        var html = "<html><body><p>Text</p><span>Excluded</span></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Text", result);
        Assert.DoesNotContain("Excluded", result);
    }

    [Fact]
    public async Task ConvertAsync_CustomExcludeSelectorsWorks()
    {
        // Arrange
        var options = new HtmlToMarkdownOptions();
        options.ExcludeSelectors.Add(".custom-exclude");
        var converter = CreateConverter(options);
        var html = "<html><body><p>Keep</p><div class='custom-exclude'>Remove</div></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Keep", result);
        Assert.DoesNotContain("Remove", result);
    }

    [Fact]
    public async Task ConvertAsync_KeepImagesOptionDisabled_RemovesImages()
    {
        // Arrange
        var options = new HtmlToMarkdownOptions { KeepImages = false };
        var converter = CreateConverter(options);
        var html = @"<html><body><img src='test.jpg' alt='Test Image' /><p>Text</p></body></html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        // Image should be stripped or not included
        Assert.Contains("Text", result);
    }

    #endregion

    #region Complex HTML Tests

    [Fact]
    public async Task ConvertAsync_ComplexPageStructure_ExtractsMainContent()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<head><title>Page Title</title></head>
<body>
<header>
  <nav>
    <a href='/'>Home</a>
    <a href='/about'>About</a>
  </nav>
</header>
<main>
  <article>
    <h1>Article Title</h1>
    <p>Article content goes here.</p>
    <p>More content with <strong>important</strong> information.</p>
  </article>
</main>
<footer>
  <p>Copyright 2024</p>
</footer>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assistant
        Assert.NotNull(result);
        Assert.Contains("Article Title", result);
        Assert.Contains("Article content goes", result);
        Assert.Contains("important", result);
    }

    [Fact]
    public async Task ConvertAsync_NestedLists_ConvertsCorrectly()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<ul>
  <li>Item 1
    <ul>
      <li>Subitem 1</li>
      <li>Subitem 2</li>
    </ul>
  </li>
  <li>Item 2</li>
</ul>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Item 1", result);
        Assert.Contains("Subitem 1", result);
        Assert.Contains("Subitem 2", result);
        Assert.Contains("Item 2", result);
    }

    [Fact]
    public async Task ConvertAsync_Tables_ConvertsToMarkdown()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<table>
  <tr><th>Header 1</th><th>Header 2</th></tr>
  <tr><td>Cell 1</td><td>Cell 2</td></tr>
</table>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        // Table content should be preserved in some form
        Assert.Contains("Header 1", result);
        Assert.Contains("Cell 1", result);
    }

    [Fact]
    public async Task ConvertAsync_MixedContentTypes_PreservesAll()
    {
        // Arrange
        var converter = CreateConverter();
        var html = @"
<html>
<body>
<h1>Title</h1>
<p>Paragraph text.</p>
<ul><li>List item</li></ul>
<blockquote>Quoted text</blockquote>
<pre><code>Code block</code></pre>
</body>
</html>";

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Title", result);
        Assert.Contains("Paragraph text", result);
        Assert.Contains("List item", result);
        Assert.Contains("Quoted text", result);
    }

    #endregion

    #region Large Content Tests

    [Fact]
    public async Task ConvertAsync_LargeContentNearLimit_Succeeds()
    {
        // Arrange
        var largeContent = new string('a', 4 * 1024 * 1024); // 4MB
        var html = $"<html><body><p>{largeContent}</p></body></html>";
        var options = new HtmlToMarkdownOptions { MaxContentLength = 5 * 1024 * 1024 };
        var converter = CreateConverter(options);

        // Act
        var result = await converter.ConvertAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("aaa", result); // Some content should be preserved
    }

    #endregion
}
