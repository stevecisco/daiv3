using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.WebFetch.Crawl.Tests;

/// <summary>
/// Unit tests for HtmlParser implementation.
/// </summary>
public class HtmlParserTests
{
    private static readonly string SampleHtml = @"
        <!DOCTYPE html>
        <html>
        <head>
            <title>Test Page</title>
        </head>
        <body>
            <h1>Welcome</h1>
            <p>This is a test page.</p>
            <a href='https://example.com'>Example</a>
            <a href='https://google.com' title='Google'>Google</a>
            <div class='content'>
                <p>Nested content</p>
            </div>
        </body>
        </html>";

    private readonly Mock<ILogger<HtmlParser>> _mockLogger;
    private readonly HtmlParser _parser;

    public HtmlParserTests()
    {
        _mockLogger = new Mock<ILogger<HtmlParser>>();
        _parser = new HtmlParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseAsync_ValidHtml_ReturnsDocument()
    {
        // Act
        var document = await _parser.ParseAsync(SampleHtml);

        // Assert
        Assert.NotNull(document);
        Assert.NotNull(document.Title);
    }

    [Fact]
    public async Task ParseAsync_ValidHtml_DocumentHasCorrectTitle()
    {
        // Act
        var document = await _parser.ParseAsync(SampleHtml);

        // Assert
        Assert.Equal("Test Page", document.Title);
    }

    [Fact]
    public async Task ParseAsync_EmptyHtml_ReturnsValidDocument()
    {
        // Arrange
        var emptyHtml = "<html><body></body></html>";

        // Act
        var document = await _parser.ParseAsync(emptyHtml);

        // Assert
        Assert.NotNull(document);
    }

    [Fact]
    public async Task ParseAsync_MalformedHtml_ReturnsValidDocument()
    {
        // Arrange
        var malformedHtml = "<html><body><p>Unclosed paragraph<div>Unclosed div</body></html>";

        // Act - AngleSharp should handle malformed HTML gracefully
        var document = await _parser.ParseAsync(malformedHtml);

        // Assert
        Assert.NotNull(document);
    }

    [Fact]
    public async Task ParseAsync_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _parser.ParseAsync(null!));
    }

    [Fact]
    public async Task ParseAsync_ContentExceedsMaxSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var largeContent = new string('a', (int)new HtmlParsingOptions().MaxContentSizeBytes + 1);
        var parser = new HtmlParser(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync(largeContent));
    }

    [Fact]
    public async Task ParseAsync_WithCustomContentSizeLimit_RespectsLimit()
    {
        // Arrange
        var options = new HtmlParsingOptions { MaxContentSizeBytes = 100 };
        var parser = new HtmlParser(_mockLogger.Object, options);
        var largeContent = new string('a', 150);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => parser.ParseAsync(largeContent));
    }

    [Fact]
    public async Task ParseAsync_LogsDebugMessage()
    {
        // Act
        await _parser.ParseAsync(SampleHtml);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Parsing HTML")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractText_ValidDocument_ReturnsText()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var text = _parser.ExtractText(document);

        // Assert
        Assert.Contains("Welcome", text);
        Assert.Contains("This is a test page", text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractText_DocumentWithNestedElements_ReturnsAllText()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var text = _parser.ExtractText(document);

        // Assert
        Assert.Contains("Nested content", text);
    }

    [Fact]
    public async Task ExtractText_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.ExtractText(null!));
    }

    [Fact]
    public async Task ExtractLinks_ValidDocument_ReturnsAllLinks()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var links = _parser.ExtractLinks(document).ToList();

        // Assert
        Assert.NotEmpty(links);
        Assert.Contains(links, l => l.Url == "https://example.com");
        Assert.Contains(links, l => l.Url == "https://google.com");
    }

    [Fact]
    public async Task ExtractLinks_LinkWithTitle_ReturnsTitle()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var links = _parser.ExtractLinks(document).ToList();

        // Assert
        var googleLink = links.FirstOrDefault(l => l.Url == "https://google.com");
        Assert.NotNull(googleLink);
        Assert.Equal("Google", googleLink.Title);
    }

    [Fact]
    public async Task ExtractLinks_LinkWithoutHref_IsSkipped()
    {
        // Arrange
        var html = "<html><body><a>No href</a><a href='https://example.com'>With href</a></body></html>";
        var document = await _parser.ParseAsync(html);

        // Act
        var links = _parser.ExtractLinks(document).ToList();

        // Assert
        Assert.Single(links);
        Assert.Equal("https://example.com", links[0].Url);
    }

    [Fact]
    public async Task ExtractLinks_EmptyHref_IsSkipped()
    {
        // Arrange
        var html = "<html><body><a href=''>Empty</a><a href='  '>Whitespace</a></body></html>";
        var document = await _parser.ParseAsync(html);

        // Act
        var links = _parser.ExtractLinks(document).ToList();

        // Assert
        Assert.Empty(links);
    }

    [Fact]
    public async Task ExtractLinks_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.ExtractLinks(null!));
    }

    [Fact]
    public async Task SelectElements_ValidSelector_ReturnsElements()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var elements = _parser.SelectElements(document, "p").ToList();

        // Assert
        Assert.NotEmpty(elements);
        Assert.True(elements.Count >= 2); // At least 2 paragraphs
    }

    [Fact]
    public async Task SelectElements_ClassSelector_ReturnsCorrectElements()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var elements = _parser.SelectElements(document, ".content").ToList();

        // Assert
        Assert.NotEmpty(elements);
        Assert.Single(elements);
        Assert.Equal("DIV", elements[0].TagName);
    }

    [Fact]
    public async Task SelectElements_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.SelectElements(null!, "p"));
    }

    [Fact]
    public async Task SelectElements_NullSelector_ThrowsArgumentException()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.SelectElements(document, null!));
    }

    [Fact]
    public async Task SelectElements_EmptySelector_ThrowsArgumentException()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.SelectElements(document, "   "));
    }

    [Fact]
    public async Task SelectElements_NoMatching_ReturnsEmpty()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var elements = _parser.SelectElements(document, "nonexistent").ToList();

        // Assert
        Assert.Empty(elements);
    }
}

/// <summary>
/// Unit tests for IHtmlDocument interface.
/// </summary>
public class HtmlDocumentTests
{
    private static readonly string SampleHtml = @"
        <!DOCTYPE html>
        <html>
        <head><title>Test</title></head>
        <body>
            <h1 class='header'>Title</h1>
            <p id='para1'>First paragraph</p>
            <p id='para2'>Second paragraph</p>
            <div><span>Nested span</span></div>
        </body>
        </html>";

    private readonly HtmlParser _parser;

    public HtmlDocumentTests()
    {
        _parser = new HtmlParser(new Mock<ILogger<HtmlParser>>().Object);
    }

    [Fact]
    public async Task QuerySelectorAll_ValidSelector_ReturnsAllMatching()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var elements = document.QuerySelectorAll("p").ToList();

        // Assert
        Assert.Equal(2, elements.Count);
    }

    [Fact]
    public async Task QuerySelector_ValidSelector_ReturnsFirst()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("p");

        // Assert
        Assert.NotNull(element);
        Assert.Contains("First paragraph", element.TextContent);
    }

    [Fact]
    public async Task QuerySelector_NoMatch_ReturnsNull()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("form");

        // Assert
        Assert.Null(element);
    }

    [Fact]
    public async Task Title_ReturnsDocumentTitle()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act & Assert
        Assert.Equal("Test", document.Title);
    }

    [Fact]
    public async Task Html_ReturnsHtmlContent()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var html = document.Html;

        // Assert
        Assert.NotEmpty(html);
        Assert.Contains("html", html.ToLower());
    }
}

/// <summary>
/// Unit tests for IHtmlElement interface.
/// </summary>
public class HtmlElementTests
{
    private static readonly string SampleHtml = @"
        <html><body>
            <div id='container' class='main content' data-value='test'>
                <p>Sample text</p>
                <span class='label'>Label</span>
                <a href='https://example.com'>Link</a>
            </div>
        </body></html>";

    private readonly HtmlParser _parser;

    public HtmlElementTests()
    {
        _parser = new HtmlParser(new Mock<ILogger<HtmlParser>>().Object);
    }

    [Fact]
    public async Task TagName_ReturnsElementTag()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("p");

        // Assert
        Assert.NotNull(element);
        Assert.Equal("P", element.TagName);
    }

    [Fact]
    public async Task TextContent_ReturnsText()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("p");

        // Assert
        Assert.NotNull(element);
        Assert.Equal("Sample text", element.TextContent);
    }

    [Fact]
    public async Task GetAttribute_ExistingAttribute_ReturnsValue()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("[data-value]");

        // Assert
        Assert.NotNull(element);
        Assert.Equal("test", element.GetAttribute("data-value"));
    }

    [Fact]
    public async Task GetAttribute_NonexistentAttribute_ReturnsNull()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("p");

        // Assert
        Assert.NotNull(element);
        Assert.Null(element.GetAttribute("nonexistent"));
    }

    [Fact]
    public async Task QuerySelectorAll_ChildElements_ReturnsMatching()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);
        var container = document.QuerySelector("#container");

        // Act
        var spans = container!.QuerySelectorAll("span").ToList();

        // Assert
        Assert.Single(spans);
    }

    [Fact]
    public async Task InnerHtml_ReturnsElementHtml()
    {
        // Arrange
        var document = await _parser.ParseAsync(SampleHtml);

        // Act
        var element = document.QuerySelector("div");

        // Assert
        Assert.NotNull(element);
        Assert.NotEmpty(element.InnerHtml);
        Assert.Contains("p", element.InnerHtml.ToLower());
    }
}

/// <summary>
/// Unit tests for HtmlParsingOptions configuration.
/// </summary>
public class HtmlParsingOptionsTests
{
    [Fact]
    public void DefaultOptions_HasReasonableDefaults()
    {
        // Arrange & Act
        var options = new HtmlParsingOptions();

        // Assert
        Assert.Equal(10 * 1024 * 1024, options.MaxContentSizeBytes); // 10 MB
        Assert.True(options.RemoveScripts);
        Assert.True(options.RemoveStyles);
        Assert.True(options.DecodeEntities);
        Assert.Equal(5000, options.TimeoutMs);
    }

    [Fact]
    public void CustomOptions_CanBeConfigured()
    {
        // Arrange & Act
        var options = new HtmlParsingOptions
        {
            MaxContentSizeBytes = 5_000_000,
            RemoveScripts = false,
            TimeoutMs = 10000
        };

        // Assert
        Assert.Equal(5_000_000, options.MaxContentSizeBytes);
        Assert.False(options.RemoveScripts);
        Assert.Equal(10000, options.TimeoutMs);
    }
}
