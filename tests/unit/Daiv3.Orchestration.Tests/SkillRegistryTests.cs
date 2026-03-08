using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for SkillRegistry.
/// </summary>
public class SkillRegistryTests
{
    private readonly Mock<ILogger<SkillRegistry>> _mockLogger;
    private readonly SkillRegistry _registry;

    public SkillRegistryTests()
    {
        _mockLogger = new Mock<ILogger<SkillRegistry>>();
        _registry = new SkillRegistry(_mockLogger.Object);
    }

    [Fact]
    public void RegisterSkill_WithValidSkill_AddsToRegistry()
    {
        // Arrange
        var skill = new TestSkill("TestSkill", "A test skill");

        // Act
        _registry.RegisterSkill(skill);

        // Assert
        var resolved = _registry.ResolveSkill("TestSkill");
        Assert.NotNull(resolved);
        Assert.Equal("TestSkill", resolved.Name);
    }

    [Fact]
    public void RegisterSkill_WithNullSkill_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _registry.RegisterSkill(null!));
    }

    [Fact]
    public void RegisterSkill_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var skill = new TestSkill("", "Description");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.RegisterSkill(skill));
    }

    [Fact]
    public void RegisterSkill_WithDuplicateName_ReplacesExisting()
    {
        // Arrange
        var skill1 = new TestSkill("Skill", "First version");
        var skill2 = new TestSkill("Skill", "Second version");

        // Act
        _registry.RegisterSkill(skill1);
        _registry.RegisterSkill(skill2);

        // Assert
        var resolved = _registry.ResolveSkill("Skill");
        Assert.NotNull(resolved);
        Assert.Equal("Second version", resolved.Description);
    }

    [Fact]
    public void RegisterSkill_IsCaseInsensitive()
    {
        // Arrange
        var skill = new TestSkill("TestSkill", "A test skill");

        // Act
        _registry.RegisterSkill(skill);

        // Assert
        Assert.NotNull(_registry.ResolveSkill("testskill"));
        Assert.NotNull(_registry.ResolveSkill("TESTSKILL"));
        Assert.NotNull(_registry.ResolveSkill("TestSkill"));
    }

    [Fact]
    public void ResolveSkill_WithNonExistentSkill_ReturnsNull()
    {
        // Act
        var skill = _registry.ResolveSkill("NonExistent");

        // Assert
        Assert.Null(skill);
    }

    [Fact]
    public void UnregisterSkill_WithExistingSkill_RemovesIt()
    {
        // Arrange
        _registry.RegisterSkill(new TestSkill("TransientSkill", "Temporary"));

        // Act
        var removed = _registry.UnregisterSkill("TransientSkill");

        // Assert
        Assert.True(removed);
        Assert.Null(_registry.ResolveSkill("TransientSkill"));
    }

    [Fact]
    public void ResolveSkill_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _registry.ResolveSkill(null!));
    }

    [Fact]
    public void ResolveSkill_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _registry.ResolveSkill(""));
    }

    [Fact]
    public void ListSkills_WithNoSkills_ReturnsEmptyList()
    {
        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.NotNull(skills);
        Assert.Empty(skills);
    }

    [Fact]
    public void ListSkills_WithMultipleSkills_ReturnsAllMetadata()
    {
        // Arrange
        _registry.RegisterSkill(new TestSkill("Skill1", "First skill"));
        _registry.RegisterSkill(new TestSkill("Skill2", "Second skill"));
        _registry.RegisterSkill(new TestSkill("Skill3", "Third skill"));

        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.Equal(3, skills.Count);
        Assert.Contains(skills, s => s.Name == "Skill1");
        Assert.Contains(skills, s => s.Name == "Skill2");
        Assert.Contains(skills, s => s.Name == "Skill3");
    }

    [Fact]
    public void ListSkills_ReturnsSortedByName()
    {
        // Arrange
        _registry.RegisterSkill(new TestSkill("Zebra", "Last"));
        _registry.RegisterSkill(new TestSkill("Alpha", "First"));
        _registry.RegisterSkill(new TestSkill("Mike", "Middle"));

        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.Equal("Alpha", skills[0].Name);
        Assert.Equal("Mike", skills[1].Name);
        Assert.Equal("Zebra", skills[2].Name);
    }

    [Fact]
    public async Task ExecuteSkill_ReturnsExpectedResult()
    {
        // Arrange
        var skill = new TestSkill("TestSkill", "A test skill");
        _registry.RegisterSkill(skill);

        var parameters = new Dictionary<string, object>
        {
            ["param1"] = "value1"
        };

        // Act
        var resolved = _registry.ResolveSkill("TestSkill");
        var result = await resolved!.ExecuteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestSkill executed", result);
    }

    [Fact]
    public void ListSkills_IncludesCategory()
    {
        // Arrange
        var skill = new TestSkill(
            "CodeReviewSkill",
            "Reviews code",
            category: SkillCategory.Code);
        _registry.RegisterSkill(skill);

        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.Single(skills);
        Assert.Equal(SkillCategory.Code, skills[0].Category);
    }

    [Fact]
    public void ListSkills_IncludesOutputSchema()
    {
        // Arrange
        var outputSchema = new OutputSchema
        {
            Type = "object",
            Description = "Analysis report",
            Schema = "{\"type\": \"object\", \"properties\": {\"score\": {\"type\": \"number\"}}}"
        };
        var skill = new TestSkill(
            "AnalysisSkill",
            "Analyzes data",
            outputSchema: outputSchema);
        _registry.RegisterSkill(skill);

        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.Single(skills);
        Assert.NotNull(skills[0].Outputs);
        Assert.Equal("object", skills[0].Outputs.Type);
        Assert.Equal("Analysis report", skills[0].Outputs.Description);
        Assert.Contains("score", skills[0].Outputs.Schema);
    }

    [Fact]
    public void ListSkills_IncludesPermissions()
    {
        // Arrange
        var permissions = new List<string> { "FileSystem.Read", "Network.Access" };
        var skill = new TestSkill(
            "WebFetchSkill",
            "Fetches web content",
            permissions: permissions);
        _registry.RegisterSkill(skill);

        // Act
        var skills = _registry.ListSkills();

        // Assert
        Assert.Single(skills);
        Assert.Equal(2, skills[0].Permissions.Count);
        Assert.Contains("FileSystem.Read", skills[0].Permissions);
        Assert.Contains("Network.Access", skills[0].Permissions);
    }

    [Fact]
    public void ListSkills_WithAllMetadata_PopulatesComplete()
    {
        // Arrange
        var outputSchema = new OutputSchema
        {
            Type = "string",
            Description = "Generated document"
        };
        var permissions = new List<string> { "FileSystem.Write" };
        var skill = new TestSkill(
            "DocumentGenerator",
            "Generates documents",
            category: SkillCategory.Document,
            outputSchema: outputSchema,
            permissions: permissions);
        _registry.RegisterSkill(skill);

        // Act
        var skills = _registry.ListSkills();

        // Assert
        var metadata = skills[0];
        Assert.Equal("DocumentGenerator", metadata.Name);
        Assert.Equal("Generates documents", metadata.Description);
        Assert.Equal(SkillCategory.Document, metadata.Category);
        Assert.NotNull(metadata.Outputs);
        Assert.Equal("string", metadata.Outputs.Type);
        Assert.Equal("Generated document", metadata.Outputs.Description);
        Assert.Single(metadata.Permissions);
        Assert.Contains("FileSystem.Write", metadata.Permissions);
    }

    [Fact]
    public void SkillMetadata_InputsProperty_IsAlias()
    {
        // Arrange
        var metadata = new SkillMetadata
        {
            Name = "Test",
            Description = "Test skill",
            Outputs = new OutputSchema { Type = "string" }
        };

        var parameter = new ParameterMetadata
        {
            Name = "input1",
            Type = "string",
            Required = true
        };

        // Act
        metadata.Inputs.Add(parameter);

        // Assert
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Single(metadata.Parameters);
        Assert.Equal("input1", metadata.Parameters[0].Name);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Test skill implementation for unit testing.
    /// </summary>
    private class TestSkill : ISkill
    {
        public TestSkill(
            string name,
            string description,
            SkillCategory category = SkillCategory.Unspecified,
            OutputSchema? outputSchema = null,
            List<string>? permissions = null,
            List<ParameterMetadata>? inputs = null)
        {
            Name = name;
            Description = description;
            Category = category;
            Inputs = inputs ?? new List<ParameterMetadata>();
            OutputSchema = outputSchema ?? new OutputSchema { Type = "string", Description = "Default output" };
            Permissions = permissions ?? new List<string>();
        }

        public string Name { get; }
        public string Description { get; }
        public SkillCategory Category { get; }
        public List<ParameterMetadata> Inputs { get; }
        public OutputSchema OutputSchema { get; }
        public List<string> Permissions { get; }

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            return Task.FromResult<object>($"{Name} executed");
        }
    }
}
