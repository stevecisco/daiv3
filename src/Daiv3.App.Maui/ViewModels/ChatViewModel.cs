using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Chat interface.
/// Manages conversation history, message input, and chat interactions.
/// </summary>
public class ChatViewModel : BaseViewModel
{
    private readonly ILogger<ChatViewModel> _logger;
    private string _messageInput = string.Empty;
    private bool _isWaitingForResponse;

    public ChatViewModel(ILogger<ChatViewModel> logger)
    {
        _logger = logger;
        Title = "Chat";
        Messages = new ObservableCollection<ChatMessage>();
        SendMessageCommand = new Command(OnSendMessage, CanSendMessage);
        ClearChatCommand = new Command(OnClearChat);

        _logger.LogInformation("ChatViewModel initialized");
    }

    /// <summary>
    /// Collection of chat messages in the conversation.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; }

    /// <summary>
    /// Gets or sets the current message input text.
    /// </summary>
    public string MessageInput
    {
        get => _messageInput;
        set
        {
            if (SetProperty(ref _messageInput, value))
            {
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the system is waiting for a response.
    /// </summary>
    public bool IsWaitingForResponse
    {
        get => _isWaitingForResponse;
        set
        {
            if (SetProperty(ref _isWaitingForResponse, value))
            {
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }
    }

    public ICommand SendMessageCommand { get; }
    public ICommand ClearChatCommand { get; }

    private bool CanSendMessage()
    {
        return !string.IsNullOrWhiteSpace(MessageInput) && !IsWaitingForResponse;
    }

    private void OnSendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput))
            return;

        _logger.LogInformation("Sending message: {Message}", MessageInput);

        // Add user message
        Messages.Add(new ChatMessage
        {
            Text = MessageInput,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        var userMessage = MessageInput;
        MessageInput = string.Empty;
        IsWaitingForResponse = true;

        // TODO: Integrate with orchestration layer to process the message
        // For now, add a placeholder response
        Task.Run(async () =>
        {
            await Task.Delay(1000); // Simulate processing

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Text = $"Echo: {userMessage} (Orchestration integration pending)",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                IsWaitingForResponse = false;
                _logger.LogInformation("Response added to chat");
            });
        });
    }

    private void OnClearChat()
    {
        Messages.Clear();
        MessageInput = string.Empty;
        _logger.LogInformation("Chat cleared");
    }
}

/// <summary>
/// Represents a single message in the chat conversation.
/// </summary>
public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
}
