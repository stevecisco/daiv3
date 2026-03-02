using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

public class SkillSandboxIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPermissionDenied_BlocksSkillExecution()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddDebug());
        services.AddOrchestrationServices();
        services.Configure<SkillSandboxConfiguration>(options =>
        {
            options.DefaultMode = SkillSandboxMode.PermissionChecks;
            options.GlobalAllowedPermissions = new List<string> { SkillPermissions.FileSystemRead };
            options.GlobalDeniedPermissions = new List<string>();
            options.AllowUntrustedSkills = true;
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ISkillRegistry>();
        var executor = provider.GetRequiredService<ISkillExecutor>();

        registry.RegisterSkill(new PermissionedSkill(
            "WriteSkill",
            new List<string> { SkillPermissions.FileSystemWrite }));

        var result = await executor.ExecuteAsync(new SkillExecutionRequest
        {
            SkillName = "WriteSkill",
            Parameters = new Dictionary<string, object>()
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Permission denied", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermissionAllowed_ExecutesSkillSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddDebug());
        services.AddOrchestrationServices();
        services.Configure<SkillSandboxConfiguration>(options =>
        {
            options.DefaultMode = SkillSandboxMode.ResourceLimits;
            options.GlobalAllowedPermissions = new List<string> { SkillPermissions.FileSystemRead };
            options.GlobalDeniedPermissions = new List<string>();
            options.AllowUntrustedSkills = true;
            options.ResourceCheckIntervalMs = 50;
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ISkillRegistry>();
        var executor = provider.GetRequiredService<ISkillExecutor>();

        registry.RegisterSkill(new PermissionedSkill(
            "ReadSkill",
            new List<string> { SkillPermissions.FileSystemRead }));

        var result = await executor.ExecuteAsync(new SkillExecutionRequest
        {
            SkillName = "ReadSkill",
            Parameters = new Dictionary<string, object>()
        });

        Assert.True(result.Success);
        Assert.Equal("ok", result.Output);
        Assert.NotNull(result.ResourceMetrics);
    }

    private sealed class PermissionedSkill : ISkill
    {
        public PermissionedSkill(string name, List<string> permissions)
        {
            Name = name;
            Permissions = permissions;
        }

        public string Name { get; }
        public string Description => "Permission test skill";
        public SkillCategory Category => SkillCategory.Other;
        public List<ParameterMetadata> Inputs => new();
        public OutputSchema OutputSchema => new() { Type = "string", Description = "status" };
        public List<string> Permissions { get; }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            await Task.Delay(25, ct);
            return "ok";
        }
    }
}
