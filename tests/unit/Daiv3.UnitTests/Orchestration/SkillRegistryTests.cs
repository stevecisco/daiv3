using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

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

    /// <summary>
    /// Test skill implementation for unit testing.
    /// </summary>
    private class TestSkill : ISkill
    {
        public TestSkill(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            return Task.FromResult<object>($"{Name} executed");
        }
    }
}
