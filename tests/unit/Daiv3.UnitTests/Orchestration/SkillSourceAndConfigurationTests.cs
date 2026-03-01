using Daiv3.Orchestration;
using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for skill source tracking in SkillRegistry.
/// Tests that built-in, user-defined, and imported skills are properly tracked.
/// </summary>
public class SkillSourceTrackingTests
{
    [Fact]
    public void RegisterSkill_DefaultsToBuiltIn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.TryAddScoped<ISkillRegistry, SkillRegistry>();
        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<ISkillRegistry>();
        var skill = new TestSkill("TestSkill", "A test skill");

        // Act
        registry.RegisterSkill(skill);

        // Assert
        var metadata = registry.ListSkills();
        var testSkill = metadata.First(s => s.Name == "TestSkill");
        Assert.Equal(SkillSource.BuiltIn, testSkill.Source);
    }

    [Fact]
    public void RegisterSkill_WithBuiltInSource_TracksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);
        var skill = new TestSkill("BuiltInSkill", "A built-in skill");

        // Act
        registry.RegisterSkill(skill, SkillSource.BuiltIn);

        // Assert
        var metadata = registry.ListSkills();
        var registeredSkill = metadata.First(s => s.Name == "BuiltInSkill");
        Assert.Equal(SkillSource.BuiltIn, registeredSkill.Source);
    }

    [Fact]
    public void RegisterSkill_WithUserDefinedSource_TracksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);
        var skill = new TestSkill("CustomSkill", "User-defined skill");

        // Act
        registry.RegisterSkill(skill, SkillSource.UserDefined);

        // Assert
        var metadata = registry.ListSkills();
        var customSkill = metadata.First(s => s.Name == "CustomSkill");
        Assert.Equal(SkillSource.UserDefined, customSkill.Source);
    }

    [Fact]
    public void RegisterSkill_WithImportedSource_TracksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);
        var skill = new TestSkill("ImportedSkill", "Imported skill");

        // Act
        registry.RegisterSkill(skill, SkillSource.Imported);

        // Assert
        var metadata = registry.ListSkills();
        var importedSkill = metadata.First(s => s.Name == "ImportedSkill");
        Assert.Equal(SkillSource.Imported, importedSkill.Source);
    }

    [Fact]
    public void RegisterSkills_WithMixedSources_AllTrackedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);
        var skill1 = new TestSkill("BuiltIn", "Built-in");
        var skill2 = new TestSkill("UserDefined", "User-defined");
        var skill3 = new TestSkill("Imported", "Imported");

        // Act
        registry.RegisterSkill(skill1, SkillSource.BuiltIn);
        registry.RegisterSkill(skill2, SkillSource.UserDefined);
        registry.RegisterSkill(skill3, SkillSource.Imported);

        // Assert
        var metadata = registry.ListSkills();
        Assert.Equal(3, metadata.Count);
        Assert.Equal(SkillSource.BuiltIn, metadata[0].Source);
        Assert.Equal(SkillSource.UserDefined, metadata[1].Source);
        Assert.Equal(SkillSource.Imported, metadata[2].Source);
    }

    [Fact]
    public void GetSkillSource_ReturnsCorrectSource()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);
        var skill = new TestSkill("TestSkill", "Test");
        registry.RegisterSkill(skill, SkillSource.UserDefined);

        // Act
        var source = registry.GetSkillSource("TestSkill");

        // Assert
        Assert.Equal(SkillSource.UserDefined, source);
    }

    [Fact]
    public void GetSkillSource_UnregisteredSkill_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillRegistry>>();
        var registry = new SkillRegistry(logger);

        // Act
        var source = registry.GetSkillSource("NonExistent");

        // Assert
        Assert.Null(source);
    }
}

/// <summary>
/// Test skill implementation for testing purposes.
/// </summary>
public class TestSkill : ISkill
{
    public string Name { get; }
    public string Description { get; }
    public SkillCategory Category { get; } = SkillCategory.Other;
    public List<ParameterMetadata> Inputs { get; } = new();
    public OutputSchema OutputSchema { get; } = new() { Type = "object" };
    public List<string> Permissions { get; } = new();

    public TestSkill(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        return Task.FromResult<object>("test result");
    }
}
