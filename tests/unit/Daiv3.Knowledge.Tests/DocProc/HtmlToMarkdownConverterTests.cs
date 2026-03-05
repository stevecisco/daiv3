using Daiv3.Knowledge.DocProc;
using Xunit;

namespace Daiv3.Knowledge.Tests.DocProc;

/// <summary>
/// Unit tests for HtmlToMarkdownConverter.
/// </summary>
public class HtmlToMarkdownConverterTests
{
    private readonly HtmlToMarkdownConverter _converter = new();

    [Fact]
    public void ConvertHtmlToMarkdown_SimpleHtml_ReturnsMarkdown()
    {
        // Arrange
        var html = "<p>Hello <strong>world</strong></p>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
        Assert.Contains("Hello", markdown);
        Assert.Contains("world", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_HeaderTags_PreservesStructure()
    {
        // Arrange
        var html = "<h1>Title</h1><h2>Subtitle</h2><p>Content</p>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        // Markdown headers use # prefix
        Assert.Contains("#", markdown);
        Assert.Contains("Title", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_ListElements_ConvertsToMarkdownLists()
    {
        // Arrange
        var html = "<ul><li>Item 1</li><li>Item 2</li><li>Item 3</li></ul>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("Item 1", markdown);
        Assert.Contains("Item 2", markdown);
        Assert.Contains("Item 3", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_LinkElements_PreservesLinks()
    {
        // Arrange
        var html = "<a href=\"https://example.com\">Example Link</a>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("Example Link", markdown);
        // Markdown links formatted as [text](url)
        Assert.Contains("example.com", markdown.ToLowerInvariant());
    }

    [Fact]
    public void ConvertHtmlToMarkdown_CodeBlocks_PreserveFormatting()
    {
        // Arrange
        var html = "<pre><code>var x = 42;</code></pre>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("var", markdown);
        Assert.Contains("42", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var html = string.Empty;

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.Empty(markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_WhitespaceOnly_ReturnsEmpty()
    {
        // Arrange
        var html = "   \n\t  ";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.Empty(markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_NullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _converter.ConvertHtmlToMarkdown(null!));
    }

    [Fact]
    public void ConvertHtmlToMarkdown_ComplexHtml_PreservesContent()
    {
        // Arrange
        var html = @"
            <div class='container'>
                <h1>Article Title</h1>
                <p>Introduction paragraph with <em>emphasis</em> and <strong>bold</strong>.</p>
                <ul>
                    <li>First point</li>
                    <li>Second point</li>
                </ul>
                <p>Conclusion paragraph.</p>
            </div>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("Article Title", markdown);
        Assert.Contains("Introduction", markdown);
        Assert.Contains("First point", markdown);
        Assert.Contains("Conclusion", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_TableStructure_ConvertsToMarkdown()
    {
        // Arrange
        var html = @"
            <table>
                <tr><th>Header 1</th><th>Header 2</th></tr>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
            </table>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        // Should contain table data
        Assert.Contains("Header", markdown);
        Assert.Contains("Cell", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_RemovesHtmlAttributes()
    {
        // Arrange
        var html = "<p class='highlight' id='intro' style='color: red;'>Text</p>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("Text", markdown);
        // Attributes should be removed (not in output)
        Assert.DoesNotContain("class", markdown);
        Assert.DoesNotContain("style", markdown);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_NormalizesExcessiveWhitespace()
    {
        // Arrange
        var html = "<p>Line 1</p>\n\n\n\n\n<p>Line 2</p>";

        // Act
        var markdown = _converter.ConvertHtmlToMarkdown(html);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("Line 1", markdown);
        Assert.Contains("Line 2", markdown);
        // Should not have more than 2 consecutive newlines
        Assert.DoesNotContain("\n\n\n", markdown);
    }
}
