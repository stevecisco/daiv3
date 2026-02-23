using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Pages;
using Daiv3.App.Maui.ViewModels;

namespace Daiv3.App.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register ViewModels
		builder.Services.AddSingleton<ChatViewModel>();
		builder.Services.AddSingleton<DashboardViewModel>();
		builder.Services.AddSingleton<ProjectsViewModel>();
		builder.Services.AddSingleton<SettingsViewModel>();

		// Register Pages
		builder.Services.AddSingleton<ChatPage>();
		builder.Services.AddSingleton<DashboardPage>();
		builder.Services.AddSingleton<ProjectsPage>();
		builder.Services.AddSingleton<SettingsPage>();
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
