using Daiv3.Orchestration.Services;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public sealed class DockerSkillIsolationPolicyServiceTests
{
    private readonly DockerSkillIsolationPolicyService _service;

    public DockerSkillIsolationPolicyServiceTests()
    {
        var logger = new Mock<ILogger<DockerSkillIsolationPolicyService>>();
        _service = new DockerSkillIsolationPolicyService(logger.Object);
    }

    [Fact]
    public async Task ValidateExecutionPolicyAsync_NoMetadata_AllowsExecution()
    {
        var skill = new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = "NoMetadataSkill",
            MetadataPath = string.Empty
        };

        var result = await _service.ValidateExecutionPolicyAsync(skill);

        Assert.True(result.IsExecutionAllowed);
        Assert.False(result.RequiresIsolatedEnvironment);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task ValidateExecutionPolicyAsync_MetadataFlagFalse_AllowsExecution()
    {
        var metadataFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(metadataFile, "requiresIsolatedEnvironment: false");

        try
        {
            var skill = new ExecutableSkill
            {
                SkillId = Guid.NewGuid().ToString(),
                Name = "NormalSkill",
                MetadataPath = metadataFile
            };

            var result = await _service.ValidateExecutionPolicyAsync(skill);

            Assert.True(result.IsExecutionAllowed);
            Assert.False(result.RequiresIsolatedEnvironment);
            Assert.Null(result.Reason);
        }
        finally
        {
            File.Delete(metadataFile);
        }
    }

    [Fact]
    public async Task ValidateExecutionPolicyAsync_MetadataFlagTrue_DeniesExecutionWithGuidance()
    {
        var metadataFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(metadataFile, "requiresIsolatedEnvironment: true");

        try
        {
            var skill = new ExecutableSkill
            {
                SkillId = Guid.NewGuid().ToString(),
                Name = "IsolatedSkill",
                MetadataPath = metadataFile
            };

            var result = await _service.ValidateExecutionPolicyAsync(skill);

            Assert.False(result.IsExecutionAllowed);
            Assert.True(result.RequiresIsolatedEnvironment);
            Assert.NotNull(result.Reason);
            Assert.Contains("Docker", result.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("requiresIsolatedEnvironment", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(metadataFile);
        }
    }
}
