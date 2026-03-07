namespace Daiv3.App.Maui.Pages;

using Daiv3.App.Maui.ViewModels;
using Microsoft.Maui.Controls;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _viewModel;
    private bool _isInitializing;

    public AdminDashboardPage(AdminDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _ = InitializeAdminDashboardAsync();
    }

    private async Task InitializeAdminDashboardAsync()
    {
        if (_isInitializing || _viewModel.IsPolling)
        {
            return;
        }

        try
        {
            _isInitializing = true;
            await _viewModel.StartPollingAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        // Stop polling when leaving page
        if (_viewModel.IsPolling)
        {
            _viewModel.StopPollingCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Lifecycle cleanup is handled by DI/container scope; avoid disposing on tab switch.
    }
}
