using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.Pages;

/// <summary>
/// Code-behind for IndexingPage.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public partial class IndexingPage : ContentPage
{
    private readonly IndexingViewModel _viewModel;
    private readonly ILogger<IndexingPage> _logger;

    public IndexingPage(IndexingViewModel viewModel, ILogger<IndexingPage> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize IndexingPage XAML");
            Content = BuildFallbackContent($"Indexing page failed to load: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during IndexingPage initialization");
            Content = BuildFallbackContent($"Indexing initialization failed: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Note: Do not dispose injected dependencies - they are managed by DI container
    }

    private static View BuildFallbackContent(string message)
    {
        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Indexing Page Error",
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = message,
                        FontSize = 14
                    }
                }
            }
        };
    }
}
