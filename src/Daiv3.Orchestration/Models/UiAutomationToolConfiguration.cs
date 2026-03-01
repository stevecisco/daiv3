using System.Text.Json;

namespace Daiv3.Orchestration.Models;

/// <summary>
/// Configuration for UI automation tool invocation via Windows accessibility APIs.
/// </summary>
/// <remarks>
/// This configuration is stored in the ToolDescriptor.Metadata dictionary as key-value pairs.
/// It defines how to locate UI elements and perform automated interactions with external applications.
/// This capability is Windows-only and uses the Windows UIA (UI Automation) framework.
/// </remarks>
public sealed class UiAutomationToolConfiguration
{
    /// <summary>
    /// Gets or sets the action type to perform on the UI element.
    /// </summary>
    /// <remarks>
    /// Supported actions: Click, SetText, GetText, TypeKeys, Scroll, WaitForElement.
    /// </remarks>
    public required string ActionType { get; set; }

    /// <summary>
    /// Gets or sets the window identifier method.
    /// </summary>
    /// <remarks>
    /// Supported methods: WindowName, WindowClass, ProcessName, WindowHandle.
    /// </remarks>
    public string WindowIdentifierType { get; set; } = "ProcessName";

    /// <summary>
    /// Gets or sets the window identifier value (name, class, process name, or handle).
    /// </summary>
    public string? WindowIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the UI element identifier method.
    /// </summary>
    /// <remarks>
    /// Supported methods: AutomationId, ClassName, Name, Xpath, Position.
    /// </remarks>
    public string ElementIdentifierType { get; set; } = "AutomationId";

    /// <summary>
    /// Gets or sets the UI element identifier value.
    /// </summary>
    /// <remarks>
    /// Format depends on ElementIdentifierType:
    /// - AutomationId: Direct automation ID string
    /// - ClassName: Window class name
    /// - Name: Element display name or label
    /// - Xpath: XPath-like navigation string
    /// - Position: "x,y" coordinate pair
    /// </remarks>
    public string? ElementIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the text to input for SetText or TypeKeys actions.
    /// </summary>
    public string? InputText { get; set; }

    /// <summary>
    /// Gets or sets the timeout in milliseconds for finding elements.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the number of retry attempts when element is not found.
    /// </summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay in milliseconds between retry attempts.
    /// </summary>
    public int RetryDelayMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets additional options for the automation action.
    /// </summary>
    /// <remarks>
    /// Examples: ClearExistingText=true, SendKeysModifiers=Control+C, ScrollDirection=Down
    /// </remarks>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to capture a screenshot after the action completes.
    /// </summary>
    public bool CaptureScreenshot { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to take a screenshot before the action begins.
    /// </summary>
    public bool CaptureScreenshotBefore { get; set; } = false;

    /// <summary>
    /// Parses UI automation configuration from ToolDescriptor.Metadata dictionary.
    /// </summary>
    /// <param name="metadata">Metadata dictionary from ToolDescriptor.</param>
    /// <returns>Parsed configuration or null if metadata is incomplete.</returns>
    public static UiAutomationToolConfiguration? FromMetadata(Dictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("ActionType", out var actionType))
        {
            return null;
        }

        var config = new UiAutomationToolConfiguration
        {
            ActionType = actionType
        };

        if (metadata.TryGetValue("WindowIdentifierType", out var windowIdType))
        {
            config.WindowIdentifierType = windowIdType;
        }

        if (metadata.TryGetValue("WindowIdentifier", out var windowId))
        {
            config.WindowIdentifier = windowId;
        }

        if (metadata.TryGetValue("ElementIdentifierType", out var elementIdType))
        {
            config.ElementIdentifierType = elementIdType;
        }

        if (metadata.TryGetValue("ElementIdentifier", out var elementId))
        {
            config.ElementIdentifier = elementId;
        }

        if (metadata.TryGetValue("InputText", out var inputText))
        {
            config.InputText = inputText;
        }

        if (metadata.TryGetValue("TimeoutMs", out var timeoutStr) &&
            int.TryParse(timeoutStr, out var timeout))
        {
            config.TimeoutMs = timeout;
        }

        if (metadata.TryGetValue("RetryCount", out var retryStr) &&
            int.TryParse(retryStr, out var retry))
        {
            config.RetryCount = retry;
        }

        if (metadata.TryGetValue("RetryDelayMs", out var retryDelayStr) &&
            int.TryParse(retryDelayStr, out var retryDelay))
        {
            config.RetryDelayMs = retryDelay;
        }

        if (metadata.TryGetValue("CaptureScreenshot", out var captureStr) &&
            bool.TryParse(captureStr, out var capture))
        {
            config.CaptureScreenshot = capture;
        }

        if (metadata.TryGetValue("CaptureScreenshotBefore", out var captureBeforeStr) &&
            bool.TryParse(captureBeforeStr, out var captureBefore))
        {
            config.CaptureScreenshotBefore = captureBefore;
        }

        // Parse options (prefixed with "Option.")
        foreach (var kvp in metadata.Where(m => m.Key.StartsWith("Option.", StringComparison.OrdinalIgnoreCase)))
        {
            var optionName = kvp.Key.Substring(7); // Remove "Option." prefix
            config.Options[optionName] = kvp.Value;
        }

        return config;
    }

    /// <summary>
    /// Converts this configuration to metadata dictionary for storage in ToolDescriptor.
    /// </summary>
    /// <returns>Metadata dictionary.</returns>
    public Dictionary<string, string> ToMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["ActionType"] = ActionType,
            ["WindowIdentifierType"] = WindowIdentifierType,
            ["ElementIdentifierType"] = ElementIdentifierType,
            ["TimeoutMs"] = TimeoutMs.ToString(),
            ["RetryCount"] = RetryCount.ToString(),
            ["RetryDelayMs"] = RetryDelayMs.ToString(),
            ["CaptureScreenshot"] = CaptureScreenshot.ToString(),
            ["CaptureScreenshotBefore"] = CaptureScreenshotBefore.ToString()
        };

        if (!string.IsNullOrWhiteSpace(WindowIdentifier))
        {
            metadata["WindowIdentifier"] = WindowIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(ElementIdentifier))
        {
            metadata["ElementIdentifier"] = ElementIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(InputText))
        {
            metadata["InputText"] = InputText;
        }

        // Add options with "Option." prefix
        foreach (var kvp in Options)
        {
            metadata[$"Option.{kvp.Key}"] = kvp.Value;
        }

        return metadata;
    }
}

/// <summary>
/// Specifies the method for identifying a UI window.
/// </summary>
public enum UiWindowIdentifierType
{
    /// <summary>
    /// Identify window by its display title/name.
    /// </summary>
    WindowName = 0,

    /// <summary>
    /// Identify window by its window class name.
    /// </summary>
    WindowClass = 1,

    /// <summary>
    /// Identify window by its process name (executable name without path or extension).
    /// </summary>
    ProcessName = 2,

    /// <summary>
    /// Identify window by its window handle (HWND as integer).
    /// </summary>
    WindowHandle = 3
}

/// <summary>
/// Specifies the method for identifying a UI element within a window.
/// </summary>
public enum UiElementIdentifierType
{
    /// <summary>
    /// Identify element by its UIA Automation ID.
    /// </summary>
    AutomationId = 0,

    /// <summary>
    /// Identify element by its class name.
    /// </summary>
    ClassName = 1,

    /// <summary>
    /// Identify element by its name/label text.
    /// </summary>
    Name = 2,

    /// <summary>
    /// Identify element by XPath-like navigation from window root.
    /// </summary>
    Xpath = 3,

    /// <summary>
    /// Identify element by screen coordinates (x,y).
    /// </summary>
    Position = 4
}

/// <summary>
/// Specifies the type of action to perform on a UI element.
/// </summary>
public enum UiAutomationActionType
{
    /// <summary>
    /// Click the element (left mouse button).
    /// </summary>
    Click = 0,

    /// <summary>
    /// Clear existing text and set new text in the element.
    /// </summary>
    SetText = 1,

    /// <summary>
    /// Get the current text value from the element.
    /// </summary>
    GetText = 2,

    /// <summary>
    /// Type keys/characters into the element (supports special keys like Enter, Control, Shift).
    /// </summary>
    TypeKeys = 3,

    /// <summary>
    /// Scroll within the element (direction specified in Options).
    /// </summary>
    Scroll = 4,

    /// <summary>
    /// Wait for the element to be visible/available (does not interact with it).
    /// </summary>
    WaitForElement = 5,

    /// <summary>
    /// Right-click the element (context menu).
    /// </summary>
    RightClick = 6,

    /// <summary>
    /// Double-click the element.
    /// </summary>
    DoubleClick = 7,

    /// <summary>
    /// Get the current value property of the element.
    /// </summary>
    GetValue = 8,

    /// <summary>
    /// Take a screenshot of the specified element or window.
    /// </summary>
    TakeScreenshot = 9
}
