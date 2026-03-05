using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

/// <summary>
/// Code-behind for IndexingPage.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public partial class IndexingPage : ContentPage
{
    private readonly IndexingViewModel _viewModel;

    public IndexingPage(IndexingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Note: Do not dispose injected dependencies - they are managed by DI container
    }
}
