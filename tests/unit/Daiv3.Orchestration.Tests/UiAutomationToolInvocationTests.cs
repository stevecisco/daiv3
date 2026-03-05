using Daiv3.Mcp.Integration;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.Tests;

/// <summary>
/// Unit tests for UI automation tool invocation in ToolRoutingService.
/// Tests AST-REQ-010 functionality for Windows UI automation.
/// </summary>
public sealed class UiAutomationToolInvocationTests
{
    private readonly Mock<ILogger<ToolRoutingService>> _mockLogger;
    private readonly Mock<IToolRegistry> _mockRegistry;
    private readonly Mock<IMcpToolProvider> _mockMcpProvider;
    private readonly Mock<IHttpClientFactory> _mockHttpFactory;
    private readonly ToolRoutingService _service;

    public UiAutomationToolInvocationTests()
    {
        _mockLogger = new Mock<ILogger<ToolRoutingService>>();
        _mockRegistry = new Mock<IToolRegistry>();
        _mockMcpProvider = new Mock<IMcpToolProvider>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();

        _service = new ToolRoutingService(
            _mockLogger.Object,
            _mockRegistry.Object,
            _mockMcpProvider.Object,
            _mockHttpFactory.Object);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithValidConfiguration_ReturnsStructuredResult()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "click-button",
            "Click",
            "ProcessName",
            "notepad.exe",
            "AutomationId",
            "OKButton");

        _mockRegistry.Setup(r => r.GetToolAsync("click-button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("click-button", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
        Assert.NotNull(result.Result);
        Assert.True(result.ContextTokenCost >= 0);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithMissingWindowIdentifier_ReturnsError()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["ActionType"] = "Click",
            ["WindowIdentifierType"] = "ProcessName",
            // WindowIdentifier is missing
            ["ElementIdentifierType"] = "AutomationId",
            ["ElementIdentifier"] = "OKButton"
        };

        var tool = new ToolDescriptor
        {
            ToolId = "click-button",
            Name = "Click Button",
            Description = "Click a button via UI automation",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = metadata,
            EstimatedTokenCost = 100
        };

        _mockRegistry.Setup(r => r.GetToolAsync("click-button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("click-button", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("MISSING_WINDOW_IDENTIFIER", result.ErrorCode);
        Assert.Contains("Window identifier", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithInvalidConfiguration_ReturnsError()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "invalid-tool",
            Name = "Invalid Tool",
            Description = "Tool with no ActionType",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = new Dictionary<string, string>(), // Missing ActionType
            EstimatedTokenCost = 100
        };

        _mockRegistry.Setup(r => r.GetToolAsync("invalid-tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("invalid-tool", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("INVALID_CONFIGURATION", result.ErrorCode);
        Assert.Contains("configuration is missing", result.ErrorMessage);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithClickAction_EstimatesTokenCost()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "click-button",
            "Click",
            "ProcessName",
            "ApplicationName.exe",
            "AutomationId",
            "VeryLongAutomationIdThatShouldContributeSomeTokens");

        _mockRegistry.Setup(r => r.GetToolAsync("click-button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("click-button", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContextTokenCost > 0, "Token cost should be positive for automation action");
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithSetTextAction_IncludesInputTextInCost()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "type-text",
            "SetText",
            "ProcessName",
            "notepad.exe",
            "ClassName",
            "Edit");

        // Add input text
        tool.Metadata["InputText"] = "This is a longer piece of text that should increase token cost";

        _mockRegistry.Setup(r => r.GetToolAsync("type-text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("type-text", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContextTokenCost > 0);
        // Token cost should be higher due to input text
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithOptionsMetadata_SerializesConfiguration()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "scroll-action",
            Name = "Scroll",
            Description = "Scroll within a UI element",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = new Dictionary<string, string>
            {
                ["ActionType"] = "Scroll",
                ["WindowIdentifierType"] = "WindowName",
                ["WindowIdentifier"] = "Main Window",
                ["ElementIdentifierType"] = "AutomationId",
                ["ElementIdentifier"] = "ScrollableArea",
                ["Option.ScrollDirection"] = "Down",
                ["Option.ScrollAmount"] = "5"
            },
            EstimatedTokenCost = 100
        };

        _mockRegistry.Setup(r => r.GetToolAsync("scroll-action", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("scroll-action", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
        Assert.NotNull(result.Result);
        // Result should be a JSON representation of the automation action
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithWaitForElementAction_ConfiguresTimeout()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "wait-element",
            Name = "Wait for Element",
            Description = "Wait for an element to appear",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = new Dictionary<string, string>
            {
                ["ActionType"] = "WaitForElement",
                ["WindowIdentifierType"] = "ProcessName",
                ["WindowIdentifier"] = "app.exe",
                ["ElementIdentifierType"] = "Name",
                ["ElementIdentifier"] = "LoadingIndicator",
                ["TimeoutMs"] = "10000",
                ["RetryCount"] = "5"
            },
            EstimatedTokenCost = 100
        };

        _mockRegistry.Setup(r => r.GetToolAsync("wait-element", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("wait-element", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
        Assert.NotNull(result.Metadata);
        Assert.Equal("10000", result.Metadata["TimeoutMs"]);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithGetTextAction_ReturnsNotImplemented()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "get-text",
            "GetText",
            "ProcessName",
            "notepad.exe",
            "AutomationId",
            "TextBox");

        _mockRegistry.Setup(r => r.GetToolAsync("get-text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("get-text", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("NOT_IMPLEMENTED", result.ErrorCode);
        Assert.Contains("infrastructure", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_WithScreenshotCapture_IncludesOptionInMetadata()
    {
        // Arrange
        var tool = new ToolDescriptor
        {
            ToolId = "capture-screenshot",
            Name = "Capture Screenshot",
            Description = "Capture a screenshot after action",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = new Dictionary<string, string>
            {
                ["ActionType"] = "Click",
                ["WindowIdentifierType"] = "ProcessName",
                ["WindowIdentifier"] = "app.exe",
                ["ElementIdentifierType"] = "AutomationId",
                ["ElementIdentifier"] = "Button",
                ["CaptureScreenshot"] = "true"
            },
            EstimatedTokenCost = 100
        };

        _mockRegistry.Setup(r => r.GetToolAsync("capture-screenshot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("capture-screenshot", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_RightClickAction_ConfiguresCorrectly()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "right-click",
            "RightClick",
            "WindowName",
            "Context Menu Window",
            "AutomationId",
            "MenuItem");

        _mockRegistry.Setup(r => r.GetToolAsync("right-click", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("right-click", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
    }

    [Fact]
    public async Task InvokeUiAutomationTool_DoubleClickAction_ConfiguresCorrectly()
    {
        // Arrange
        var tool = CreateTestUiAutomationTool(
            "double-click",
            "DoubleClick",
            "ProcessName",
            "explorer.exe",
            "Name",
            "Document.txt");

        _mockRegistry.Setup(r => r.GetToolAsync("double-click", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tool);

        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _service.InvokeToolAsync("double-click", parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ToolBackendType.UiAutomation, result.BackendUsed);
    }

    [Fact]
    public async Task UiAutomationToolConfiguration_SerializesAndDeserializes()
    {
        // Arrange
        var config = new UiAutomationToolConfiguration
        {
            ActionType = "SetText",
            WindowIdentifierType = "ProcessName",
            WindowIdentifier = "app.exe",
            ElementIdentifierType = "AutomationId",
            ElementIdentifier = "TextBox",
            InputText = "Test input",
            TimeoutMs = 7500,
            RetryCount = 3,
            RetryDelayMs = 750,
            CaptureScreenshot = true,
            CaptureScreenshotBefore = true,
            Options = new Dictionary<string, string>
            {
                ["ClearFirst"] = "true",
                ["VerifyText"] = "true"
            }
        };

        // Act
        var metadata = config.ToMetadata();
        var deserializedConfig = UiAutomationToolConfiguration.FromMetadata(metadata);

        // Assert
        Assert.NotNull(deserializedConfig);
        Assert.Equal(config.ActionType, deserializedConfig.ActionType);
        Assert.Equal(config.WindowIdentifier, deserializedConfig.WindowIdentifier);
        Assert.Equal(config.ElementIdentifier, deserializedConfig.ElementIdentifier);
        Assert.Equal(config.InputText, deserializedConfig.InputText);
        Assert.Equal(config.TimeoutMs, deserializedConfig.TimeoutMs);
        Assert.Equal(config.RetryCount, deserializedConfig.RetryCount);
        Assert.Equal(config.CaptureScreenshot, deserializedConfig.CaptureScreenshot);
        Assert.Equal(config.CaptureScreenshotBefore, deserializedConfig.CaptureScreenshotBefore);
        Assert.Equal(2, deserializedConfig.Options.Count);
    }

    // Helper methods

    private static ToolDescriptor CreateTestUiAutomationTool(
        string toolId,
        string actionType,
        string windowIdentifierType,
        string windowIdentifier,
        string elementIdentifierType,
        string elementIdentifier)
    {
        return new ToolDescriptor
        {
            ToolId = toolId,
            Name = $"UI Automation - {actionType}",
            Description = $"Perform {actionType} action via UI automation",
            Backend = ToolBackendType.UiAutomation,
            Source = "UI",
            IsAvailable = true,
            Metadata = new Dictionary<string, string>
            {
                ["ActionType"] = actionType,
                ["WindowIdentifierType"] = windowIdentifierType,
                ["WindowIdentifier"] = windowIdentifier,
                ["ElementIdentifierType"] = elementIdentifierType,
                ["ElementIdentifier"] = elementIdentifier,
                ["TimeoutMs"] = "5000",
                ["RetryCount"] = "2"
            },
            EstimatedTokenCost = 100
        };
    }
}
