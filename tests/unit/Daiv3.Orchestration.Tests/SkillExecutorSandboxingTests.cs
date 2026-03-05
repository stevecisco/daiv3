using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for SkillExecutor sandboxing integration.
/// Tests permission enforcement and resource monitoring during skill execution.
/// </summary>
public class SkillExecutorSandboxingTests
{
    private readonly Mock<ISkillRegistry> _registryMock;
    private readonly Mock<ILogger<SkillExecutor>> _executorLoggerMock;
    private readonly Mock<ILogger<SkillResourceMonitor>> _monitorLoggerMock;
    private readonly Mock<ILogger<SkillPermissionValidator>> _validatorLoggerMock;
    private readonly OrchestrationOptions _orchestrationOptions;
    private readonly SkillSandboxConfiguration _sandboxConfig;
    private readonly SkillPermissionValidator _permissionValidator;
    private readonly SkillExecutor _executor;

    public SkillExecutorSandboxingTests()
    {
        _registryMock = new Mock<ISkillRegistry>();
        _executorLoggerMock = new Mock<ILogger<SkillExecutor>>();
        _monitorLoggerMock = new Mock<ILogger<SkillResourceMonitor>>();
        _validatorLoggerMock = new Mock<ILogger<SkillPermissionValidator>>();

        _orchestrationOptions = new OrchestrationOptions();
        _sandboxConfig = new SkillSandboxConfiguration
        {
            DefaultMode = SkillSandboxMode.PermissionChecks,
            GlobalAllowedPermissions = { SkillPermissions.FileSystemRead },
            ResourceCheckIntervalMs = 100
        };

        _permissionValidator = new SkillPermissionValidator(
            _validatorLoggerMock.Object,
            Options.Create(_sandboxConfig));

        _executor = new SkillExecutor(
            _registryMock.Object,
            _executorLoggerMock.Object,
            _monitorLoggerMock.Object,
            Options.Create(_orchestrationOptions),
            Options.Create(_sandboxConfig),
            _permissionValidator);
    }

    #region Permission Enforcement Tests

    [Fact]
    public async Task ExecuteAsync_SandboxModeNone_SkipsPermissionCheck()
    {
        // Arrange
        _sandboxConfig.SkillOverrides["TestSkill"] = new SkillSandboxOverride
        {
            Mode = SkillSandboxMode.None
        };

        var skill = new TestSkill("TestSkill", new List<string>
        {
            SkillPermissions.DatabaseWrite // Would be denied if checked
        });

        _registryMock.Setup(r => r.ResolveSkill("TestSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("TestSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "TestSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Test output", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_PermissionDenied_ReturnsError()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.PermissionChecks;
        _sandboxConfig.GlobalDeniedPermissions.Add(SkillPermissions.DatabaseWrite);

        var skill = new TestSkill("BlockedSkill", new List<string>
        {
            SkillPermissions.DatabaseWrite
        });

        _registryMock.Setup(r => r.ResolveSkill("BlockedSkill")).Returns(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "BlockedSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Permission denied", result.ErrorMessage);
        Assert.Contains("Database.Write", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PermissionAllowed_Executes()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.PermissionChecks;
        _sandboxConfig.GlobalAllowedPermissions.Clear();
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemRead);

        var skill = new TestSkill("AllowedSkill", new List<string>
        {
            SkillPermissions.FileSystemRead
        });

        _registryMock.Setup(r => r.ResolveSkill("AllowedSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("AllowedSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "AllowedSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Test output", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WildcardPermission_AllowsSubPermissions()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.PermissionChecks;
        _sandboxConfig.GlobalAllowedPermissions.Clear();
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemAll);

        var skill = new TestSkill("FileSkill", new List<string>
        {
            SkillPermissions.FileSystemRead,
            SkillPermissions.FileSystemWrite
        });

        _registryMock.Setup(r => r.ResolveSkill("FileSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("FileSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "FileSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region Resource Monitoring Tests

    [Fact]
    public async Task ExecuteAsync_ResourceLimitsMode_IncludesMetrics()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.ResourceLimits;
        _sandboxConfig.GlobalAllowedPermissions.Clear();
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemRead);

        var skill = new TestSkill("MonitoredSkill", new List<string>
        {
            SkillPermissions.FileSystemRead
        });

        skill.ExecuteFunc = async (_, _) =>
        {
            await Task.Delay(100);
            return "Monitored output";
        };

        _registryMock.Setup(r => r.ResolveSkill("MonitoredSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("MonitoredSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "MonitoredSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ResourceMetrics);
        Assert.True(result.ResourceMetrics.ExecutionDuration.TotalMilliseconds >= 100);
        Assert.False(result.ResourceMetrics.LimitsExceeded);
    }

    [Fact]
    public async Task ExecuteAsync_PermissionChecksMode_NoResourceMetrics()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.PermissionChecks;
        _sandboxConfig.GlobalAllowedPermissions.Clear();
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemRead);

        var skill = new TestSkill("UnmonitoredSkill", new List<string>
        {
            SkillPermissions.FileSystemRead
        });

        _registryMock.Setup(r => r.ResolveSkill("UnmonitoredSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("UnmonitoredSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "UnmonitoredSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ResourceMetrics);
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public async Task ExecuteAsync_SkillNotFound_ReturnsErrorRegardlessOfSandbox()
    {
        // Arrange
        _registryMock.Setup(r => r.ResolveSkill("NonExistent")).Returns((ISkill?)null);

        var request = new SkillExecutionRequest
        {
            SkillName = "NonExistent",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ParameterValidationFailure_ReturnsErrorBeforePermissionCheck()
    {
        // Arrange
        var skill = new TestSkill("ParamSkill", new List<string>
        {
            SkillPermissions.FileSystemRead
        });

        _registryMock.Setup(r => r.ResolveSkill("ParamSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata(
                "ParamSkill",
                new List<ParameterMetadata>
                {
                    new() { Name = "requiredParam", Required = true, Type = "string" }
                })
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "ParamSkill",
            Parameters = new() // Missing required parameter
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Parameter validation failed", result.ErrorMessage);
    }

    #endregion

    #region Per-Skill Override Tests

    [Fact]
    public async Task ExecuteAsync_SkillOverride_UsesCustomPermissions()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.PermissionChecks;
        _sandboxConfig.GlobalAllowedPermissions.Clear();
        _sandboxConfig.GlobalAllowedPermissions.Add(SkillPermissions.FileSystemRead);

        _sandboxConfig.SkillOverrides["PrivilegedSkill"] = new SkillSandboxOverride
        {
            AllowedPermissions = new List<string>
            {
                SkillPermissions.FileSystemRead,
                SkillPermissions.DatabaseWrite
            }
        };

        var skill = new TestSkill("PrivilegedSkill", new List<string>
        {
            SkillPermissions.DatabaseWrite // Normally denied, but override allows it
        });

        _registryMock.Setup(r => r.ResolveSkill("PrivilegedSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("PrivilegedSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "PrivilegedSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_SkillOverride_CanDowngradeToNone()
    {
        // Arrange
        _sandboxConfig.DefaultMode = SkillSandboxMode.ResourceLimits;
        _sandboxConfig.SkillOverrides["TrustedSkill"] = new SkillSandboxOverride
        {
            Mode = SkillSandboxMode.None
        };

        var skill = new TestSkill("TrustedSkill", new List<string>
        {
            SkillPermissions.All // Would normally be denied
        });

        _registryMock.Setup(r => r.ResolveSkill("TrustedSkill")).Returns(skill);
        _registryMock.Setup(r => r.ListSkills()).Returns(new List<SkillMetadata>
        {
            CreateSkillMetadata("TrustedSkill")
        });

        var request = new SkillExecutionRequest
        {
            SkillName = "TrustedSkill",
            Parameters = new()
        };

        // Act
        var result = await _executor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ResourceMetrics); // No monitoring for Mode.None
    }

    #endregion

    #region Standard Permission Constants Tests

    [Fact]
    public void SkillPermissions_AllConstantsAreDefined()
    {
        // Assert all standard permissions are non-null and non-empty
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.FileSystemRead));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.FileSystemWrite));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.FileSystemAll));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.NetworkAccess));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.RegistryRead));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.RegistryWrite));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.ProcessExecute));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.DatabaseRead));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.DatabaseWrite));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.McpInvoke));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.UiAutomation));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.SystemConfiguration));
        Assert.False(string.IsNullOrWhiteSpace(SkillPermissions.All));
    }

    [Fact]
    public void SkillPermissions_FileSystemWildcard_EndsWithStar()
    {
        // Assert
        Assert.EndsWith(".*", SkillPermissions.FileSystemAll);
    }

    [Fact]
    public void SkillPermissions_AllWildcard_IsSingleStar()
    {
        // Assert
        Assert.Equal("*", SkillPermissions.All);
    }

    #endregion

    #region Test Helper Classes

    private static SkillMetadata CreateSkillMetadata(string name, List<ParameterMetadata>? inputs = null)
    {
        return new SkillMetadata
        {
            Name = name,
            Description = $"Metadata for {name}",
            Inputs = inputs ?? new List<ParameterMetadata>(),
            Outputs = new OutputSchema { Type = "object", Description = "Test output" }
        };
    }

    private class TestSkill : ISkill
    {
        public string Name { get; }
        public string Description => "Test skill for sandboxing";
        public SkillCategory Category => SkillCategory.Other;
        public List<ParameterMetadata> Inputs => new();
        public OutputSchema OutputSchema => new() { Type = "object" };
        public List<string> Permissions { get; }

        public Func<Dictionary<string, object>, CancellationToken, Task<object>>? ExecuteFunc { get; set; }

        public TestSkill(string name, List<string> permissions)
        {
            Name = name;
            Permissions = permissions;
        }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            if (ExecuteFunc != null)
            {
                return await ExecuteFunc(parameters, ct);
            }
            return "Test output";
        }
    }

    #endregion
}
