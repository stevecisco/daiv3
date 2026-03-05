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
}
