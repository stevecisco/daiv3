using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

public class SkillExecutorTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISkillExecutor _skillExecutor;
    private readonly ISkillRegistry _skillRegistry;

    public SkillExecutorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddDebug());
        services.AddOptions<OrchestrationOptions>();
        services.AddOptions<SkillSandboxConfiguration>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddScoped<SkillPermissionValidator>();
        services.AddScoped<ISkillExecutor, SkillExecutor>();

        _serviceProvider = services.BuildServiceProvider();
        _skillExecutor = _serviceProvider.GetRequiredService<ISkillExecutor>();
        _skillRegistry = _serviceProvider.GetRequiredService<ISkillRegistry>();
    }

    #region Test Helpers

    private class TestSkill : ISkill
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public SkillCategory Category { get; set; } = SkillCategory.ReasoningAndAnalysis;
        public List<ParameterMetadata> Inputs { get; set; } = new();
        public OutputSchema OutputSchema { get; set; } = new() { Type = "string", Description = "Test output" };
        public List<string> Permissions { get; set; } = new();

        public Func<Dictionary<string, object>, CancellationToken, Task<object>>? ExecuteFunc { get; set; }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            if (ExecuteFunc != null)
            {
                return await ExecuteFunc(parameters, ct);
            }
            return "Test output";
        }
    }

    private class TestSkillWithInputs : ISkill
    {
        public string Name => "TestSkillWithInputs";
        public string Description => "Test skill with required inputs";
        public SkillCategory Category => SkillCategory.Code;
        public List<ParameterMetadata> Inputs => new()
        {
            new() { Name = "input1", Type = "string", Required = true, Description = "First input" },
            new() { Name = "input2", Type = "string", Required = false, Description = "Second input" }
        };
        public OutputSchema OutputSchema => new() { Type = "string" };
        public List<string> Permissions => new() { "TestPermission" };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            var input1 = parameters["input1"]?.ToString() ?? "default";
            var input2 = parameters.ContainsKey("input2") ? parameters["input2"]?.ToString() : "optional";
            return Task.FromResult<object>($"Processed: {input1}, {input2}");
        }
    }

    #endregion

    #region Direct Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithValidSkill_ReturnsSuccess()
    {
        // Arrange
        var skill = new TestSkill { Name = "TestSkill" };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "TestSkill",
            Parameters = new(),
            CallerContext = "Unit Test"
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentSkill_ReturnsFailed()
    {
        // Arrange
        var request = new SkillExecutionRequest
        {
            SkillName = "NonExistentSkill",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _skillExecutor.ExecuteAsync(null));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySkillName_ThrowsArgumentException()
    {
        // Arrange
        var request = new SkillExecutionRequest
        {
            SkillName = "",
            Parameters = new()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _skillExecutor.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTimeout_RespectTimeoutDuration()
    {
        // Arrange
        var skill = new TestSkill { Name = "SlowSkill" };
        skill.ExecuteFunc = async (_, ct) =>
        {
            await Task.Delay(5000, ct); // Sleep longer than timeout
            return "Never reached";
        };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "SlowSkill",
            Parameters = new(),
            TimeoutSeconds = 1  // 1 second timeout
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Exception);
        Assert.True(result.ElapsedMilliseconds >= 900); // Allow some tolerance
    }

    [Fact]
    public async Task ExecuteAsync_WithSkillException_ReturnsFailureWithException()
    {
        // Arrange
        var skill = new TestSkill { Name = "FailingSkill" };
        skill.ExecuteFunc = (_, _) =>
        {
            throw new InvalidOperationException("Skill execution failed");
        };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "FailingSkill",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("Skill execution failed", result.ErrorMessage);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_WithCallerContext_LogsContext()
    {
        // Arrange
        var skill = new TestSkill { Name = "TestSkill" };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "TestSkill",
            Parameters = new(),
            CallerContext = "Agent:TaskExecution"
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        // Caller context is logged, not returned in result
    }

    [Fact]
    public async Task ExecuteAsync_WithParameters_PassesToSkill()
    {
        // Arrange
        var passedParams = new Dictionary<string, object>();
        var skill = new TestSkill { Name = "ParamSkill" };
        skill.ExecuteFunc = (parameters, _) =>
        {
            passedParams = parameters;
            return Task.FromResult<object>("Done");
        };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "ParamSkill",
            Parameters = new() { { "key", "value" }, { "number", 42 } }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, passedParams.Count);
        Assert.Equal("value", passedParams["key"]);
        Assert.Equal(42, passedParams["number"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ReturnsCancelled()
    {
        // Arrange
        var skill = new TestSkill { Name = "CancellableSkill" };
        skill.ExecuteFunc = async (_, ct) =>
        {
            await Task.Delay(10000, ct); // Long-running operation
            return "Completed";
        };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "CancellableSkill",
            Parameters = new()
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act
        var result = await _skillExecutor.ExecuteAsync(request, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("timeout", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public void ValidateParameters_WithValidParameters_ReturnsValid()
    {
        // Arrange
        var skill = new TestSkillWithInputs();
        _skillRegistry.RegisterSkill(skill);

        var parameters = new Dictionary<string, object>
        {
            { "input1", "value1" },
            { "input2", "value2" }
        };

        // Act
        var result = _skillExecutor.ValidateParameters("TestSkillWithInputs", parameters);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateParameters_WithMissingRequiredParameter_ReturnsInvalid()
    {
        // Arrange
        var skill = new TestSkillWithInputs();
        _skillRegistry.RegisterSkill(skill);

        var parameters = new Dictionary<string, object>
        {
            { "input2", "value2" } // Missing required input1
        };

        // Act
        var result = _skillExecutor.ValidateParameters("TestSkillWithInputs", parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("input1", string.Join(";", result.Errors));
    }

    [Fact]
    public void ValidateParameters_WithUnknownParameter_ReturnsWarning()
    {
        // Arrange
        var skill = new TestSkillWithInputs();
        _skillRegistry.RegisterSkill(skill);

        var parameters = new Dictionary<string, object>
        {
            { "input1", "value1" },
            { "unknownParam", "value" } // Unknown parameter
        };

        // Act
        var result = _skillExecutor.ValidateParameters("TestSkillWithInputs", parameters);

        // Assert
        Assert.True(result.IsValid); // Still valid despite warning
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("unknownParam", result.Warnings[0]);
    }

    [Fact]
    public void ValidateParameters_WithNonExistentSkill_ReturnsInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "param1", "value1" }
        };

        // Act
        var result = _skillExecutor.ValidateParameters("NonExistent", parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not found", string.Join(";", result.Errors));
    }

    [Fact]
    public void ValidateParameters_WithNullParameters_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => _skillExecutor.ValidateParameters("TestSkill", null));
    }

    [Fact]
    public void ValidateParameters_WithEmptySkillName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => _skillExecutor.ValidateParameters("", new()));
    }

    #endregion

    #region Skill Capability Tests

    [Fact]
    public void CanExecute_WithRegisteredSkill_ReturnsTrue()
    {
        // Arrange
        var skill = new TestSkill { Name = "ExecutableSkill" };
        _skillRegistry.RegisterSkill(skill);

        // Act
        var canExecute = _skillExecutor.CanExecute("ExecutableSkill");

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void CanExecute_WithUnregisteredSkill_ReturnsFalse()
    {
        // Act
        var canExecute = _skillExecutor.CanExecute("NonExistent");

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public void CanExecute_WithNullSkillName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => _skillExecutor.CanExecute(null));
    }

    [Fact]
    public void CanExecute_WithEmptySkillName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => _skillExecutor.CanExecute(""));
    }

    [Fact]
    public void CanExecute_IsCaseInsensitive()
    {
        // Arrange
        var skill = new TestSkill { Name = "TestSkill" };
        _skillRegistry.RegisterSkill(skill);

        // Act
        var lowerCaseCanExecute = _skillExecutor.CanExecute("testskill");
        var upperCaseCanExecute = _skillExecutor.CanExecute("TESTSKILL");
        var mixedCaseCanExecute = _skillExecutor.CanExecute("TeStSkIlL");

        // Assert
        Assert.True(lowerCaseCanExecute);
        Assert.True(upperCaseCanExecute);
        Assert.True(mixedCaseCanExecute);
    }

    #endregion

    #region Edge Cases and Integration

    [Fact]
    public async Task ExecuteAsync_WithMultipleSkills_ExecutesCorrectOne()
    {
        // Arrange
        var skill1 = new TestSkill { Name = "Skill1" };
        var skill2 = new TestSkill { Name = "Skill2" };
        _skillRegistry.RegisterSkill(skill1);
        _skillRegistry.RegisterSkill(skill2);

        skill2.ExecuteFunc = (_, _) => Task.FromResult<object>("Output from Skill2");

        var request = new SkillExecutionRequest
        {
            SkillName = "Skill2",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Output from Skill2", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexOutput_ReturnsOutputAsIs()
    {
        // Arrange
        var skill = new TestSkill { Name = "ComplexSkill" };
        var complexOutput = new { Message = "Test", Count = 42 };
        skill.ExecuteFunc = (_, _) => Task.FromResult<object>(complexOutput);
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "ComplexSkill",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        var outputObj = result.Output as dynamic;
        Assert.Equal("Test", outputObj.Message);
        Assert.Equal(42, outputObj.Count);
    }

    [Fact]
    public async Task ExecuteAsync_TracksExecutionTime()
    {
        // Arrange
        var skill = new TestSkill { Name = "TimedSkill" };
        skill.ExecuteFunc = async (_, _) =>
        {
            await Task.Delay(100);
            return "Done";
        };
        _skillRegistry.RegisterSkill(skill);

        var request = new SkillExecutionRequest
        {
            SkillName = "TimedSkill",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ElapsedMilliseconds >= 100);
    }

    #endregion
}
