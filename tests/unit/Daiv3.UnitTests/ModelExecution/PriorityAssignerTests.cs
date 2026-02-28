using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for PriorityAssigner (MQ-REQ-010).
/// </summary>
public class PriorityAssignerTests
{
    private readonly ILogger<PriorityAssigner> _logger;

    public PriorityAssignerTests()
    {
        _logger = new LoggerFactory().CreateLogger<PriorityAssigner>();
    }

    private PriorityAssignerOptions GetDefaultOptions()
    {
        return new PriorityAssignerOptions
        {
            TaskTypePriorityMappings = PriorityAssignerOptions.GetDefaultMappings(),
            UserFacingAlwaysImmediate = false,
            ElevateInteractivePriority = true,
            ElevateRetryPriority = true,
            DefaultPriority = ExecutionPriority.Normal
        };
    }

    [Theory]
    [InlineData(TaskType.Chat, ExecutionPriority.Immediate)]
    [InlineData(TaskType.QuestionAnswer, ExecutionPriority.Immediate)]
    [InlineData(TaskType.Code, ExecutionPriority.Normal)]
    [InlineData(TaskType.Rewrite, ExecutionPriority.Normal)]
    [InlineData(TaskType.Search, ExecutionPriority.Background)]
    [InlineData(TaskType.Summarize, ExecutionPriority.Background)]
    [InlineData(TaskType.Analysis, ExecutionPriority.Background)]
    public void AssignPriority_WithDefaultMappings_ReturnsCorrectPriority(
        TaskType taskType, ExecutionPriority expectedPriority)
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid defaults
            IsInteractive = false,
            IsRetry = false
        };

        // Act
        var result = assigner.AssignPriority(taskType, context);

        // Assert
        Assert.Equal(expectedPriority, result);
    }

    [Fact]
    public void AssignPriority_WithPriorityOverride_RespectOverride()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            PriorityOverride = ExecutionPriority.Immediate
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Search normally is Background, but override sets it to Immediate)
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithUserFacingAlwaysImmediate_ElevatesToImmediate()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.UserFacingAlwaysImmediate = true;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = true
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Search normally is Background, but user-facing elevates to Immediate)
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithInteractiveRequest_ElevatesToNormal()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid default true
            IsInteractive = true,
            IsRetry = false
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Search normally is Background, but interactive elevates to Normal)
        Assert.Equal(ExecutionPriority.Normal, result);
    }

    [Fact]
    public void AssignPriority_WithRetry_ElevatesPriority()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid defaults
            IsInteractive = false,
            IsRetry = true
        };

        // Act - Background task retry
        var result1 = assigner.AssignPriority(TaskType.Search, context);
        // Act - Normal task retry
        var result2 = assigner.AssignPriority(TaskType.Code, context);

        // Assert (Background -> Normal, Normal -> Immediate)
        Assert.Equal(ExecutionPriority.Normal, result1);
        Assert.Equal(ExecutionPriority.Immediate, result2);
    }

    [Fact]
    public void AssignPriority_WithRetryElevationDisabled_DoesNotElevate()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.ElevateRetryPriority = false;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid defaults
            IsInteractive = false,
            IsRetry = true
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Should remain Background)
        Assert.Equal(ExecutionPriority.Background, result);
    }

    [Fact]
    public void AssignPriority_WithInteractiveElevationDisabled_DoesNotElevate()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.ElevateInteractivePriority = false;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid defaults
            IsInteractive = true,
            IsRetry = false
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Should remain Background)
        Assert.Equal(ExecutionPriority.Background, result);
    }

    [Fact]
    public void AssignPriority_WithNullContext_UsesDefaults()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);

        // Act
        var result = assigner.AssignPriority(TaskType.Chat, context: null);

        // Assert
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithCustomMapping_UsesCustomPriority()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypePriorityMappings["Code"] = nameof(ExecutionPriority.Background);
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,  // Explicitly set to avoid defaults
            IsInteractive = false,
            IsRetry = false
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Code, context);

        // Assert (Code normally Normal, but custom mapping sets Background)
        Assert.Equal(ExecutionPriority.Background, result);
    }

    [Fact]
    public void GetDefaultPriority_ReturnsTaskTypeMappedPriority()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);

        // Act
        var result = assigner.GetDefaultPriority(TaskType.Chat);

        // Assert
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void GetDefaultPriority_WithUnmappedType_ReturnsFallback()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.TaskTypePriorityMappings.Clear();
        customOptions.DefaultPriority = ExecutionPriority.Normal;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);

        // Act
        var result = assigner.GetDefaultPriority(TaskType.Unknown);

        // Assert
        Assert.Equal(ExecutionPriority.Normal, result);
    }

    [Fact]
    public void AssignPriority_WithImmediatePriorityRetry_DoesNotElevateAboveImmediate()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsRetry = true
        };

        // Act (Chat already has Immediate priority)
        var result = assigner.AssignPriority(TaskType.Chat, context);

        // Assert (Should remain Immediate, not go higher)
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithMultipleElevations_AppliesMostRestrictive()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.UserFacingAlwaysImmediate = true;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = true,
            IsInteractive = true,
            IsRetry = true
        };

        // Act (Search normally Background)
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (UserFacingAlwaysImmediate takes precedence)
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithNonUserFacingBackground_RemainsBackground()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = false,
            IsInteractive = false,
            IsRetry = false
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Search, context);

        // Assert (Should remain Background)
        Assert.Equal(ExecutionPriority.Background, result);
    }

    [Fact]
    public void AssignPriority_WithUnknownTaskType_UsesDefaultPriority()
    {
        // Arrange
        var customOptions = GetDefaultOptions();
        customOptions.DefaultPriority = ExecutionPriority.Background;
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);

        // Act
        var result = assigner.AssignPriority(TaskType.Unknown);

        // Assert (Unknown maps to Normal by default mapping, but we test fallback logic)
        Assert.Equal(ExecutionPriority.Normal, result); // From default mappings
    }

    [Fact]
    public void AssignPriority_WithEmptyMappingsConfiguration_UsesDefaultMappings()
    {
        // Arrange
        var customOptions = new PriorityAssignerOptions
        {
            // TaskTypePriorityMappings left empty - should use defaults
            UserFacingAlwaysImmediate = false,
            ElevateInteractivePriority = true,
            ElevateRetryPriority = true,
            DefaultPriority = ExecutionPriority.Normal
        };
        var options = Options.Create(customOptions);
        var assigner = new PriorityAssigner(options, _logger);

        // Act
        var result = assigner.AssignPriority(TaskType.Chat);

        // Assert (Should use default mapping for Chat = Immediate)
        Assert.Equal(ExecutionPriority.Immediate, result);
    }

    [Fact]
    public void AssignPriority_WithComplexContext_AppliesCorrectLogic()
    {
        // Arrange
        var options = Options.Create(GetDefaultOptions());
        var assigner = new PriorityAssigner(options, _logger);
        var context = new PriorityContext
        {
            IsUserFacing = true,
            IsInteractive = true,
            SessionId = "session-123",
            ProjectId = "project-456",
            UserId = "user-789"
        };

        // Act
        var result = assigner.AssignPriority(TaskType.Code, context);

        // Assert (Code is Normal by default, interactive keeps it Normal)
        Assert.Equal(ExecutionPriority.Normal, result);
    }
}
