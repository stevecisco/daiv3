using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for <see cref="ToolRegistry"/>.
/// </summary>
public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_mockLogger.Object);
    }

    [Fact]
    public async Task RegisterToolAsync_NewTool_ReturnsTrue()
    {
        // Arrange
        var tool = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);

        // Act
        var result = await _registry.RegisterToolAsync(tool);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RegisterToolAsync_Duplicate_Updates()
    {
        // Arrange
        var tool1 = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);
        var tool2 = CreateTestTool("tool-1", "Updated Tool", ToolBackendType.MCP);

        // Act
        await _registry.RegisterToolAsync(tool1);
        var result = await _registry.RegisterToolAsync(tool2);

        // Assert
        Assert.True(result);
        var retrieved = await _registry.GetToolAsync("tool-1");
        Assert.Equal("Updated Tool", retrieved!.Name);
        Assert.Equal(ToolBackendType.MCP, retrieved.Backend);
    }

    [Fact]
    public async Task GetToolAsync_ExistingTool_ReturnsTool()
    {
        // Arrange
        var tool = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);
        await _registry.RegisterToolAsync(tool);

        // Act
        var result = await _registry.GetToolAsync("tool-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("tool-1", result.ToolId);
        Assert.Equal("Test Tool", result.Name);
    }

    [Fact]
    public async Task GetToolAsync_NonExistingTool_ReturnsNull()
    {
        // Act
        var result = await _registry.GetToolAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UnregisterToolAsync_ExistingTool_ReturnsTrue()
    {
        // Arrange
        var tool = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);
        await _registry.RegisterToolAsync(tool);

        // Act
        var result = await _registry.UnregisterToolAsync("tool-1");

        // Assert
        Assert.True(result);
        var retrieved = await _registry.GetToolAsync("tool-1");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task UnregisterToolAsync_NonExistingTool_ReturnsFalse()
    {
        // Act
        var result = await _registry.UnregisterToolAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllToolsAsync_ReturnsAllRegisteredTools()
    {
        // Arrange
        var tool1 = CreateTestTool("tool-1", "Tool 1", ToolBackendType.Direct);
        var tool2 = CreateTestTool("tool-2", "Tool 2", ToolBackendType.CLI);
        var tool3 = CreateTestTool("tool-3", "Tool 3", ToolBackendType.MCP);
        await _registry.RegisterToolAsync(tool1);
        await _registry.RegisterToolAsync(tool2);
        await _registry.RegisterToolAsync(tool3);

        // Act
        var result = await _registry.GetAllToolsAsync();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetToolsByBackendAsync_FiltersCorrectly()
    {
        // Arrange
        var directTool1 = CreateTestTool("direct-1", "Direct 1", ToolBackendType.Direct);
        var directTool2 = CreateTestTool("direct-2", "Direct 2", ToolBackendType.Direct);
        var cliTool = CreateTestTool("cli-1", "CLI 1", ToolBackendType.CLI);
        var mcpTool = CreateTestTool("mcp-1", "MCP 1", ToolBackendType.MCP);

        await _registry.RegisterToolAsync(directTool1);
        await _registry.RegisterToolAsync(directTool2);
        await _registry.RegisterToolAsync(cliTool);
        await _registry.RegisterToolAsync(mcpTool);

        // Act
        var directTools = await _registry.GetToolsByBackendAsync(ToolBackendType.Direct);
        var cliTools = await _registry.GetToolsByBackendAsync(ToolBackendType.CLI);
        var mcpTools = await _registry.GetToolsByBackendAsync(ToolBackendType.MCP);

        // Assert
        Assert.Equal(2, directTools.Count);
        Assert.All(directTools, t => Assert.Equal(ToolBackendType.Direct, t.Backend));

        Assert.Single(cliTools);
        Assert.Equal(ToolBackendType.CLI, cliTools[0].Backend);

        Assert.Single(mcpTools);
        Assert.Equal(ToolBackendType.MCP, mcpTools[0].Backend);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ReturnsOnlyAvailable()
    {
        // Arrange
        var tool1 = CreateTestTool("tool-1", "Tool 1", ToolBackendType.Direct);
        tool1.IsAvailable = true;
        var tool2 = CreateTestTool("tool-2", "Tool 2", ToolBackendType.Direct);
        tool2.IsAvailable = false;
        var tool3 = CreateTestTool("tool-3", "Tool 3", ToolBackendType.MCP);
        tool3.IsAvailable = true;

        await _registry.RegisterToolAsync(tool1);
        await _registry.RegisterToolAsync(tool2);
        await _registry.RegisterToolAsync(tool3);

        // Act
        var result = await _registry.GetAvailableToolsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.True(t.IsAvailable));
    }

    [Fact]
    public async Task UpdateToolAvailabilityAsync_ExistingTool_UpdatesSuccessfully()
    {
        // Arrange
        var tool = CreateTestTool("tool-1", "Test Tool", ToolBackendType.Direct);
        tool.IsAvailable = true;
        await _registry.RegisterToolAsync(tool);

        // Act
        var result = await _registry.UpdateToolAvailabilityAsync("tool-1", false);

        // Assert
        Assert.True(result);
        var updated = await _registry.GetToolAsync("tool-1");
        Assert.False(updated!.IsAvailable);
    }

    [Fact]
    public async Task UpdateToolAvailabilityAsync_NonExistingTool_ReturnsFalse()
    {
        // Act
        var result = await _registry.UpdateToolAvailabilityAsync("nonexistent", true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ToolDescriptor_EstimatedTokenCost_ReflectsBackend()
    {
        // Arrange
        var directTool = CreateTestTool("direct-1", "Direct Tool", ToolBackendType.Direct);
        directTool.EstimatedTokenCost = 0;

        var cliTool = CreateTestTool("cli-1", "CLI Tool", ToolBackendType.CLI);
        cliTool.EstimatedTokenCost = 5;

        var mcpTool = CreateTestTool("mcp-1", "MCP Tool", ToolBackendType.MCP);
        mcpTool.EstimatedTokenCost = 150;

        await _registry.RegisterToolAsync(directTool);
        await _registry.RegisterToolAsync(cliTool);
        await _registry.RegisterToolAsync(mcpTool);

        // Act
        var direct = await _registry.GetToolAsync("direct-1");
        var cli = await _registry.GetToolAsync("cli-1");
        var mcp = await _registry.GetToolAsync("mcp-1");

        // Assert
        Assert.Equal(0, direct!.EstimatedTokenCost);
        Assert.Equal(5, cli!.EstimatedTokenCost);
        Assert.Equal(150, mcp!.EstimatedTokenCost);
    }

    private ToolDescriptor CreateTestTool(string toolId, string name, ToolBackendType backend)
    {
        return new ToolDescriptor
        {
            ToolId = toolId,
            Name = name,
            Description = $"Description for {name}",
            Source = backend == ToolBackendType.MCP ? "test-server" : "test-source",
            Backend = backend
        };
    }
}
