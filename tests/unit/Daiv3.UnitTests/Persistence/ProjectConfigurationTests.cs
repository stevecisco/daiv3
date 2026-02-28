using Daiv3.Persistence;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

public class ProjectConfigurationTests
{
    [Fact]
    public void Parse_WithNullOrWhitespace_ReturnsEmptyConfiguration()
    {
        var nullConfig = ProjectConfiguration.Parse(null);
        var emptyConfig = ProjectConfiguration.Parse("   ");

        Assert.Null(nullConfig.Instructions);
        Assert.NotNull(nullConfig.ModelPreferences);
        Assert.Null(emptyConfig.Instructions);
        Assert.NotNull(emptyConfig.ModelPreferences);
    }

    [Fact]
    public void Parse_WithInvalidJson_ReturnsEmptyConfiguration()
    {
        var config = ProjectConfiguration.Parse("not-json");

        Assert.Null(config.Instructions);
        Assert.NotNull(config.ModelPreferences);
        Assert.Null(config.ModelPreferences.PreferredModelId);
        Assert.Null(config.ModelPreferences.FallbackModelId);
    }

    [Fact]
    public void ToJsonOrNull_WithNoValues_ReturnsNull()
    {
        var config = new ProjectConfiguration();

        var json = config.ToJsonOrNull();

        Assert.Null(json);
    }

    [Fact]
    public void ToJsonOrNull_WithValues_ProducesRoundTrippablePayload()
    {
        var config = new ProjectConfiguration
        {
            Instructions = "  Keep responses concise.  ",
            ModelPreferences = new ProjectModelPreferences
            {
                PreferredModelId = "  phi-4-mini ",
                FallbackModelId = "  gpt-4o-mini  "
            }
        };

        var json = config.ToJsonOrNull();

        Assert.NotNull(json);
        Assert.Contains("instructions", json);
        Assert.Contains("modelPreferences", json);

        var parsed = ProjectConfiguration.Parse(json);
        Assert.Equal("Keep responses concise.", parsed.Instructions);
        Assert.Equal("phi-4-mini", parsed.ModelPreferences.PreferredModelId);
        Assert.Equal("gpt-4o-mini", parsed.ModelPreferences.FallbackModelId);
    }

    [Fact]
    public void ToJsonOrNull_WithWhitespaceOnlyValues_DropsEmptyFields()
    {
        var config = new ProjectConfiguration
        {
            Instructions = "  ",
            ModelPreferences = new ProjectModelPreferences
            {
                PreferredModelId = "  ",
                FallbackModelId = "  "
            }
        };

        var json = config.ToJsonOrNull();

        Assert.Null(json);
    }
}
