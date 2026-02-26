using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Pages;
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

		// Bootstrap embedding and OCR models on app startup
		// This runs in the background and logs progress
		var logger = app.Services.GetRequiredService<ILogger<EmbeddingModelBootstrapService>>();
		_ = Task.Run(async () =>
		{
			try
			{
				logger.LogInformation("Starting model bootstrap (embedding Tier 1, Tier 2, and OCR models)");
				var bootstrapService = app.Services.GetRequiredService<EmbeddingModelBootstrapService>();
				
				var success = await bootstrapService.EnsureModelsAsync(progress =>
				{
					if (progress.PercentComplete.HasValue)
					{
						logger.LogInformation("Model download: {Percent:F1}% ({Downloaded:N0} / {Total:N0} bytes)",
							progress.PercentComplete.Value,
							progress.BytesDownloaded,
							progress.TotalBytes ?? 0);
					}
					else
					{
						logger.LogInformation("Model download: {Status}", progress.Status);
					}
				});

				if (success)
				{
					logger.LogInformation("Model bootstrap completed successfully");
				}
				else
				{
					logger.LogError("Model bootstrap failed");
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error bootstrapping models");
			}
		});

		return app;
	}

	private static string GetDefaultEmbeddingModelPath()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
	}
}
