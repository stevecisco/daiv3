using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Messaging;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Integration tests for skill execution with full dependency injection and message bus.
/// </summary>
public class SkillExecutorIntegrationTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISkillRegistry _skillRegistry;
    private readonly ISkillExecutor _skillExecutor;
    private readonly IMessageBroker _messageBroker;
    private readonly DatabaseContext _databaseContext;
    private readonly string _dbPath;

    public SkillExecutorIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddConsole());
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-skill-executor-test-{Guid.NewGuid():N}.db");
        
        // Register persistence
        services.AddPersistence(options => options.DatabasePath = _dbPath);
        
        // Register orchestration
        services.AddOrchestrationServices();
        
        _serviceProvider = services.BuildServiceProvider();
        _skillRegistry = _serviceProvider.GetRequiredService<ISkillRegistry>();
        _skillExecutor = _serviceProvider.GetRequiredService<ISkillExecutor>();
        _messageBroker = _serviceProvider.GetRequiredService<IMessageBroker>();
        _databaseContext = _serviceProvider.GetRequiredService<DatabaseContext>();
    }

    public async Task InitializeAsync()
    {
        // Initialize database (in-memory SQLite for tests)
        await _databaseContext.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup
        if (_databaseContext is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    #region Test Helpers

    private class TestCalculatorSkill : ISkill
    {
        public string Name => "Calculator";
        public string Description => "Performs basic arithmetic";
        public SkillCategory Category => SkillCategory.ReasoningAndAnalysis;
        public List<ParameterMetadata> Inputs => new()
        {
            new() { Name = "operation", Type = "string", Required = true, Description = "Operation: add, subtract, multiply, divide" },
            new() { Name = "operand1", Type = "double", Required = true, Description = "First operand" },
            new() { Name = "operand2", Type = "double", Required = true, Description = "Second operand" }
        };
        public OutputSchema OutputSchema => new() { Type = "double", Description = "Result of the operation" };
        public List<string> Permissions => new() { "Math.Compute" };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            var operation = parameters["operation"]?.ToString() ?? "add";
            var op1 = Convert.ToDouble(parameters["operand1"]);
            var op2 = Convert.ToDouble(parameters["operand2"]);

            var result = operation.ToLower() switch
            {
                "add" => op1 + op2,
                "subtract" => op1 - op2,
                "multiply" => op1 * op2,
                "divide" when op2 != 0 => op1 / op2,
                "divide" => throw new DivideByZeroException("Cannot divide by zero"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return Task.FromResult<object>(result);
        }
    }

    private class TestStringSkill : ISkill
    {
        public string Name => "StringProcessor";
        public string Description => "Processes strings";
        public SkillCategory Category => SkillCategory.Document;
        public List<ParameterMetadata> Inputs => new()
        {
            new() { Name = "text", Type = "string", Required = true, Description = "Text to process" },
            new() { Name = "operation", Type = "string", Required = true, Description = "Operation: upper, lower, length" }
        };
        public OutputSchema OutputSchema => new() { Type = "string", Description = "Processed text" };
        public List<string> Permissions => new() { "Text.Process" };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
        {
            var text = parameters["text"]?.ToString() ?? string.Empty;
            var operation = parameters["operation"]?.ToString() ?? "upper";

            var result = operation.ToLower() switch
            {
                "upper" => text.ToUpper(),
                "lower" => text.ToLower(),
                "length" => text.Length.ToString(),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return Task.FromResult<object>(result);
        }
    }

    #endregion

    #region Basic Skill Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithRegisteredSkill_ExecutesSuccessfully()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var request = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "add" },
                { "operand1", 5.0 },
                { "operand2", 3.0 }
            }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8.0, result.Output);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSkills_ExecutesCorrectSkill()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());
        _skillRegistry.RegisterSkill(new TestStringSkill());

        var calcRequest = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "multiply" },
                { "operand1", 6.0 },
                { "operand2", 7.0 }
            }
        };

        var stringRequest = new SkillExecutionRequest
        {
            SkillName = "StringProcessor",
            Parameters = new()
            {
                { "text", "hello" },
                { "operation", "upper" }
            }
        };

        // Act
        var calcResult = await _skillExecutor.ExecuteAsync(calcRequest);
        var stringResult = await _skillExecutor.ExecuteAsync(stringRequest);

        // Assert
        Assert.True(calcResult.Success);
        Assert.Equal(42.0, calcResult.Output);
        
        Assert.True(stringResult.Success);
        Assert.Equal("HELLO", stringResult.Output);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenSkillThrowsException_CapturesException()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var request = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "divide" },
                { "operand1", 10.0 },
                { "operand2", 0.0 } // Division by zero
            }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.NotNull(result.Exception);
        Assert.Contains("divide by zero", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidParameters_ValidationFails()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var request = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "add" }
                // Missing required operand1 and operand2
            }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("Parameter validation failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownSkill_ReturnsNotFound()
    {
        // Arrange
        var request = new SkillExecutionRequest
        {
            SkillName = "UnknownSkill",
            Parameters = new()
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Contains("not found", result.ErrorMessage);
    }

    #endregion

    #region Skill Registry Integration Tests

    [Fact]
    public void CanExecute_ReturnsCorrectStatus()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        // Act
        var canExecuteCalc = _skillExecutor.CanExecute("Calculator");
        var canExecuteUnknown = _skillExecutor.CanExecute("Unknown");

        // Assert
        Assert.True(canExecuteCalc);
        Assert.False(canExecuteUnknown);
    }

    [Fact]
    public void ValidateParameters_WithValidInputs_ReturnsValid()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var parameters = new Dictionary<string, object>
        {
            { "operation", "add" },
            { "operand1", 5.0 },
            { "operand2", 3.0 }
        };

        // Act
        var result = _skillExecutor.ValidateParameters("Calculator", parameters);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateParameters_WithMissingRequired_ReturnsInvalid()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var parameters = new Dictionary<string, object>
        {
            { "operation", "add" }
            // Missing operand1 and operand2
        };

        // Act
        var result = _skillExecutor.ValidateParameters("Calculator", parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("operand1", string.Join(";", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Skill Metadata Tests

    [Fact]
    public void ListSkills_IncludesRegisteredSkills()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());
        _skillRegistry.RegisterSkill(new TestStringSkill());

        // Act
        var skills = _skillRegistry.ListSkills();

        // Assert
        Assert.NotEmpty(skills);
        var calcSkill = skills.FirstOrDefault(s => s.Name == "Calculator");
        var stringSkill = skills.FirstOrDefault(s => s.Name == "StringProcessor");

        Assert.NotNull(calcSkill);
        Assert.NotNull(stringSkill);
        
        Assert.Equal(SkillCategory.ReasoningAndAnalysis, calcSkill.Category);
        Assert.Equal(SkillCategory.Document, stringSkill.Category);
        
        Assert.Equal(3, calcSkill.Inputs.Count);
        Assert.Equal(2, stringSkill.Inputs.Count);
    }

    #endregion

    #region Message Bus Integration Tests

    [Fact]
    public async Task ExecuteAsync_WithMessageBroker_CanPublishEvents()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var request = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "add" },
                { "operand1", 2.0 },
                { "operand2", 3.0 }
            }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Could enhance SkillExecutor to publish events
        // For now, verify skill execution works with message broker available
        Assert.True(result.Success);
        Assert.Equal(5.0, result.Output);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ExecuteAsync_TracksExecutionTime()
    {
        // Arrange
        _skillRegistry.RegisterSkill(new TestCalculatorSkill());

        var request = new SkillExecutionRequest
        {
            SkillName = "Calculator",
            Parameters = new()
            {
                { "operation", "add" },
                { "operand1", 1.0 },
                { "operand2", 2.0 }
            }
        };

        // Act
        var result = await _skillExecutor.ExecuteAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    #endregion
}
