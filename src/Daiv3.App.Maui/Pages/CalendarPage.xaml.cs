using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.Pages;

/// <summary>
/// Calendar and Reminders page.
/// Implements CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;
    private readonly ILogger<CalendarPage> _logger;
    private bool _isPageReady;

    public CalendarPage(CalendarViewModel viewModel, ILogger<CalendarPage> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            InitializeComponent();
            BindingContext = _viewModel;
            _isPageReady = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CalendarPage failed to initialize XAML");
            _isPageReady = false;

            // Fallback content keeps app stable and surfaces the issue without crashing navigation.
            Title = "Calendar";
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "Calendar page failed to load.",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.DarkRed
                    },
                    new Label
                    {
                        Text = "Check application logs for CalendarPage initialization details.",
                        FontSize = 14,
                        TextColor = Colors.Gray
                    }
                }
            };
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isPageReady)
        {
            return;
        }

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing CalendarPage");
        }
    }

    private void OnPreviousClicked(object? sender, EventArgs e)
    {
        _viewModel.NavigatePrevious();
    }

    private void OnNextClicked(object? sender, EventArgs e)
    {
        _viewModel.NavigateNext();
    }

    private void OnTodayClicked(object? sender, EventArgs e)
    {
        _viewModel.NavigateToday();
    }

    private async void OnMarkReminderReadClicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string reminderId)
            {
                await _viewModel.MarkReminderAsReadAsync(reminderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reminder as read from CalendarPage");
        }
    }

    private async void OnSnoozeReminderClicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string reminderId)
            {
                // Default snooze: 1 hour
                await _viewModel.SnoozeReminderAsync(reminderId, TimeSpan.FromHours(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing reminder from CalendarPage");
        }
    }
}
