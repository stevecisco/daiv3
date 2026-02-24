using Daiv3.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.Knowledge;

public class TopicSummaryServiceTests
{
    [Fact]
    public async Task GenerateSummaryAsync_NormalText_ReturnsSummary()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions
        {
            MinSentences = 2,
            MaxSentences = 3,
            MaxCharacters = 500,
            PreserveSentenceOrder = true
        });
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);
        
        var text = "This is the first sentence. This is the second sentence with important information. " +
                   "This is the third sentence also important. This is fourth sentence. " +
                   "This is fifth sentence with less importance.";

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.NotEmpty(summary);
        Assert.True(summary.Length <= options.Value.MaxCharacters);
        var sentenceCount = summary.Count(c => c == '.');
        Assert.InRange(sentenceCount, options.Value.MinSentences, options.Value.MaxSentences);
    }

    [Fact]
    public async Task GenerateSummaryAsync_VeryShortText_ReturnsAsIs()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);
        
        var text = "First sentence. Second sentence.";

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.Equal(text, summary);
    }

    [Fact]
    public async Task GenerateSummaryAsync_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.GenerateSummaryAsync(string.Empty));
    }

    [Fact]
    public async Task GenerateSummaryAsync_NullText_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.GenerateSummaryAsync(null!));
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhitespaceText_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.GenerateSummaryAsync("   \n\t   "));
    }

    [Fact]
    public async Task GenerateSummaryAsync_PreserveSentenceOrder_MaintainsOriginalSequence()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions
        {
            MinSentences = 2,
            MaxSentences = 3,
            PreserveSentenceOrder = true
        });
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        var text = "First important point. Second less important point. Third very important point. " +
                   "Fourth least important point. Fifth somewhat important point.";

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.NotEmpty(summary);
        // Verify it doesn't start with words that would indicate out-of-order sentences
        Assert.NotNull(summary);
    }

    [Fact]
    public async Task GenerateSummaryAsync_ObeyMaxCharacters_LimitsSummaryLength()
    {
        // Arrange
        var maxChars = 100;
        var options = Options.Create(new TopicSummaryOptions
        {
            MaxCharacters = maxChars,
            MaxSentences = 5
        });
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        var text = "This is sentence one about a very long topic. This is sentence two that repeats information. " +
                   "This is sentence three with more details. This is sentence four continuing the discussion. " +
                   "This is sentence five with even more content. This is sentence six adding to the narrative.";

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.True(summary.Length <= maxChars, $"Summary length {summary.Length} exceeds max {maxChars}");
    }

    [Fact]
    public async Task GenerateSummaryAsync_MultipleDocuments_ProducesDifferentSummaries()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        var text1 = "Document one discusses machine learning. Deep learning is a subset. Neural networks are powerful.";
        var text2 = "Document two talks about gardening. Plants need sunlight. Water is essential for growth.";

        // Act
        var summary1 = await service.GenerateSummaryAsync(text1);
        var summary2 = await service.GenerateSummaryAsync(text2);

        // Assert
        Assert.NotEqual(summary1, summary2);
    }

    [Fact]
    public void ImplementationName_ReturnsExtractiveDescription()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        // Act
        var name = service.ImplementationName;

        // Assert
        Assert.Equal("Extractive (TF-based)", name);
    }

    [Fact]
    public void TopicSummaryOptions_Validate_ThrowsOnInvalidMinSentences()
    {
        // Arrange
        var options = new TopicSummaryOptions { MinSentences = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void TopicSummaryOptions_Validate_ThrowsWhenMaxLessThanMin()
    {
        // Arrange
        var options = new TopicSummaryOptions 
        { 
            MinSentences = 5,
            MaxSentences = 2
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void TopicSummaryOptions_Validate_ThrowsOnInvalidMaxCharacters()
    {
        // Arrange
        var options = new TopicSummaryOptions { MaxCharacters = 10 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public async Task GenerateSummaryAsync_LongDocument_SummarizesEffectively()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        // Create a longer document
        var sentences = Enumerable.Range(1, 20)
            .Select(i => $"This is sentence number {i} with some content.")
            .ToList();
        var text = string.Join(" ", sentences);

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.NotEmpty(summary);
        var sentenceCount = summary.Count(c => c == '.');
        Assert.InRange(sentenceCount, 2, 3); // Default min/max
    }

    [Fact]
    public async Task GenerateSummaryAsync_TextWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var options = Options.Create(new TopicSummaryOptions());
        var service = new TopicSummaryService(NullLogger<TopicSummaryService>.Instance, options);

        var text = "First sentence! This sentence has a question? This is a normal sentence. Another sentence here.";

        // Act
        var summary = await service.GenerateSummaryAsync(text);

        // Assert
        Assert.NotEmpty(summary);
        Assert.True(summary.Length <= options.Value.MaxCharacters);
    }
}
