using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for TaskTypeClassifier (MQ-REQ-008).
/// </summary>
public class TaskTypeClassifierTests
{
    private readonly ILogger<TaskTypeClassifier> _logger;

    public TaskTypeClassifierTests()
    {
        _logger = new LoggerFactory().CreateLogger<TaskTypeClassifier>();
    }

    [Theory]
    [InlineData("let's chat about the weather", TaskType.Chat)]
    [InlineData("can you help me with something?", TaskType.Chat)]
    [InlineData("tell me about your capabilities", TaskType.Chat)]
    [InlineData("search for documents about AI", TaskType.Search)]
    [InlineData("find information on machine learning", TaskType.Search)]
    [InlineData("where can I find the project files?", TaskType.Search)]
    [InlineData("summarize this document", TaskType.Summarize)]
    [InlineData("give me a brief overview", TaskType.Summarize)]
    [InlineData("provide a summary of the main ideas", TaskType.Summarize)]
    [InlineData("write a function to sort an array", TaskType.Code)]
    [InlineData("implement a class for user management", TaskType.Code)]
    [InlineData("debug this python script", TaskType.Code)]
    [InlineData("what is machine learning?", TaskType.QuestionAnswer)]
    [InlineData("why does this happen?", TaskType.QuestionAnswer)]
    [InlineData("how do I fix this?", TaskType.QuestionAnswer)]
    [InlineData("rewrite this paragraph in formal tone", TaskType.Rewrite)]
    [InlineData("paraphrase this sentence", TaskType.Rewrite)]
    [InlineData("improve this text", TaskType.Rewrite)]
    [InlineData("translate this to Spanish", TaskType.Translation)]
    [InlineData("convert to French", TaskType.Translation)]
    [InlineData("in German please", TaskType.Translation)]
    [InlineData("analyze the performance metrics", TaskType.Analysis)]
    [InlineData("evaluate the pros and cons", TaskType.Analysis)]
    [InlineData("compare these two options", TaskType.Analysis)]
    [InlineData("generate a report", TaskType.Generation)]
    [InlineData("create a new design", TaskType.Generation)]
    [InlineData("write a blog post", TaskType.Generation)]
    [InlineData("extract the names from this text", TaskType.Extraction)]
    [InlineData("list all email addresses", TaskType.Extraction)]
    [InlineData("get the dates from the document", TaskType.Extraction)]
    public void Classify_WithTypicalContent_ReturnsCorrectTaskType(string content, TaskType expectedType)
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void Classify_WithUnrecognizedContent_ReturnsUnknown()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        var content = "xyzabc123";

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(TaskType.Unknown, result);
    }

    [Fact]
    public void Classify_WithRequest_UsesContentForClassification()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        var request = new ExecutionRequest
        {
            Content = "write a function to calculate fibonacci numbers"
        };

        // Act
        var result = classifier.Classify(request);

        // Assert
        Assert.Equal(TaskType.Code, result);
    }

    [Fact]
    public void Classify_WithExplicitTaskType_RespectsExplicitType()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions
        {
            UseExplicitTaskType = true
        });
        var classifier = new TaskTypeClassifier(options, _logger);
        var request = new ExecutionRequest
        {
            TaskType = "Code",
            Content = "this content would normally be classified as chat"
        };

        // Act
        var result = classifier.Classify(request);

        // Assert
        Assert.Equal(TaskType.Code, result);
    }

    [Fact]
    public void Classify_WithDisabledExplicitTaskType_IgnoresExplicitType()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions
        {
            UseExplicitTaskType = false
        });
        var classifier = new TaskTypeClassifier(options, _logger);
        var request = new ExecutionRequest
        {
            TaskType = "Code",
            Content = "let's chat about the weather"
        };

        // Act
        var result = classifier.Classify(request);

        // Assert
        Assert.Equal(TaskType.Chat, result);
    }

    [Theory]
    [InlineData("SEARCH for documents", TaskType.Search)]
    [InlineData("SeArCh for documents", TaskType.Search)]
    [InlineData("search for documents", TaskType.Search)]
    public void Classify_WithCaseInsensitive_MatchesAnyCase(string content, TaskType expectedType)
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions
        {
            CaseInsensitiveMatching = true
        });
        var classifier = new TaskTypeClassifier(options, _logger);

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void Classify_WithCustomPatterns_UsesCustomPatterns()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions
        {
            CustomPatterns = new Dictionary<string, string[]>
            {
                ["Code"] = new[] { "custom-code-keyword" }
            }
        });
        var classifier = new TaskTypeClassifier(options, _logger);
        var content = "use custom-code-keyword to do something";

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(TaskType.Code, result);
    }

    [Fact]
    public void Classify_WithMultipleMatches_ReturnsHighestScoringType()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        // This content has both "search" and "find" which are search patterns
        var content = "search and find information about code implementation";

        // Act
        var result = classifier.Classify(content);

        // Assert
        // Should match Search more strongly (2 matches) than Code (1 match)
        Assert.Equal(TaskType.Search, result);
    }

    [Fact]
    public void Classify_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => classifier.Classify((ExecutionRequest)null!));
    }

    [Fact]
    public void Classify_WithNullContent_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => classifier.Classify((string)null!));
    }

    [Fact]
    public void Classify_WithEmptyContent_ReturnsUnknown()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        var content = "";

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(TaskType.Unknown, result);
    }

    [Fact]
    public void Classify_WithInvalidExplicitTaskType_FallsBackToContentClassification()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions
        {
            UseExplicitTaskType = true
        });
        var classifier = new TaskTypeClassifier(options, _logger);
        var request = new ExecutionRequest
        {
            TaskType = "InvalidType",
            Content = "search for information"
        };

        // Act
        var result = classifier.Classify(request);

        // Assert
        Assert.Equal(TaskType.Search, result);
    }

    [Fact]
    public void Classify_WithCodePatterns_IdentifiesCodeContent()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        var codeContent = "public class MyClass { public void MyMethod() { } }";

        // Act
        var result = classifier.Classify(codeContent);

        // Assert
        Assert.Equal(TaskType.Code, result);
    }

    [Fact]
    public void Classify_WithQuestionMark_IdentifiesQuestionAnswer()
    {
        // Arrange
        var options = Options.Create(new TaskTypeClassifierOptions());
        var classifier = new TaskTypeClassifier(options, _logger);
        var content = "What is the meaning of this code?";

        // Act
        var result = classifier.Classify(content);

        // Assert
        Assert.Equal(TaskType.QuestionAnswer, result);
    }
}
