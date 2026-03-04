using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for LocalModelIntentClassifier (MQ-REQ-011).
/// </summary>
public class LocalModelIntentClassifierTests
{
    private readonly Mock<ILogger<LocalModelIntentClassifier>> _mockLogger;
    private readonly Mock<ITaskTypeClassifier> _mockPatternFallback;
    private readonly LocalModelIntentClassificationOptions _options;

    public LocalModelIntentClassifierTests()
    {
        _mockLogger = new Mock<ILogger<LocalModelIntentClassifier>>();
        _mockPatternFallback = new Mock<ITaskTypeClassifier>();
        _options = new LocalModelIntentClassificationOptions();
    }

    [Fact]
    public async Task ClassifyAsync_WithModelDisabled_UsesFallback()
    {
        // Arrange
        _options.EnableLocalModel = false;
        var optionsWrapper = Options.Create(_options);
        _mockPatternFallback
            .Setup(x => x.Classify(It.IsAny<ExecutionRequest>()))
            .Returns(TaskType.Chat);

        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var result = await classifier.ClassifyAsync("Tell me about this topic");

        // Assert
        Assert.Equal(TaskType.Chat, result);
        Assert.Equal("pattern-based", classifier.GetClassificationMethod());
        _mockPatternFallback.Verify(x => x.Classify(It.IsAny<ExecutionRequest>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_WithNoModelPath_UsesFallback()
    {
        // Arrange
        _options.EnableLocalModel = true;
        _options.LocalModelPath = null;
        var optionsWrapper = Options.Create(_options);
        _mockPatternFallback
            .Setup(x => x.Classify(It.IsAny<ExecutionRequest>()))
            .Returns(TaskType.Search);

        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var result = await classifier.ClassifyAsync("Find information about AI");

        // Assert
        Assert.Equal(TaskType.Search, result);
        Assert.Equal("pattern-based", classifier.GetClassificationMethod());
    }

    [Fact]
    public async Task ClassifyAsync_WithValidInput_ReturnsFallbackClassification()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        _mockPatternFallback
            .Setup(x => x.Classify(It.IsAny<ExecutionRequest>()))
            .Returns(TaskType.Code);

        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var result = await classifier.ClassifyAsync("Write a function in C#");

        // Assert
        Assert.Equal(TaskType.Code, result);
        Assert.True(classifier.GetLastConfidenceScore() > 0);
    }

    [Fact]
    public async Task ClassifyAsync_WithNullContent_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            classifier.ClassifyAsync(null!));
    }

    [Fact]
    public async Task ClassifyAsync_WithEmptyContent_ThrowsException()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            classifier.ClassifyAsync(""));
    }

    [Fact]
    public async Task ClassifyAsync_OnException_FallsBackSafely()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);

        // Set up the mock to throw once (first call in try block), 
        // then return safely for the fallback call
        var callCount = 0;
        _mockPatternFallback
            .Setup(x => x.Classify(It.IsAny<ExecutionRequest>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("First call fails");
                }
                // Fallback returns a safe default
                return TaskType.Unknown;
            });

        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act & Assert - should not throw, should use pattern fallback
        var result = await classifier.ClassifyAsync("Some test input");
        Assert.Equal(TaskType.Unknown, result); // Should have fallen back to pattern classification
    }

    [Fact]
    public async Task IsModelAvailableAsync_WithoutModel_ReturnsFalse()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var isAvailable = await classifier.IsModelAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public void GetLastConfidenceScore_InitiallyZero()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var score = classifier.GetLastConfidenceScore();

        // Assert
        Assert.Equal(0m, score);
    }

    [Fact]
    public void GetClassificationMethod_ReturnsExpectedValue()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var method = classifier.GetClassificationMethod();

        // Assert
        Assert.NotNull(method);
        Assert.NotEmpty(method);
    }

    [Fact]
    public async Task ClassifyAsync_MultipleCallsWithDifferentInput_ProducesCorrectFallback()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        _mockPatternFallback
            .SetupSequence(x => x.Classify(It.IsAny<ExecutionRequest>()))
            .Returns(TaskType.Chat)
            .Returns(TaskType.Search)
            .Returns(TaskType.Code);

        var classifier = new LocalModelIntentClassifier(
            optionsWrapper,
            _mockLogger.Object,
            _mockPatternFallback.Object);

        // Act
        var result1 = await classifier.ClassifyAsync("Chat with me");
        var result2 = await classifier.ClassifyAsync("Find this");
        var result3 = await classifier.ClassifyAsync("Code something");

        // Assert
        Assert.Equal(TaskType.Chat, result1);
        Assert.Equal(TaskType.Search, result2);
        Assert.Equal(TaskType.Code, result3);
    }
}
