namespace Daiv3.App.Maui.Pages;

using Daiv3.App.Maui.ViewModels;
using Microsoft.Maui.Controls;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _viewModel;

    public AdminDashboardPage(AdminDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void  OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // Load initial metrics
        _viewModel.RefreshMetricsCommand.Execute(null);

        // Start polling on navigation to page
        if (!_viewModel.IsPolling)
        {
            _viewModel.StartPollingCommand.Execute(null);
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

        // Ensure proper cleanup
        if (_viewModel is IAsyncDisposable disposable)
        {
            // Fire and forget - don't await in sync method
            _ = disposable.DisposeAsync();
        }
    }
}
