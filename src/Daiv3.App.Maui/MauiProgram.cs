using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Daiv3.App.Maui.Pages;
using Daiv3.App.Maui.ViewModels;
using Daiv3.App.Maui.Services;
using Daiv3.Knowledge;
using Daiv3.Knowledge.Embedding;
using Daiv3.Infrastructure.Shared.Logging;
using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.Orchestration;
using Daiv3.Persistence;
using Daiv3.Persistence.Services;
using Daiv3.FoundryLocal.Management;

namespace Daiv3.App.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        try
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

        // Register system metrics service (CT-REQ-006: CPU/Memory/Disk)
        builder.Services.AddSingleton<ISystemMetricsService, SystemMetricsService>();

        // Register Admin Dashboard Service (CT-REQ-010: System Admin Dashboard)
        builder.Services.Configure<AdminDashboardOptions>(options =>
        {
            // Use default values from AdminDashboardOptions class
        });
        builder.Services.AddSingleton<IAdminDashboardService, AdminDashboardService>();

        // Register Dashboard Service (CT-REQ-003, CT-REQ-006)
        var dashboardConfig = new DashboardConfiguration
        {
            RefreshIntervalMs = 3000,
            EnableCaching = true,
            EnableLogging = true,
            ContinueOnError = true,
            DataCollectionTimeoutMs = 5000
        };
        builder.Services.AddSingleton(dashboardConfig);
        builder.Services.AddSingleton<IDashboardService>(serviceProvider =>
            new DashboardService(
                serviceProvider.GetRequiredService<ILogger<DashboardService>>(),
                serviceProvider.GetService<IModelQueue>(),                  // Optional - may not be registered
                dashboardConfig,
                serviceProvider.GetRequiredService<IServiceScopeFactory>(), // Always available
                serviceProvider.GetService<AgentExecutionMetricsCollector>(), // Optional - registered when orchestration is active
                serviceProvider.GetRequiredService<ISystemMetricsService>()));

        // Register ViewModels
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<AdminDashboardViewModel>();
        builder.Services.AddSingleton<ProjectsViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>(serviceProvider =>
            new SettingsViewModel(
                serviceProvider.GetRequiredService<ILogger<SettingsViewModel>>(),
                serviceProvider.GetRequiredService<ISettingsService>(),
                serviceProvider.GetRequiredService<ISettingsInitializer>(),
                serviceProvider.GetRequiredService<IFoundryLocalManagementService>()));
        builder.Services.AddTransient<IndexingViewModel>();

        // Register Pages
        builder.Services.AddSingleton<ChatPage>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<AdminDashboardPage>();
        builder.Services.AddSingleton<ProjectsPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<IndexingPage>();
        builder.Services.AddSingleton<MainPage>();

        var modelPath = GetDefaultEmbeddingModelPath();
        // Add persistence layer (database, repositories, settings management)
        builder.Services.AddPersistence();

        builder.Services.AddEmbeddingServices(options =>
        {
            options.ModelPath = modelPath;
        });

        // Add knowledge layer (Tier 1/2 vector indexing and search)
        builder.Services.AddKnowledgeLayer();

        // Add model execution layer (Foundry Local integration)
        builder.Services.AddModelExecutionServices();

        var app = builder.Build();

        // Bootstrap embedding and OCR models on app startup
        // This runs in the background and logs progress
        var logger = app.Services.GetRequiredService<ILogger<Daiv3.Knowledge.Embedding.EmbeddingModelBootstrapService>>();
        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Starting model bootstrap (embedding Tier 1, Tier 2, and OCR models)");
                var bootstrapService = app.Services.GetRequiredService<Daiv3.Knowledge.Embedding.EmbeddingModelBootstrapService>();

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
        catch (Exception ex)
        {
            // Log to console and file
            Console.WriteLine($"FATAL ERROR creating MAUI app: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Inner Message: {ex.InnerException.Message}");
                Console.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
            }
            
            // Write to error file as well
            var errorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "logs", "fatal-error.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
            File.WriteAllText(errorPath, $"{DateTime.Now:O}\n{ex}\n{ex.InnerException}");
            
            throw; // Re-throw to prevent app from starting in bad state
        }
    }

    private static string GetDefaultEmbeddingModelPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
    }
}
