using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for IntentResolver.
/// </summary>
public class IntentResolverTests
{
    private readonly Mock<ILogger<IntentResolver>> _mockLogger;
    private readonly OrchestrationOptions _options;
    private readonly IntentResolver _resolver;

    public IntentResolverTests()
    {
        _mockLogger = new Mock<ILogger<IntentResolver>>();
        _options = new OrchestrationOptions();

        _resolver = new IntentResolver(
            _mockLogger.Object,
            Options.Create(_options));
    }

    [Theory]
    [InlineData("search for documents", "search")]
    [InlineData("find all files", "search")]
    [InlineData("tell me about AI", "chat")]
    [InlineData("what is machine learning", "chat")]
    [InlineData("create a new project", "create")]
    [InlineData("generate a report", "create")]
    [InlineData("analyze this code", "analyze")]
    [InlineData("review the implementation", "analyze")]
    [InlineData("summarize the document", "summarize")]
    [InlineData("write code for login", "code")]
    [InlineData("debug this issue", "debug")]
    public async Task ResolveAsync_RecognizesCommonIntents(string input, string expectedIntent)
    {
        // Act
        var intent = await _resolver.ResolveAsync(input, new Dictionary<string, string>());

        // Assert
        Assert.Equal(expectedIntent, intent.Type);
        Assert.InRange(intent.Confidence, 0.5m, 1.0m);
    }

    [Fact]
    public async Task ResolveAsync_WithAmbiguousInput_DefaultsToChat()
    {
        // Arrange
        var input = "hello there";

        // Act
        var intent = await _resolver.ResolveAsync(input, new Dictionary<string, string>());

        // Assert
        Assert.Equal("chat", intent.Type);
        Assert.True(intent.Confidence > 0);
    }

    [Fact]
    public async Task ResolveAsync_ExtractsFileTypeEntities()
    {
        // Arrange
        var input = "find all .cs files";

        // Act
        var intent = await _resolver.ResolveAsync(input, new Dictionary<string, string>());

        // Assert
        Assert.Contains("file_type", intent.Entities.Keys);
        Assert.Equal(".cs", intent.Entities["file_type"]);
    }

    [Fact]
    public async Task ResolveAsync_ExtractsQuotedText()
    {
        // Arrange
        var input = "search for \"hello world\" in the codebase";

        // Act
        var intent = await _resolver.ResolveAsync(input, new Dictionary<string, string>());

        // Assert
        Assert.Contains("quoted_0", intent.Entities.Keys);
        Assert.Equal("hello world", intent.Entities["quoted_0"]);
    }

    [Fact]
    public async Task ResolveAsync_MergesContextIntoEntities()
    {
        // Arrange
        var input = "search for files";
        var context = new Dictionary<string, string>
        {
            ["project_id"] = Guid.NewGuid().ToString(),
            ["user_id"] = "test_user"
        };

        // Act
        var intent = await _resolver.ResolveAsync(input, context);

        // Assert
        Assert.Contains("project_id", intent.Entities.Keys);
        Assert.Contains("user_id", intent.Entities.Keys);
    }

    [Fact]
    public async Task ResolveAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _resolver.ResolveAsync(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public async Task ResolveAsync_WithEmptyInput_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _resolver.ResolveAsync("", new Dictionary<string, string>()));
    }

    [Fact]
    public async Task ResolveAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _resolver.ResolveAsync("test", null!));
    }

    [Fact]
    public async Task ResolveAsync_WithMultipleKeywords_PrioritizesStrongestIntent()
    {        // Arrange - "search" appears twice, should be strongest
        var input = "search and find the code to analyze";

        // Act
        var intent = await _resolver.ResolveAsync(input, new Dictionary<string, string>());

        // Assert
        Assert.Equal("search", intent.Type);
        Assert.True(intent.Confidence > 0.6m);
    }
}
