using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Pages;
using Daiv3.App.Maui.Services;
using Daiv3.App.Maui.ViewModels;
using Daiv3.Knowledge.Embedding;
using Daiv3.Infrastructure.Shared.Logging;

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

		// Configure logging
		builder.Logging.SetMinimumLevel(LogLevel.Information);
#if DEBUG
		builder.Logging.AddDebug();
		builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif
		builder.Logging.AddFileLogging("maui", LogLevel.Debug);

		// Register bootstrap service for embedding model
		builder.Services.AddSingleton<EmbeddingModelBootstrapService>();

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

		var modelPath = GetDefaultEmbeddingModelPath();
		builder.Services.AddEmbeddingServices(options =>
		{
			options.ModelPath = modelPath;
		});

		var app = builder.Build();

		// Bootstrap the embedding model on app startup (fire-and-forget)
		// Don't block app startup - model will be copied in background
		var logger = app.Services.GetRequiredService<ILogger<EmbeddingModelBootstrapService>>();
		_ = Task.Run(async () =>
		{
			try
			{
				logger.LogInformation("Starting embedding model bootstrap");
				var bootstrapService = app.Services.GetRequiredService<EmbeddingModelBootstrapService>();
				await bootstrapService.EnsureModelAsync();
				logger.LogInformation("Embedding model bootstrap completed successfully");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error bootstrapping embedding model");
			}
		});

		return app;
	}

	private static string GetDefaultEmbeddingModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "model.onnx");
	}
}
