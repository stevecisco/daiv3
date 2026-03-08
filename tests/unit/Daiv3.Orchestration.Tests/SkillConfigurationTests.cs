using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for skill configuration loading and validation.
/// </summary>
public class SkillConfigurationTests
{
    [Fact]
    public void ValidateConfiguration_WithValidConfig_IsValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var config = new SkillConfigurationFile
        {
            Name = "ValidSkill",
            Description = "A valid skill",
            Output = new SkillOutputSchemaConfiguration { Type = "string" }
        };

        // Act
        var result = loader.ValidateConfiguration(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguration_MissingName_IsInvalid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var config = new SkillConfigurationFile
        {
            Name = "",
            Description = "A skill",
            Output = new SkillOutputSchemaConfiguration { Type = "string" }
        };

        // Act
        var result = loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguration_MissingOutput_IsInvalid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var config = new SkillConfigurationFile
        {
            Name = "TestSkill",
            Description = "Test",
            Output = null
        };

        // Act
        var result = loader.ValidateConfiguration(config);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ToSkillMetadata_ConvertConfig_PopulatesAllFields()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var config = new SkillConfigurationFile
        {
            Name = "TestSkill",
            Description = "Test skill",
            Category = "Code",
            Source = "UserDefined",
            Inputs = new List<SkillParameterConfiguration>
            {
                new() { Name = "input1", Type = "string", Required = true }
            },
            Output = new SkillOutputSchemaConfiguration
            {
                Type = "string",
                Description = "Output"
            },
            Permissions = new List<string> { "FileSystem.Read" }
        };

        // Act
        var metadata = loader.ToSkillMetadata(config);

        // Assert
        Assert.Equal("TestSkill", metadata.Name);
        Assert.Equal("Test skill", metadata.Description);
        Assert.Equal(SkillCategory.Code, metadata.Category);
        Assert.Equal(SkillSource.UserDefined, metadata.Source);
        Assert.Single(metadata.Inputs);
        Assert.Equal("string", metadata.Outputs.Type);
        Assert.Contains("FileSystem.Read", metadata.Permissions);
    }

    [Fact]
    public void SkillConfigurationFile_DefaultValues_SetCorrectly()
    {
        // Arrange & Act
        var config = new SkillConfigurationFile
        {
            Name = "MinimalSkill",
            Description = "Minimal"
        };

        // Assert
        Assert.Equal("UserDefined", config.Source);
        Assert.Equal("Other", config.Category);
        Assert.Empty(config.Permissions);
        Assert.Empty(config.Inputs);
    }

    [Fact]
    public void SkillOutputSchemaConfiguration_PopulatesCorrectly()
    {
        // Arrange & Act
        var output = new SkillOutputSchemaConfiguration
        {
            Type = "array",
            Description = "Array of items",
            Schema = "{\"type\": \"array\"}"
        };

        // Assert
        Assert.Equal("array", output.Type);
        Assert.Equal("Array of items", output.Description);
        Assert.NotNull(output.Schema);
    }

    [Fact]
    public void SkillParameterConfiguration_PopulatesCorrectly()
    {
        // Arrange & Act
        var param = new SkillParameterConfiguration
        {
            Name = "iterations",
            Type = "int",
            Required = true,
            Description = "Number of iterations"
        };

        // Assert
        Assert.Equal("iterations", param.Name);
        Assert.Equal("int", param.Type);
        Assert.True(param.Required);
        Assert.Equal("Number of iterations", param.Description);
    }

    [Fact]
    public async Task LoadSkillConfigAsync_Markdown_FirstTwoLinesBecomeNameAndDescription()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var filePath = Path.Combine(Path.GetTempPath(), $"skill-md-{Guid.NewGuid():N}.md");
        try
        {
            var content = """
SkillCreator
Creates child skills from natural language prompts.

## Metadata
scope: Project
project: Daiv3
capabilities: scaffold,metadata-normalization
restrictions: no-runtime-code

## Inputs
- prompt|string|true|The requested capability

## Output
type: object
description: Generated skill draft
""";

            await File.WriteAllTextAsync(filePath, content);
            var config = await loader.LoadSkillConfigAsync(filePath);

            Assert.Equal("SkillCreator", config.Name);
            Assert.Equal("Creates child skills from natural language prompts.", config.Description);
            Assert.Equal("Project", config.ScopeLevel);
            Assert.Equal("Daiv3", config.ProjectId);
            Assert.Contains("scaffold", config.Capabilities);
            Assert.Contains("no-runtime-code", config.Restrictions);
            Assert.Single(config.Inputs);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task LoadSkillBatchAsync_Directory_ComposesHierarchyByScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var directory = Path.Combine(Path.GetTempPath(), $"skill-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var globalFile = Path.Combine(directory, "Writer.global.md");
            var taskFile = Path.Combine(directory, "Writer.task.md");

            await File.WriteAllTextAsync(globalFile, """
Writer
Base writing skill.

## Metadata
scope: Global
capabilities: summarize

## Output
type: object
description: Base output
""");

            await File.WriteAllTextAsync(taskFile, """
Writer
Task scoped writing variant.

## Metadata
scope: Task
task: ES-ACC-003
capabilities: summarize,skill-authoring
""");

            var batch = await loader.LoadSkillBatchAsync(directory, recursive: false);
            var effective = Assert.Single(batch.Skills);

            Assert.Equal("Writer", effective.Name);
            Assert.Equal("Task scoped writing variant.", effective.Description);
            Assert.Equal("Task", effective.ScopeLevel);
            Assert.Equal("ES-ACC-003", effective.TaskId);
            Assert.Contains("summarize", effective.Capabilities);
            Assert.Contains("skill-authoring", effective.Capabilities);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadSkillCatalogAsync_SearchByCapability_ReturnsMatches()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SkillConfigFileLoader>>();
        var loader = new SkillConfigFileLoader(logger);

        var directory = Path.Combine(Path.GetTempPath(), $"skill-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var file = Path.Combine(directory, "Analyzer.md");
            await File.WriteAllTextAsync(file, """
Analyzer
Analyzes requirements and produces testable implementation notes.

## Metadata
scope: Global
domain: Architecture
capabilities: analysis,test-planning
""");

            var catalog = await loader.LoadSkillCatalogAsync(directory, recursive: false);
            var matches = catalog.Search(capability: "analysis");

            var entry = Assert.Single(matches);
            Assert.Equal("Analyzer", entry.Name);
            Assert.Equal("Architecture", entry.Domain);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
