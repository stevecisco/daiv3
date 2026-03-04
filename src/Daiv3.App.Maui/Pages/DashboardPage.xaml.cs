using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel? _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>
    /// Called when the page appears. Initializes dashboard monitoring.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel != null)
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing dashboard: {ex}");
            }
        }
    }

    /// <summary>
    /// Called when the page disappears. Shuts down dashboard monitoring.
    /// </summary>
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        if (_viewModel != null)
        {
            try
            {
                await _viewModel.ShutdownAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error shutting down dashboard: {ex}");
            }
        }
    }
}
