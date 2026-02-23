using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
