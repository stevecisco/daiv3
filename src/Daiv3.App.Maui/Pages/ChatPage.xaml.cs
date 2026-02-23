using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

public partial class ChatPage : ContentPage
{
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
