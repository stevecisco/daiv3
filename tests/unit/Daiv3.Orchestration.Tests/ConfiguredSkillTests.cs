using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public class ConfiguredSkillTests
{
    [Fact]
    public async Task ExecuteAsync_WithResponseTemplate_InterpolatesParameters()
    {
        // Arrange
        var metadata = new SkillMetadata
        {
            Name = "GreetingSkill",
            Description = "Returns a greeting",
            Category = SkillCategory.Communication,
            Outputs = new OutputSchema { Type = "string" },
            Inputs = new List<ParameterMetadata>
            {
                new() { Name = "name", Type = "string", Required = true }
            }
        };

        var skill = new ConfiguredSkill(
            metadata,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["response_template"] = "Hello {{name}}"
            });

        // Act
        var output = await skill.ExecuteAsync(new Dictionary<string, object>
        {
            ["name"] = "World"
        });

        // Assert
        Assert.Equal("Hello World", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTemplate_ReturnsStructuredPayload()
    {
        // Arrange
        var metadata = new SkillMetadata
        {
            Name = "EchoSkill",
            Description = "Echoes inputs",
            Category = SkillCategory.Other,
            Outputs = new OutputSchema { Type = "object" }
        };

        var skill = new ConfiguredSkill(metadata);

        // Act
        var output = await skill.ExecuteAsync(new Dictionary<string, object>
        {
            ["value"] = 42
        });

        // Assert
        var payload = Assert.IsType<Dictionary<string, object>>(output);
        Assert.Equal("EchoSkill", payload["skill"]);
        Assert.Equal("ok", payload["status"]);
    }
}
