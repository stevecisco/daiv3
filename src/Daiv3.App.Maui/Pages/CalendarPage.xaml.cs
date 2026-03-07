using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui.Pages;

/// <summary>
/// Calendar and Reminders page.
/// Implements CT-REQ-014: Calendar and Reminders Dashboard.
/// </summary>
public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
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
        if (sender is Button button && button.CommandParameter is string reminderId)
        {
            await _viewModel.MarkReminderAsReadAsync(reminderId);
        }
    }

    private async void OnSnoozeReminderClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string reminderId)
        {
            // Default snooze: 1 hour
            await _viewModel.SnoozeReminderAsync(reminderId, TimeSpan.FromHours(1));
        }
    }
}
