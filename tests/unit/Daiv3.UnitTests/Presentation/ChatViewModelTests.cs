using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for ChatViewModel.
/// </summary>
public class ChatViewModelTests
{
    private readonly Mock<ILogger<ChatViewModel>> _mockLogger;

    public ChatViewModelTests()
    {
        _mockLogger = new Mock<ILogger<ChatViewModel>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var viewModel = new ChatViewModel(_mockLogger.Object);

        // Assert
        Assert.Equal("Chat", viewModel.Title);
        Assert.NotNull(viewModel.Messages);
        Assert.Empty(viewModel.Messages);
        Assert.Equal(string.Empty, viewModel.MessageInput);
        Assert.False(viewModel.IsWaitingForResponse);
    }

    [Fact]
    public void SendMessageCommand_WhenMessageIsEmpty_ShouldNotExecute()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = string.Empty;

        // Act
        var canExecute = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public void SendMessageCommand_WhenMessageIsWhitespace_ShouldNotExecute()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = "   ";

        // Act
        var canExecute = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public void SendMessageCommand_WhenMessageIsValid_ShouldExecute()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = "Hello, World!";

        // Act
        var canExecute = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void SendMessageCommand_WhenWaitingForResponse_ShouldNotExecute()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = "Hello";
        viewModel.IsWaitingForResponse = true;

        // Act
        var canExecute = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public void SendMessageCommand_ShouldAddUserMessage()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = "Test message";

        // Act
        viewModel.SendMessageCommand.Execute(null);

        // Assert
        Assert.Single(viewModel.Messages);
        Assert.Equal("Test message", viewModel.Messages[0].Text);
        Assert.True(viewModel.Messages[0].IsUser);
        Assert.Equal(string.Empty, viewModel.MessageInput);
        Assert.True(viewModel.IsWaitingForResponse);
    }

    [Fact]
    public void ClearChatCommand_ShouldClearAllMessages()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.Messages.Add(new ChatMessage { Text = "Message 1", IsUser = true });
        viewModel.Messages.Add(new ChatMessage { Text = "Message 2", IsUser = false });
        viewModel.MessageInput = "Current input";

        // Act
        viewModel.ClearChatCommand.Execute(null);

        // Assert
        Assert.Empty(viewModel.Messages);
        Assert.Equal(string.Empty, viewModel.MessageInput);
    }

    [Fact]
    public void MessageInput_WhenChanged_ShouldRaiseSendCommandCanExecuteChanged()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        var canExecuteBefore = viewModel.SendMessageCommand.CanExecute(null);

        // Act
        viewModel.MessageInput = "New message";
        var canExecuteAfter = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.False(canExecuteBefore);
        Assert.True(canExecuteAfter);
    }

    [Fact]
    public void IsWaitingForResponse_WhenChanged_ShouldRaiseSendCommandCanExecuteChanged()
    {
        // Arrange
        var viewModel = new ChatViewModel(_mockLogger.Object);
        viewModel.MessageInput = "Test";
        var canExecuteBefore = viewModel.SendMessageCommand.CanExecute(null);

        // Act
        viewModel.IsWaitingForResponse = true;
        var canExecuteAfter = viewModel.SendMessageCommand.CanExecute(null);

        // Assert
        Assert.True(canExecuteBefore);
        Assert.False(canExecuteAfter);
    }
}
