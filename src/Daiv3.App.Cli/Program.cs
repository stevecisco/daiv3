using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Daiv3.Infrastructure.Shared.Logging;
using Daiv3.Persistence;
using Daiv3.Knowledge.Embedding;

namespace Daiv3.App.Cli;

/// <summary>
/// Command-line interface for Daiv3.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Daiv3 CLI - Distributed AI System");

        // Database commands
        var dbCommand = new Command("db", "Database management commands");
        
        var dbInitCommand = new Command("init", "Initialize the database");
        dbInitCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await DatabaseInitCommand(host);
            Environment.Exit(exitCode);
        });

        var dbStatusCommand = new Command("status", "Show database status");
        dbStatusCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await DatabaseStatusCommand(host);
            Environment.Exit(exitCode);
        });

        dbCommand.AddCommand(dbInitCommand);
        dbCommand.AddCommand(dbStatusCommand);
        rootCommand.AddCommand(dbCommand);

        // Dashboard command
        var dashboardCommand = new Command("dashboard", "Show system dashboard and status");
        dashboardCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await Task.FromResult(DashboardCommand(host));
            Environment.Exit(exitCode);
        });
        rootCommand.AddCommand(dashboardCommand);

        // Chat command
        var chatCommand = new Command("chat", "Interactive chat interface");
        var messageOption = new Option<string?>(
            aliases: new[] { "--message", "-m" },
            description: "Send a single message and exit");
        chatCommand.AddOption(messageOption);
        chatCommand.SetHandler(async (string? message) =>
        {
            var host = CreateHost();
            var exitCode = await Task.FromResult(ChatCommand(host, message));
            Environment.Exit(exitCode);
        }, messageOption);
        rootCommand.AddCommand(chatCommand);

        // Projects command
        var projectsCommand = new Command("projects", "Project management commands");
        
        var projectsListCommand = new Command("list", "List all projects");
        projectsListCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await Task.FromResult(ProjectsListCommand(host));
            Environment.Exit(exitCode);
        });

        var projectsCreateCommand = new Command("create", "Create a new project");
        var projectNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Project name") { IsRequired = true };
        var projectDescOption = new Option<string>(
            aliases: new[] { "--description", "-d" },
            description: "Project description",
            getDefaultValue: () => "");
        projectsCreateCommand.AddOption(projectNameOption);
        projectsCreateCommand.AddOption(projectDescOption);
        projectsCreateCommand.SetHandler(async (string name, string desc) =>
        {
            var host = CreateHost();
            var exitCode = await Task.FromResult(ProjectsCreateCommand(host, name, desc));
            Environment.Exit(exitCode);
        }, projectNameOption, projectDescOption);

        projectsCommand.AddCommand(projectsListCommand);
        projectsCommand.AddCommand(projectsCreateCommand);
        rootCommand.AddCommand(projectsCommand);

        // Settings command
        var settingsCommand = new Command("settings", "Configuration management");
        
        var settingsShowCommand = new Command("show", "Show current settings");
        settingsShowCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await Task.FromResult(SettingsShowCommand(host));
            Environment.Exit(exitCode);
        });

        settingsCommand.AddCommand(settingsShowCommand);
        rootCommand.AddCommand(settingsCommand);

        // Embedding commands
        var embeddingCommand = new Command("embedding", "Embedding generation and testing");
        
        var embeddingTestCommand = new Command("test", "Test embedding generation with sample text");
        var textOption = new Option<string>(
            aliases: new[] { "--text", "-t" },
            description: "Text to embed",
            getDefaultValue: () => "The quick brown fox jumps over the lazy dog");
        embeddingTestCommand.AddOption(textOption);
        embeddingTestCommand.SetHandler(async (string text) =>
        {
            var host = CreateHost();
            var exitCode = await EmbeddingTestCommand(host, text);
            Environment.Exit(exitCode);
        }, textOption);

        embeddingCommand.AddCommand(embeddingTestCommand);
        rootCommand.AddCommand(embeddingCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddFileLogging("cli", LogLevel.Debug);
                });

                // Add persistence layer
                services.AddPersistence(options =>
                {
                    // Use default options (database in %LOCALAPPDATA%\Daiv3\)
                });

                var modelPath = GetDefaultEmbeddingModelPath();
                services.AddEmbeddingServices(options =>
                {
                    options.ModelPath = modelPath;
                });
            })
            .Build();
    }

    private static string GetDefaultEmbeddingModelPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "model.onnx");
    }

    private static async Task<int> DatabaseInitCommand(IHost host)
    {
        try
        {
            Console.WriteLine("Initializing Daiv3 database...");
            
            await host.Services.InitializeDatabaseAsync();
            
            var dbContext = host.Services.GetRequiredService<IDatabaseContext>();
            var version = await dbContext.GetSchemaVersionAsync();
            
            Console.WriteLine($"✓ Database initialized successfully");
            Console.WriteLine($"  Path: {dbContext.DatabasePath}");
            Console.WriteLine($"  Schema Version: {version}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to initialize database:");
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack Trace:");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<int> DatabaseStatusCommand(IHost host)
    {
        try
        {
            var dbContext = host.Services.GetRequiredService<IDatabaseContext>();
            
            Console.WriteLine("Database Status:");
            Console.WriteLine($"  Path: {dbContext.DatabasePath}");
            
            if (!File.Exists(dbContext.DatabasePath))
            {
                Console.WriteLine($"  Status: Not initialized");
                return 0;
            }

            var fileInfo = new FileInfo(dbContext.DatabasePath);
            Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
            Console.WriteLine($"  Last Modified: {fileInfo.LastWriteTime}");
            
            var version = await dbContext.GetSchemaVersionAsync();
            Console.WriteLine($"  Schema Version: {version}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get database status: {ex.Message}");
            return 1;
        }
    }

    private static int DashboardCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Displaying dashboard");

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("                    DAIV3 DASHBOARD                        ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Hardware Status
            Console.WriteLine("HARDWARE STATUS:");
            Console.WriteLine("  Overall: System Ready");
            Console.WriteLine("  NPU: Detection pending (integration pending)");
            Console.WriteLine("  GPU: Detection pending (integration pending)");
            Console.WriteLine();

            // Task Queue Status
            Console.WriteLine("TASK QUEUE:");
            Console.WriteLine("  Queued Tasks: 0");
            Console.WriteLine("  Completed Tasks: 0");
            Console.WriteLine("  Current Activity: Ready for tasks");
            Console.WriteLine();

            Console.WriteLine("NOTE: Full hardware detection and queue monitoring pending integration.");
            Console.WriteLine("      Use 'db status' to check database, 'projects list' for projects.");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show dashboard: {ex.Message}");
            return 1;
        }
    }

    private static int ChatCommand(IHost host, string? singleMessage)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            
            if (singleMessage != null)
            {
                // Single message mode
                Console.WriteLine($"User: {singleMessage}");
                Console.WriteLine($"AI: Echo: {singleMessage} (Orchestration integration pending)");
                logger.LogInformation("Processed single chat message");
                return 0;
            }

            // Interactive mode
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("                 DAIV3 CHAT INTERFACE                      ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("Type your message and press Enter. Type 'exit' to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                // TODO: Integrate with orchestration layer
                Console.WriteLine($"AI: Echo: {input} (Orchestration integration pending)");
                logger.LogInformation("Processed chat message: {Message}", input);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Chat command failed: {ex.Message}");
            return 1;
        }
    }

    private static int ProjectsListCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Listing projects");

            Console.WriteLine("PROJECTS:");
            Console.WriteLine("  (No projects - persistence integration pending)");
            Console.WriteLine();
            Console.WriteLine("NOTE: Project CRUD operations pending persistence layer integration.");
            Console.WriteLine("      Use 'projects create --name \"My Project\"' to create a project.");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list projects: {ex.Message}");
            return 1;
        }
    }

    private static int ProjectsCreateCommand(IHost host, string name, string description)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Creating project: {Name}", name);

            var projectId = Guid.NewGuid();
            Console.WriteLine($"✓ Project created successfully (simulation - persistence integration pending)");
            Console.WriteLine($"  ID: {projectId}");
            Console.WriteLine($"  Name: {name}");
            Console.WriteLine($"  Description: {description}");
            Console.WriteLine($"  Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create project: {ex.Message}");
            return 1;
        }
    }

    private static int SettingsShowCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Displaying settings");

            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3", "Data");
            var modelsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3", "Models");

            Console.WriteLine("CURRENT SETTINGS:");
            Console.WriteLine();
            Console.WriteLine("Directories:");
            Console.WriteLine($"  Data Directory: {dataDir}");
            Console.WriteLine($"  Models Directory: {modelsDir}");
            Console.WriteLine();
            Console.WriteLine("Hardware Preferences:");
            Console.WriteLine("  Use NPU: True (default)");
            Console.WriteLine("  Use GPU: True (default)");
            Console.WriteLine();
            Console.WriteLine("Model Execution:");
            Console.WriteLine("  Allow Online Providers: False (default)");
            Console.WriteLine("  Token Budget: 8192 (default)");
            Console.WriteLine();
            Console.WriteLine("NOTE: Settings persistence integration pending.");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show settings: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> EmbeddingTestCommand(IHost host, string text)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Testing embedding generation with text: {Text}", text);

            var generator = host.Services.GetRequiredService<IEmbeddingGenerator>();
            
            Console.WriteLine("EMBEDDING TEST");
            Console.WriteLine("==============");
            Console.WriteLine($"Input text: {text}");
            Console.WriteLine();
            Console.Write("Generating embedding... ");
            
            var embedding = await generator.GenerateEmbeddingAsync(text);
            
            Console.WriteLine("✓ Success!");
            Console.WriteLine();
            Console.WriteLine($"Embedding dimensions: {embedding.Length}");
            
            // Calculate basic statistics
            float magnitude = 0;
            float max = embedding[0];
            float min = embedding[0];
            
            for (int i = 0; i < embedding.Length; i++)
            {
                magnitude += embedding[i] * embedding[i];
                if (embedding[i] > max) max = embedding[i];
                if (embedding[i] < min) min = embedding[i];
            }
            
            magnitude = (float)Math.Sqrt(magnitude);
            
            Console.WriteLine($"Vector magnitude: {magnitude:F4}");
            Console.WriteLine($"Value range: [{min:F6}, {max:F6}]");
            Console.WriteLine();
            Console.WriteLine("First 10 embedding values:");
            for (int i = 0; i < Math.Min(10, embedding.Length); i++)
            {
                Console.WriteLine($"  [{i,3}] = {embedding[i]:F6}");
            }
            
            if (embedding.Length > 10)
            {
                Console.WriteLine($"  ... ({embedding.Length - 10} more values)");
            }
            
            logger.LogInformation("✓ Embedding test completed successfully. Dimensions: {Dimensions}, Magnitude: {Magnitude:F4}",
                embedding.Length, magnitude);
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to generate embedding: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Embedding generation failed");
            return 1;
        }
    }

}
