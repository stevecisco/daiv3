using Daiv3.Orchestration;
using Daiv3.Orchestration.Models;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public class KnowledgePromotionServiceTests
{
    private readonly KnowledgePromotionService _service = new();

    [Fact]
    public void GetSupportedLevels_ReturnsExpectedHierarchyOrder()
    {
        var levels = _service.GetSupportedLevels();

        Assert.Equal(
        [
            KnowledgePromotionLevel.Context,
            KnowledgePromotionLevel.SubTask,
            KnowledgePromotionLevel.Task,
            KnowledgePromotionLevel.SubTopic,
            KnowledgePromotionLevel.Topic,
            KnowledgePromotionLevel.Project,
            KnowledgePromotionLevel.Organization,
            KnowledgePromotionLevel.Internet
        ],
            levels);
    }

    [Fact]
    public void GetEnabledLevels_ExcludesOrganizationFutureLevel()
    {
        var levels = _service.GetEnabledLevels();

        Assert.DoesNotContain(KnowledgePromotionLevel.Organization, levels);
        Assert.Contains(KnowledgePromotionLevel.Internet, levels);
    }

    [Theory]
    [InlineData("Context", KnowledgePromotionLevel.Context)]
    [InlineData("sub-task", KnowledgePromotionLevel.SubTask)]
    [InlineData("SubTopic", KnowledgePromotionLevel.SubTopic)]
    [InlineData("project", KnowledgePromotionLevel.Project)]
    [InlineData("org", KnowledgePromotionLevel.Organization)]
    [InlineData("internet", KnowledgePromotionLevel.Internet)]
    public void TryParseLevel_WithKnownAlias_ReturnsTrue(string input, KnowledgePromotionLevel expected)
    {
        var success = _service.TryParseLevel(input, out var parsed);

        Assert.True(success);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-level")]
    public void TryParseLevel_WithInvalidInput_ReturnsFalse(string input)
    {
        var success = _service.TryParseLevel(input, out _);

        Assert.False(success);
    }

    [Fact]
    public void IsEnabled_ReturnsFalseForOrganization_TrueForProject()
    {
        Assert.False(_service.IsEnabled(KnowledgePromotionLevel.Organization));
        Assert.True(_service.IsEnabled(KnowledgePromotionLevel.Project));
    }
}
