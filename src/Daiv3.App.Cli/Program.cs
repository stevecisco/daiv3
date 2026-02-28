using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Daiv3.Infrastructure.Shared.Logging;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Daiv3.Knowledge.Embedding;
using Daiv3.Scheduler;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Configuration;

namespace Daiv3.App.Cli;

/// <summary>
/// Command-line interface for Daiv3.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Bootstrap all models (embeddings Tier 1/2, OCR, and multimodal) before processing commands
        await EnsureAllModelsAsync();

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
            var exitCode = await ProjectsListCommand(host);
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
        var projectRootPathOption = new Option<string[]>(
            aliases: new[] { "--root-path", "-r" },
            description: "Project root path for scoped indexing (repeat option for multiple roots)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        var projectInstructionsOption = new Option<string?>(
            aliases: new[] { "--instructions", "-i" },
            description: "Project-level instructions (system prompt and constraints)");
        var preferredModelOption = new Option<string?>(
            aliases: new[] { "--preferred-model" },
            description: "Preferred model ID for project tasks");
        var fallbackModelOption = new Option<string?>(
            aliases: new[] { "--fallback-model" },
            description: "Fallback model ID when preferred model is unavailable");
        projectsCreateCommand.AddOption(projectNameOption);
        projectsCreateCommand.AddOption(projectDescOption);
        projectsCreateCommand.AddOption(projectRootPathOption);
        projectsCreateCommand.AddOption(projectInstructionsOption);
        projectsCreateCommand.AddOption(preferredModelOption);
        projectsCreateCommand.AddOption(fallbackModelOption);
        projectsCreateCommand.SetHandler(async (string name, string desc, string[] rootPaths, string? instructions, string? preferredModel, string? fallbackModel) =>
        {
            var host = CreateHost();
            var exitCode = await ProjectsCreateCommand(host, name, desc, rootPaths, instructions, preferredModel, fallbackModel);
            Environment.Exit(exitCode);
        }, projectNameOption, projectDescOption, projectRootPathOption, projectInstructionsOption, preferredModelOption, fallbackModelOption);

        projectsCommand.AddCommand(projectsListCommand);
        projectsCommand.AddCommand(projectsCreateCommand);
        rootCommand.AddCommand(projectsCommand);

        // Tasks command
        var tasksCommand = new Command("tasks", "Task management commands");
        
        var tasksListCommand = new Command("list", "List all tasks");
        var taskProjectIdOption = new Option<string?>(
            aliases: new[] { "--project-id", "-p" },
            description: "Filter by project ID");
        var taskStatusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status (pending, queued, in-progress, complete, failed, blocked)");
        tasksListCommand.AddOption(taskProjectIdOption);
        tasksListCommand.AddOption(taskStatusOption);
        tasksListCommand.SetHandler(async (string? projectId, string? status) =>
        {
            var host = CreateHost();
            var exitCode = await TasksListCommand(host, projectId, status);
            Environment.Exit(exitCode);
        }, taskProjectIdOption, taskStatusOption);

        var tasksCreateCommand = new Command("create", "Create a new task");
        var taskTitleOption = new Option<string>(
            aliases: new[] { "--title", "-t" },
            description: "Task title") { IsRequired = true };
        var taskDescriptionOption = new Option<string?>(
            aliases: new[] { "--description", "-d" },
            description: "Task description");
        var taskProjectOption = new Option<string?>(
            aliases: new[] { "--project-id", "-p" },
            description: "Project ID");
        var taskPriorityOption = new Option<int>(
            aliases: new[] { "--priority" },
            description: "Priority (0-9, default: 5)",
            getDefaultValue: () => 5);
        var taskDependenciesOption = new Option<string[]>(
            aliases: new[] { "--dependency", "--dep" },
            description: "Task dependency (repeat option for multiple)") 
        { 
            Arity = ArgumentArity.ZeroOrMore 
        };
        tasksCreateCommand.AddOption(taskTitleOption);
        tasksCreateCommand.AddOption(taskDescriptionOption);
        tasksCreateCommand.AddOption(taskProjectOption);
        tasksCreateCommand.AddOption(taskPriorityOption);
        tasksCreateCommand.AddOption(taskDependenciesOption);
        tasksCreateCommand.SetHandler(async (string title, string? description, string? projectId, int priority, string[] dependencies) =>
        {
            var host = CreateHost();
            var exitCode = await TasksCreateCommand(host, title, description, projectId, priority, dependencies);
            Environment.Exit(exitCode);
        }, taskTitleOption, taskDescriptionOption, taskProjectOption, taskPriorityOption, taskDependenciesOption);

        var tasksUpdateCommand = new Command("update", "Update a task");
        var taskIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Task ID") { IsRequired = true };
        var taskUpdateStatusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Update status");
        var taskUpdatePriorityOption = new Option<int?>(
            aliases: new[] { "--priority" },
            description: "Update priority (0-9)");
        tasksUpdateCommand.AddOption(taskIdOption);
        tasksUpdateCommand.AddOption(taskUpdateStatusOption);
        tasksUpdateCommand.AddOption(taskUpdatePriorityOption);
        tasksUpdateCommand.SetHandler(async (string id, string? status, int? priority) =>
        {
            var host = CreateHost();
            var exitCode = await TasksUpdateCommand(host, id, status, priority);
            Environment.Exit(exitCode);
        }, taskIdOption, taskUpdateStatusOption, taskUpdatePriorityOption);

        tasksCommand.AddCommand(tasksListCommand);
        tasksCommand.AddCommand(tasksCreateCommand);
        tasksCommand.AddCommand(tasksUpdateCommand);
        rootCommand.AddCommand(tasksCommand);

        // Schedule command
        var scheduleCommand = new Command("schedule", "Job scheduling commands");
        
        var scheduleListCommand = new Command("list", "List all scheduled jobs");
        var scheduleStatusFilter = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status (pending, scheduled, running, completed, failed, cancelled)");
        scheduleListCommand.AddOption(scheduleStatusFilter);
        scheduleListCommand.SetHandler(async (string? status) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleListCommand(host, status);
            Environment.Exit(exitCode);
        }, scheduleStatusFilter);

        var scheduleCronCommand = new Command("cron", "Schedule a job using cron expression");
        var cronJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name") { IsRequired = true };
        var cronExpressionOption = new Option<string>(
            aliases: new[] { "--expression", "-e" },
            description: "Cron expression (e.g., '0 0 * * *' for daily at midnight)") { IsRequired = true };
        scheduleCronCommand.AddOption(cronJobNameOption);
        scheduleCronCommand.AddOption(cronExpressionOption);
        scheduleCronCommand.SetHandler(async (string jobName, string cronExpression) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleCronCommand(host, jobName, cronExpression);
            Environment.Exit(exitCode);
        }, cronJobNameOption, cronExpressionOption);

        var scheduleOnceCommand = new Command("once", "Schedule a one-time job");
        var onceJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name") { IsRequired = true };
        var onceTimeOption = new Option<DateTime>(
            aliases: new[] { "--time", "-t" },
            description: "UTC time to run (ISO 8601 format, e.g., '2026-03-01T10:30:00Z')") { IsRequired = true };
        scheduleOnceCommand.AddOption(onceJobNameOption);
        scheduleOnceCommand.AddOption(onceTimeOption);
        scheduleOnceCommand.SetHandler(async (string jobName, DateTime time) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleOnceCommand(host, jobName, time);
            Environment.Exit(exitCode);
        }, onceJobNameOption, onceTimeOption);

        var scheduleEventCommand = new Command("on-event", "Schedule a job to run on event");
        var eventJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name") { IsRequired = true };
        var eventTypeOption = new Option<string>(
            aliases: new[] { "--event-type", "-e" },
            description: "Event type to listen for") { IsRequired = true };
        scheduleEventCommand.AddOption(eventJobNameOption);
        scheduleEventCommand.AddOption(eventTypeOption);
        scheduleEventCommand.SetHandler(async (string jobName, string eventType) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleEventCommand(host, jobName, eventType);
            Environment.Exit(exitCode);
        }, eventJobNameOption, eventTypeOption);

        var scheduleCancelCommand = new Command("cancel", "Cancel a scheduled job");
        var cancelJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID") { IsRequired = true };
        scheduleCancelCommand.AddOption(cancelJobIdOption);
        scheduleCancelCommand.SetHandler(async (string jobId) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleCancelCommand(host, jobId);
            Environment.Exit(exitCode);
        }, cancelJobIdOption);

        var scheduleInfoCommand = new Command("info", "Show detailed information about a scheduled job");
        var infoJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID") { IsRequired = true };
        scheduleInfoCommand.AddOption(infoJobIdOption);
        scheduleInfoCommand.SetHandler(async (string jobId) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleInfoCommand(host, jobId);
            Environment.Exit(exitCode);
        }, infoJobIdOption);

        scheduleCommand.AddCommand(scheduleListCommand);
        scheduleCommand.AddCommand(scheduleCronCommand);
        scheduleCommand.AddCommand(scheduleOnceCommand);
        scheduleCommand.AddCommand(scheduleEventCommand);
        scheduleCommand.AddCommand(scheduleCancelCommand);
        scheduleCommand.AddCommand(scheduleInfoCommand);

        var schedulePauseCommand = new Command("pause", "Pause a scheduled job");
        var pauseJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID") { IsRequired = true };
        schedulePauseCommand.AddOption(pauseJobIdOption);
        schedulePauseCommand.SetHandler(async (string jobId) =>
        {
            var host = CreateHost();
            var exitCode = await SchedulePauseCommand(host, jobId);
            Environment.Exit(exitCode);
        }, pauseJobIdOption);

        var scheduleResumeCommand = new Command("resume", "Resume a paused job");
        var resumeJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID") { IsRequired = true };
        scheduleResumeCommand.AddOption(resumeJobIdOption);
        scheduleResumeCommand.SetHandler(async (string jobId) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleResumeCommand(host, jobId);
            Environment.Exit(exitCode);
        }, resumeJobIdOption);

        var scheduleModifyCommand = new Command("modify", "Modify a scheduled job");
        var modifyJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID") { IsRequired = true };
        var modifyTimeOption = new Option<DateTime?>(
            aliases: new[] { "--time", "-t" },
            description: "New scheduled time (UTC, ISO 8601 format) for one-time jobs");
        var modifyIntervalOption = new Option<uint?>(
            aliases: new[] { "--interval", "-i" },
            description: "New interval in seconds for recurring jobs");
        var modifyCronOption = new Option<string>(
            aliases: new[] { "--cron", "-c" },
            description: "New cron expression for cron jobs");
        var modifyEventTypeOption = new Option<string>(
            aliases: new[] { "--event-type", "-e" },
            description: "New event type for event-triggered jobs");
        scheduleModifyCommand.AddOption(modifyJobIdOption);
        scheduleModifyCommand.AddOption(modifyTimeOption);
        scheduleModifyCommand.AddOption(modifyIntervalOption);
        scheduleModifyCommand.AddOption(modifyCronOption);
        scheduleModifyCommand.AddOption(modifyEventTypeOption);
        scheduleModifyCommand.SetHandler(async (string jobId, DateTime? time, uint? interval, string? cron, string? eventType) =>
        {
            var host = CreateHost();
            var exitCode = await ScheduleModifyCommand(host, jobId, time, interval, cron, eventType);
            Environment.Exit(exitCode);
        }, modifyJobIdOption, modifyTimeOption, modifyIntervalOption, modifyCronOption, modifyEventTypeOption);

        scheduleCommand.AddCommand(schedulePauseCommand);
        scheduleCommand.AddCommand(scheduleResumeCommand);
        scheduleCommand.AddCommand(scheduleModifyCommand);
        rootCommand.AddCommand(scheduleCommand);

        // Agent commands
        var agentCommand = new Command("agent", "Agent management and execution commands");

        var agentListCommand = new Command("list", "List all agents");
        agentListCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await AgentListCommand(host);
            Environment.Exit(exitCode);
        });

        var agentCreateCommand = new Command("create", "Create a new agent");
        var agentNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Agent name") { IsRequired = true };
        var agentPurposeOption = new Option<string>(
            aliases: new[] { "--purpose", "-p" },
            description: "Agent purpose or description") { IsRequired = true };
        var agentSkillsOption = new Option<string[]>(
            aliases: new[] { "--skills", "-s" },
            description: "Enabled skill names (repeat option for multiple skills)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        agentCreateCommand.AddOption(agentNameOption);
        agentCreateCommand.AddOption(agentPurposeOption);
        agentCreateCommand.AddOption(agentSkillsOption);
        agentCreateCommand.SetHandler(async (string name, string purpose, string[] skills) =>
        {
            var host = CreateHost();
            var exitCode = await AgentCreateCommand(host, name, purpose, skills);
            Environment.Exit(exitCode);
        }, agentNameOption, agentPurposeOption, agentSkillsOption);

        var agentGetCommand = new Command("get", "Get agent details");
        var agentIdGetOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Agent ID") { IsRequired = true };
        agentGetCommand.AddOption(agentIdGetOption);
        agentGetCommand.SetHandler(async (string id) =>
        {
            var host = CreateHost();
            var exitCode = await AgentGetCommand(host, id);
            Environment.Exit(exitCode);
        }, agentIdGetOption);

        var agentDeleteCommand = new Command("delete", "Delete an agent");
        var agentIdDeleteOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Agent ID") { IsRequired = true };
        agentDeleteCommand.AddOption(agentIdDeleteOption);
        agentDeleteCommand.SetHandler(async (string id) =>
        {
            var host = CreateHost();
            var exitCode = await AgentDeleteCommand(host, id);
            Environment.Exit(exitCode);
        }, agentIdDeleteOption);

        var agentExecuteCommand = new Command("execute", "Execute a task using an agent");
        var agentIdExecuteOption = new Option<string>(
            aliases: new[] { "--agent-id", "-a" },
            description: "Agent ID to use for execution") { IsRequired = true };
        var agentGoalOption = new Option<string>(
            aliases: new[] { "--goal", "-g" },
            description: "Task goal or objective") { IsRequired = true };
        var agentMaxIterationsOption = new Option<int>(
            aliases: new[] { "--max-iterations", "-i" },
            description: "Maximum number of iterations (default: 10)",
            getDefaultValue: () => 10);
        var agentTimeoutOption = new Option<int>(
            aliases: new[] { "--timeout", "-t" },
            description: "Execution timeout in seconds (default: 600)",
            getDefaultValue: () => 600);
        var agentTokenBudgetOption = new Option<int>(
            aliases: new[] { "--token-budget", "-b" },
            description: "Token budget for execution (default: 10000)",
            getDefaultValue: () => 10_000);
        agentExecuteCommand.AddOption(agentIdExecuteOption);
        agentExecuteCommand.AddOption(agentGoalOption);
        agentExecuteCommand.AddOption(agentMaxIterationsOption);
        agentExecuteCommand.AddOption(agentTimeoutOption);
        agentExecuteCommand.AddOption(agentTokenBudgetOption);
        agentExecuteCommand.SetHandler(async (string agentId, string goal, int maxIterations, int timeout, int tokenBudget) =>
        {
            var host = CreateHost();
            var exitCode = await AgentExecuteCommand(host, agentId, goal, maxIterations, timeout, tokenBudget);
            Environment.Exit(exitCode);
        }, agentIdExecuteOption, agentGoalOption, agentMaxIterationsOption, agentTimeoutOption, agentTokenBudgetOption);

        var agentLoadCommand = new Command("load", "Load agent(s) from a configuration file or directory");
        var agentConfigPathOption = new Option<string>(
            aliases: new[] { "--path", "-p" },
            description: "Path to configuration file (.json) or directory of configuration files") { IsRequired = true };
        var agentRecursiveOption = new Option<bool>(
            aliases: new[] { "--recursive", "-r" },
            description: "When loading from directory, search subdirectories recursively",
            getDefaultValue: () => false);
        var agentValidateOnlyOption = new Option<bool>(
            aliases: new[] { "--validate-only" },
            description: "Validate configuration without creating agents",
            getDefaultValue: () => false);
        agentLoadCommand.AddOption(agentConfigPathOption);
        agentLoadCommand.AddOption(agentRecursiveOption);
        agentLoadCommand.AddOption(agentValidateOnlyOption);
        agentLoadCommand.SetHandler(async (string path, bool recursive, bool validateOnly) =>
        {
            var host = CreateHost();
            var exitCode = await AgentLoadCommand(host, path, recursive, validateOnly);
            Environment.Exit(exitCode);
        }, agentConfigPathOption, agentRecursiveOption, agentValidateOnlyOption);

        agentCommand.AddCommand(agentListCommand);
        agentCommand.AddCommand(agentCreateCommand);
        agentCommand.AddCommand(agentGetCommand);
        agentCommand.AddCommand(agentDeleteCommand);
        agentCommand.AddCommand(agentExecuteCommand);
        agentCommand.AddCommand(agentLoadCommand);
        rootCommand.AddCommand(agentCommand);

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

        // Multimodal CLIP commands
        var multimodalCommand = new Command("multimodal", "CLIP multimodal image-text embedding testing");
        
        var multimodalTextCommand = new Command("text", "Test text embedding with CLIP");
        var multimodalTextOption = new Option<string>(
            aliases: new[] { "--text", "-t" },
            description: "Text to encode",
            getDefaultValue: () => "a dog and a cat");
        multimodalTextCommand.AddOption(multimodalTextOption);
        multimodalTextCommand.SetHandler(async (string text) =>
        {
            var host = CreateHost();
            var exitCode = await MultimodalTextCommand(host, text);
            Environment.Exit(exitCode);
        }, multimodalTextOption);

        multimodalCommand.AddCommand(multimodalTextCommand);
        rootCommand.AddCommand(multimodalCommand);

        // OCR commands
        var ocrCommand = new Command("ocr", "Optical Character Recognition (OCR) testing");
        
        var ocrTestCommand = new Command("test", "Test OCR with sample text");
        ocrTestCommand.SetHandler(async () =>
        {
            var host = CreateHost();
            var exitCode = await OcrTestCommand(host);
            Environment.Exit(exitCode);
        });

        ocrCommand.AddCommand(ocrTestCommand);
        rootCommand.AddCommand(ocrCommand);

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

                // Add orchestration services (includes IAgentManager, ISkillRegistry, etc.)
                services.AddOrchestrationServices();

                // Register agent configuration loader
                services.AddScoped<AgentConfigFileLoader>();

                var modelPath = GetDefaultEmbeddingModelPath();
                services.AddEmbeddingServices(options =>
                {
                    options.ModelPath = modelPath;
                });

                // Add scheduler
                services.AddScheduler(options =>
                {
                    options.CheckIntervalMilliseconds = 1000; // Check every second
                    options.JobTimeoutSeconds = 300; // 5 minute timeout
                    options.MaxConcurrentJobs = 5;
                });
            })
            .Build();
    }

    private static string GetDefaultEmbeddingModelPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
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

    private static async Task<int> ProjectsListCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Listing projects");

            var projects = await projectRepository.GetAllAsync().ConfigureAwait(false);

            Console.WriteLine("PROJECTS:");
            if (projects.Count == 0)
            {
                Console.WriteLine("  (No projects found)");
                Console.WriteLine();
                Console.WriteLine("Use 'projects create --name \"My Project\"' to create a project.");
                return 0;
            }

            foreach (var project in projects)
            {
                var rootPaths = ProjectRootPaths.Parse(project.RootPaths);
                var configuration = ProjectConfiguration.Parse(project.ConfigJson);
                Console.WriteLine($"  ID: {project.ProjectId}");
                Console.WriteLine($"  Name: {project.Name}");
                Console.WriteLine($"  Description: {project.Description ?? string.Empty}");
                Console.WriteLine("  Root Paths:");
                if (rootPaths.Count == 0)
                {
                    Console.WriteLine("    - (none)");
                }
                else
                {
                    foreach (var rootPath in rootPaths)
                    {
                        Console.WriteLine($"    - {rootPath}");
                    }
                }
                Console.WriteLine($"  Instructions: {configuration.Instructions ?? string.Empty}");
                Console.WriteLine($"  Preferred Model: {configuration.ModelPreferences.PreferredModelId ?? string.Empty}");
                Console.WriteLine($"  Fallback Model: {configuration.ModelPreferences.FallbackModelId ?? string.Empty}");
                Console.WriteLine($"  Status: {project.Status}");
                Console.WriteLine($"  Created: {FromUnixSeconds(project.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Updated: {FromUnixSeconds(project.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list projects: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ProjectsCreateCommand(
        IHost host,
        string name,
        string description,
        IReadOnlyList<string> rootPaths,
        string? instructions,
        string? preferredModel,
        string? fallbackModel)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Creating project: {Name}", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                Console.WriteLine("✗ Project name is required.");
                return 1;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var projectId = Guid.NewGuid().ToString();
            var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            var normalizedRootPaths = NormalizeProjectRootPaths(rootPaths);
            var serializedRootPaths = ProjectRootPaths.Serialize(normalizedRootPaths);
            var projectConfiguration = new ProjectConfiguration
            {
                Instructions = instructions,
                ModelPreferences = new ProjectModelPreferences
                {
                    PreferredModelId = preferredModel,
                    FallbackModelId = fallbackModel
                }
            };
            var projectConfigJson = projectConfiguration.ToJsonOrNull();
            var effectiveProjectConfiguration = ProjectConfiguration.Parse(projectConfigJson);

            await projectRepository.AddAsync(new Project
            {
                ProjectId = projectId,
                Name = name.Trim(),
                Description = normalizedDescription,
                RootPaths = serializedRootPaths,
                CreatedAt = now,
                UpdatedAt = now,
                Status = "active",
                ConfigJson = projectConfigJson
            }).ConfigureAwait(false);

            Console.WriteLine("✓ Project created successfully");
            Console.WriteLine($"  ID: {projectId}");
            Console.WriteLine($"  Name: {name.Trim()}");
            Console.WriteLine($"  Description: {normalizedDescription ?? string.Empty}");
            Console.WriteLine("  Root Paths:");
            foreach (var rootPath in normalizedRootPaths)
            {
                Console.WriteLine($"    - {rootPath}");
            }
            Console.WriteLine($"  Instructions: {effectiveProjectConfiguration.Instructions ?? string.Empty}");
            Console.WriteLine($"  Preferred Model: {effectiveProjectConfiguration.ModelPreferences.PreferredModelId ?? string.Empty}");
            Console.WriteLine($"  Fallback Model: {effectiveProjectConfiguration.ModelPreferences.FallbackModelId ?? string.Empty}");
            Console.WriteLine("  Status: active");
            Console.WriteLine($"  Created: {FromUnixSeconds(now):yyyy-MM-dd HH:mm:ss} UTC");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create project: {ex.Message}");
            return 1;
        }
    }

    private static IReadOnlyList<string> NormalizeProjectRootPaths(IReadOnlyList<string> rootPaths)
    {
        var candidatePaths = rootPaths.Count == 0 ? [Directory.GetCurrentDirectory()] : rootPaths;
        var normalizedPaths = ProjectRootPaths.Normalize(candidatePaths);

        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("At least one valid root path is required.", nameof(rootPaths));
        }

        foreach (var rootPath in normalizedPaths)
        {
            if (!Directory.Exists(rootPath))
            {
                throw new DirectoryNotFoundException($"Root path does not exist: {rootPath}");
            }
        }

        return normalizedPaths;
    }

    private static DateTimeOffset FromUnixSeconds(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

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

    private static async Task<int> TasksListCommand(
        IHost host,
        string? projectId,
        string? status)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var taskRepository = host.Services.GetRequiredService<TaskRepository>();
            logger.LogInformation("Listing tasks");

            IReadOnlyList<ProjectTask> tasks;
            if (!string.IsNullOrWhiteSpace(status))
            {
                tasks = await taskRepository.GetByStatusAsync(status, CancellationToken.None).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(projectId))
            {
                tasks = await taskRepository.GetByProjectIdAsync(projectId, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                tasks = await taskRepository.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
            }

            Console.WriteLine("TASKS:");
            if (tasks.Count == 0)
            {
                Console.WriteLine("  (No tasks found)");
                Console.WriteLine();
                Console.WriteLine("Use 'tasks create --title \"My Task\"' to create a task.");
                return 0;
            }

            foreach (var task in tasks)
            {
                Console.WriteLine($"  ID: {task.TaskId}");
                Console.WriteLine($"  Title: {task.Title}");
                Console.WriteLine($"  Description: {task.Description ?? string.Empty}");
                Console.WriteLine($"  Project: {task.ProjectId ?? "(none)"}");
                Console.WriteLine($"  Status: {task.Status}");
                Console.WriteLine($"  Priority: {task.Priority}");
                
                if (!string.IsNullOrEmpty(task.DependenciesJson))
                {
                    Console.WriteLine($"  Dependencies: {task.DependenciesJson}");
                }

                if (task.NextRunAt.HasValue)
                {
                    Console.WriteLine($"  Next Run: {FromUnixSeconds(task.NextRunAt.Value):yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (task.LastRunAt.HasValue)
                {
                    Console.WriteLine($"  Last Run: {FromUnixSeconds(task.LastRunAt.Value):yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (task.CompletedAt.HasValue)
                {
                    Console.WriteLine($"  Completed: {FromUnixSeconds(task.CompletedAt.Value):yyyy-MM-dd HH:mm:ss} UTC");
                }

                Console.WriteLine($"  Created: {FromUnixSeconds(task.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Updated: {FromUnixSeconds(task.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list tasks: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TasksCreateCommand(
        IHost host,
        string title,
        string? description,
        string? projectId,
        int priority,
        IReadOnlyList<string> dependencies)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var taskRepository = host.Services.GetRequiredService<TaskRepository>();
            logger.LogInformation("Creating task: {Title}", title);

            if (string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine("✗ Task title is required.");
                return 1;
            }

            if (priority < 0 || priority > 9)
            {
                Console.WriteLine("✗ Priority must be between 0 and 9.");
                return 1;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var taskId = Guid.NewGuid().ToString();
            var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            string? dependenciesJson = null;

            if (dependencies.Count > 0)
            {
                var depList = new System.Collections.Generic.List<string>();
                foreach (var dep in dependencies)
                {
                    if (!string.IsNullOrWhiteSpace(dep))
                    {
                        depList.Add(dep.Trim());
                    }
                }

                if (depList.Count > 0)
                {
                    dependenciesJson = System.Text.Json.JsonSerializer.Serialize(depList);
                }
            }

            await taskRepository.AddAsync(new ProjectTask
            {
                TaskId = taskId,
                ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim(),
                Title = title.Trim(),
                Description = normalizedDescription,
                Status = "pending",
                Priority = priority,
                DependenciesJson = dependenciesJson,
                CreatedAt = now,
                UpdatedAt = now
            }).ConfigureAwait(false);

            Console.WriteLine("✓ Task created successfully");
            Console.WriteLine($"  ID: {taskId}");
            Console.WriteLine($"  Title: {title.Trim()}");
            Console.WriteLine($"  Description: {normalizedDescription ?? string.Empty}");
            Console.WriteLine($"  Project: {(string.IsNullOrWhiteSpace(projectId) ? "(none)" : projectId.Trim())}");
            Console.WriteLine("  Status: pending");
            Console.WriteLine($"  Priority: {priority}");
            if (!string.IsNullOrEmpty(dependenciesJson))
            {
                Console.WriteLine($"  Dependencies: {dependenciesJson}");
            }
            Console.WriteLine($"  Created: {FromUnixSeconds(now):yyyy-MM-dd HH:mm:ss} UTC");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create task: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> TasksUpdateCommand(
        IHost host,
        string taskId,
        string? status,
        int? priority)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var taskRepository = host.Services.GetRequiredService<TaskRepository>();
            logger.LogInformation("Updating task: {TaskId}", taskId);

            if (string.IsNullOrWhiteSpace(taskId))
            {
                Console.WriteLine("✗ Task ID is required.");
                return 1;
            }

            if (priority.HasValue && (priority < 0 || priority > 9))
            {
                Console.WriteLine("✗ Priority must be between 0 and 9.");
                return 1;
            }

            var task = await taskRepository.GetByIdAsync(taskId, CancellationToken.None).ConfigureAwait(false);
            if (task == null)
            {
                Console.WriteLine($"✗ Task {taskId} not found.");
                return 1;
            }

            var updated = false;
            if (!string.IsNullOrWhiteSpace(status))
            {
                task.Status = status.Trim();
                if (status.Equals("complete", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    task.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                updated = true;
            }

            if (priority.HasValue)
            {
                task.Priority = priority.Value;
                updated = true;
            }

            if (!updated)
            {
                Console.WriteLine("✗ No updates specified. Use --status or --priority.");
                return 1;
            }

            task.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await taskRepository.UpdateAsync(task, CancellationToken.None).ConfigureAwait(false);

            Console.WriteLine("✓ Task updated successfully");
            Console.WriteLine($"  ID: {task.TaskId}");
            Console.WriteLine($"  Title: {task.Title}");
            Console.WriteLine($"  Status: {task.Status}");
            Console.WriteLine($"  Priority: {task.Priority}");
            Console.WriteLine($"  Updated: {FromUnixSeconds(task.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to update task: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleListCommand(IHost host, string? status)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Listing scheduled jobs");

            IReadOnlyList<ScheduledJobMetadata> jobs;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ScheduledJobStatus>(status, true, out var statusEnum))
            {
                jobs = await scheduler.GetJobsByStatusAsync(statusEnum);
            }
            else
            {
                jobs = await scheduler.GetAllJobsAsync();
            }

            Console.WriteLine("SCHEDULED JOBS:");
            if (jobs.Count == 0)
            {
                Console.WriteLine("  (No scheduled jobs found)");
                Console.WriteLine();
                Console.WriteLine("Use 'schedule cron' or 'schedule once' to create a scheduled job.");
                return 0;
            }

            foreach (var job in jobs)
            {
                Console.WriteLine($"  Job ID: {job.JobId}");
                Console.WriteLine($"  Name: {job.JobName}");
                Console.WriteLine($"  Type: {job.ScheduleType}");
                Console.WriteLine($"  Status: {job.Status}");

                if (job.ScheduledAtUtc.HasValue)
                {
                    Console.WriteLine($"  Scheduled At: {job.ScheduledAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (!string.IsNullOrEmpty(job.CronExpression))
                {
                    Console.WriteLine($"  Cron Expression: {job.CronExpression}");
                }

                if (!string.IsNullOrEmpty(job.EventType))
                {
                    Console.WriteLine($"  Event Type: {job.EventType}");
                }

                if (job.IntervalSeconds.HasValue)
                {
                    Console.WriteLine($"  Interval: {job.IntervalSeconds.Value} seconds");
                }

                Console.WriteLine($"  Execution Count: {job.ExecutionCount}");

                if (job.LastStartedAtUtc.HasValue)
                {
                    Console.WriteLine($"  Last Started: {job.LastStartedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (job.LastCompletedAtUtc.HasValue)
                {
                    Console.WriteLine($"  Last Completed: {job.LastCompletedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (job.LastExecutionDuration.HasValue)
                {
                    Console.WriteLine($"  Last Duration: {job.LastExecutionDuration.Value.TotalMilliseconds:F0} ms");
                }

                if (!string.IsNullOrEmpty(job.LastErrorMessage))
                {
                    Console.WriteLine($"  Last Error: {job.LastErrorMessage}");
                }

                Console.WriteLine($"  Created: {job.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list scheduled jobs: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleCronCommand(IHost host, string jobName, string cronExpression)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Scheduling cron job: {JobName} with expression {CronExpression}", jobName, cronExpression);

            // Create a demo job (in production, this would be a real job implementation)
            var job = new DemoScheduledJob(jobName);
            var jobId = await scheduler.ScheduleCronAsync(job, cronExpression);

            Console.WriteLine("✓ Cron job scheduled successfully");
            Console.WriteLine($"  Job ID: {jobId}");
            Console.WriteLine($"  Name: {jobName}");
            Console.WriteLine($"  Cron Expression: {cronExpression}");
            Console.WriteLine();
            Console.WriteLine("Note: This is a demo job. In production, integrate with actual task execution.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to schedule cron job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleOnceCommand(IHost host, string jobName, DateTime time)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Scheduling one-time job: {JobName} at {Time}", jobName, time);

            if (time.Kind != DateTimeKind.Utc)
            {
                time = time.ToUniversalTime();
            }

            // Create a demo job (in production, this would be a real job implementation)
            var job = new DemoScheduledJob(jobName);
            var jobId = await scheduler.ScheduleAtTimeAsync(job, time);

            Console.WriteLine("✓ One-time job scheduled successfully");
            Console.WriteLine($"  Job ID: {jobId}");
            Console.WriteLine($"  Name: {jobName}");
            Console.WriteLine($"  Scheduled Time: {time:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();
            Console.WriteLine("Note: This is a demo job. In production, integrate with actual task execution.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to schedule one-time job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleEventCommand(IHost host, string jobName, string eventType)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Scheduling event-triggered job: {JobName} for event {EventType}", jobName, eventType);

            // Create a demo job (in production, this would be a real job implementation)
            var job = new DemoScheduledJob(jobName);
            var jobId = await scheduler.ScheduleOnEventAsync(job, eventType);

            Console.WriteLine("✓ Event-triggered job scheduled successfully");
            Console.WriteLine($"  Job ID: {jobId}");
            Console.WriteLine($"  Name: {jobName}");
            Console.WriteLine($"  Event Type: {eventType}");
            Console.WriteLine();
            Console.WriteLine("Note: This job will execute when an event of the specified type is raised.");
            Console.WriteLine("      Use your application's event system to trigger execution.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to schedule event-triggered job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleCancelCommand(IHost host, string jobId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Cancelling job: {JobId}", jobId);

            var cancelled = await scheduler.CancelJobAsync(jobId);

            if (cancelled)
            {
                Console.WriteLine("✓ Job cancelled successfully");
                Console.WriteLine($"  Job ID: {jobId}");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Job {jobId} not found.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to cancel job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleInfoCommand(IHost host, string jobId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Getting job info: {JobId}", jobId);

            var metadata = await scheduler.GetJobMetadataAsync(jobId);

            if (metadata == null)
            {
                Console.WriteLine($"✗ Job {jobId} not found.");
                return 1;
            }

            Console.WriteLine("JOB DETAILS:");
            Console.WriteLine($"  Job ID: {metadata.JobId}");
            Console.WriteLine($"  Name: {metadata.JobName}");
            Console.WriteLine($"  Type: {metadata.ScheduleType}");
            Console.WriteLine($"  Status: {metadata.Status}");

            if (metadata.ScheduledAtUtc.HasValue)
            {
                Console.WriteLine($"  Scheduled At: {metadata.ScheduledAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (!string.IsNullOrEmpty(metadata.CronExpression))
            {
                Console.WriteLine($"  Cron Expression: {metadata.CronExpression}");
            }

            if (!string.IsNullOrEmpty(metadata.EventType))
            {
                Console.WriteLine($"  Event Type: {metadata.EventType}");
            }

            if (metadata.IntervalSeconds.HasValue)
            {
                Console.WriteLine($"  Interval: {metadata.IntervalSeconds.Value} seconds");
            }

            Console.WriteLine($"  Execution Count: {metadata.ExecutionCount}");

            if (metadata.LastStartedAtUtc.HasValue)
            {
                Console.WriteLine($"  Last Started: {metadata.LastStartedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (metadata.LastCompletedAtUtc.HasValue)
            {
                Console.WriteLine($"  Last Completed: {metadata.LastCompletedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (metadata.LastExecutionDuration.HasValue)
            {
                Console.WriteLine($"  Last Duration: {metadata.LastExecutionDuration.Value.TotalMilliseconds:F0} ms");
            }

            if (!string.IsNullOrEmpty(metadata.LastErrorMessage))
            {
                Console.WriteLine($"  Last Error: {metadata.LastErrorMessage}");
            }

            Console.WriteLine($"  Created: {metadata.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get job info: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SchedulePauseCommand(IHost host, string jobId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Pausing job: {JobId}", jobId);

            var paused = await scheduler.PauseJobAsync(jobId);

            if (paused)
            {
                Console.WriteLine("✓ Job paused successfully");
                Console.WriteLine($"  Job ID: {jobId}");
                Console.WriteLine();
                Console.WriteLine("The job will not execute while paused. Use 'schedule resume' to resume it.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Job {jobId} could not be paused. It may not exist or may be in a state that cannot be paused.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to pause job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleResumeCommand(IHost host, string jobId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Resuming job: {JobId}", jobId);

            var resumed = await scheduler.ResumeJobAsync(jobId);

            if (resumed)
            {
                Console.WriteLine("✓ Job resumed successfully");
                Console.WriteLine($"  Job ID: {jobId}");
                Console.WriteLine();
                Console.WriteLine("The job is now active and will execute according to its schedule.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Job {jobId} could not be resumed. It may not exist or may not be paused.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to resume job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleModifyCommand(
        IHost host, 
        string jobId, 
        DateTime? time, 
        uint? interval, 
        string? cron, 
        string? eventType)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var scheduler = host.Services.GetRequiredService<IScheduler>();
            logger.LogInformation("Modifying job: {JobId}", jobId);

            // First get the job metadata to determine its type
            var metadata = await scheduler.GetJobMetadataAsync(jobId);
            if (metadata == null)
            {
                Console.WriteLine($"✗ Job {jobId} not found.");
                return 1;
            }

            // Build the modification request
            var request = new ScheduleModificationRequest();
            int parametersProvided = 0;

            if (time.HasValue)
            {
                if (metadata.ScheduleType != ScheduleType.OneTime)
                {
                    Console.WriteLine($"✗ Job {jobId} is not a one-time job. --time is only valid for one-time jobs.");
                    return 1;
                }
                request.ScheduledAtUtc = time.Value.Kind == DateTimeKind.Utc ? time.Value : time.Value.ToUniversalTime();
                parametersProvided++;
            }

            if (interval.HasValue)
            {
                if (metadata.ScheduleType != ScheduleType.Recurring)
                {
                    Console.WriteLine($"✗ Job {jobId} is not a recurring job. --interval is only valid for recurring jobs.");
                    return 1;
                }
                request.IntervalSeconds = interval.Value;
                parametersProvided++;
            }

            if (!string.IsNullOrWhiteSpace(cron))
            {
                if (metadata.ScheduleType != ScheduleType.Cron)
                {
                    Console.WriteLine($"✗ Job {jobId} is not a cron job. --cron is only valid for cron jobs.");
                    return 1;
                }
                request.CronExpression = cron;
                parametersProvided++;
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                if (metadata.ScheduleType != ScheduleType.EventTriggered)
                {
                    Console.WriteLine($"✗ Job {jobId} is not an event-triggered job. --event-type is only valid for event-triggered jobs.");
                    return 1;
                }
                request.EventType = eventType;
                parametersProvided++;
            }

            if (parametersProvided == 0)
            {
                Console.WriteLine("✗ No modification parameters provided. Please specify:");
                Console.WriteLine("  --time for one-time jobs");
                Console.WriteLine("  --interval for recurring jobs");
                Console.WriteLine("  --cron for cron jobs");
                Console.WriteLine("  --event-type for event-triggered jobs");
                return 1;
            }

            if (parametersProvided > 1)
            {
                Console.WriteLine("✗ Multiple modification parameters provided. Please specify only one parameter for the job's schedule type.");
                return 1;
            }

            var modified = await scheduler.ModifyJobScheduleAsync(jobId, request);

            if (modified)
            {
                Console.WriteLine("✓ Job schedule modified successfully");
                Console.WriteLine($"  Job ID: {jobId}");
                Console.WriteLine($"  Job Name: {metadata.JobName}");
                Console.WriteLine($"  Schedule Type: {metadata.ScheduleType}");
                
                if (request.ScheduledAtUtc.HasValue)
                {
                    Console.WriteLine($"  New Scheduled Time: {request.ScheduledAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }
                if (request.IntervalSeconds.HasValue)
                {
                    Console.WriteLine($"  New Interval: {request.IntervalSeconds.Value} seconds");
                }
                if (!string.IsNullOrWhiteSpace(request.CronExpression))
                {
                    Console.WriteLine($"  New Cron Expression: {request.CronExpression}");
                }
                if (!string.IsNullOrWhiteSpace(request.EventType))
                {
                    Console.WriteLine($"  New Event Type: {request.EventType}");
                }

                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Job {jobId} could not be modified.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to modify job: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentListCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();
            logger.LogInformation("Listing agents");

            var agents = await agentManager.ListAgentsAsync();

            Console.WriteLine("AGENTS:");
            if (agents.Count == 0)
            {
                Console.WriteLine("  (No agents found)");
                Console.WriteLine();
                Console.WriteLine("Use 'agent create' to create a new agent.");
                return 0;
            }

            foreach (var agent in agents)
            {
                Console.WriteLine($"  Agent ID: {agent.Id}");
                Console.WriteLine($"  Name: {agent.Name}");
                Console.WriteLine($"  Purpose: {agent.Purpose}");
                Console.WriteLine($"  Enabled Skills: {(agent.EnabledSkills.Count > 0 ? string.Join(", ", agent.EnabledSkills) : "(none)")}");
                Console.WriteLine($"  Created: {agent.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list agents: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentCreateCommand(IHost host, string name, string purpose, string[] skills)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();
            logger.LogInformation("Creating agent '{Name}'", name);

            var definition = new Daiv3.Orchestration.Interfaces.AgentDefinition
            {
                Name = name,
                Purpose = purpose,
                EnabledSkills = new List<string>(skills)
            };

            var agent = await agentManager.CreateAgentAsync(definition);

            Console.WriteLine("✓ Agent created successfully");
            Console.WriteLine($"  Agent ID: {agent.Id}");
            Console.WriteLine($"  Name: {agent.Name}");
            Console.WriteLine($"  Purpose: {agent.Purpose}");
            Console.WriteLine($"  Enabled Skills: {(agent.EnabledSkills.Count > 0 ? string.Join(", ", agent.EnabledSkills) : "(none)")}");
            Console.WriteLine($"  Created: {agent.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create agent: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentGetCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();

            if (!Guid.TryParse(id, out var agentId))
            {
                Console.WriteLine($"✗ Invalid agent ID: {id}");
                return 1;
            }

            logger.LogInformation("Getting agent {AgentId}", agentId);

            var agent = await agentManager.GetAgentAsync(agentId);

            if (agent == null)
            {
                Console.WriteLine($"✗ Agent not found: {id}");
                return 1;
            }

            Console.WriteLine("AGENT DETAILS:");
            Console.WriteLine($"  Agent ID: {agent.Id}");
            Console.WriteLine($"  Name: {agent.Name}");
            Console.WriteLine($"  Purpose: {agent.Purpose}");
            Console.WriteLine($"  Enabled Skills: {(agent.EnabledSkills.Count > 0 ? string.Join(", ", agent.EnabledSkills) : "(none)")}");
            Console.WriteLine($"  Created: {agent.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            
            if (agent.Config.Count > 0)
            {
                Console.WriteLine("  Configuration:");
                foreach (var kvp in agent.Config)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get agent: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentDeleteCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();

            if (!Guid.TryParse(id, out var agentId))
            {
                Console.WriteLine($"✗ Invalid agent ID: {id}");
                return 1;
            }

            logger.LogInformation("Deleting agent {AgentId}", agentId);

            await agentManager.DeleteAgentAsync(agentId);

            Console.WriteLine($"✓ Agent {id} deleted successfully");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to delete agent: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentExecuteCommand(
        IHost host,
        string agentId,
        string goal,
        int maxIterations,
        int timeout,
        int tokenBudget)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();

            if (!Guid.TryParse(agentId, out var parsedAgentId))
            {
                Console.WriteLine($"✗ Invalid agent ID: {agentId}");
                return 1;
            }

            logger.LogInformation("Executing task with agent {AgentId}", parsedAgentId);

            Console.WriteLine("AGENT EXECUTION");
            Console.WriteLine("===============");
            Console.WriteLine($"Agent ID: {agentId}");
            Console.WriteLine($"Goal: {goal}");
            Console.WriteLine($"Max Iterations: {maxIterations}");
            Console.WriteLine($"Timeout: {timeout}s");
            Console.WriteLine($"Token Budget: {tokenBudget}");
            Console.WriteLine();
            Console.WriteLine("Executing...");
            Console.WriteLine();

            var request = new Daiv3.Orchestration.Interfaces.AgentExecutionRequest
            {
                AgentId = parsedAgentId,
                TaskGoal = goal,
                Options = new Daiv3.Orchestration.Interfaces.AgentExecutionOptions
                {
                    MaxIterations = maxIterations,
                    TimeoutSeconds = timeout,
                    TokenBudget = tokenBudget
                }
            };

            var result = await agentManager.ExecuteTaskAsync(request);

            Console.WriteLine("EXECUTION RESULT:");
            Console.WriteLine($"  Execution ID: {result.ExecutionId}");
            Console.WriteLine($"  Status: {(result.Success ? "✓ Success" : "✗ Failed")}");
            Console.WriteLine($"  Termination Reason: {result.TerminationReason}");
            Console.WriteLine($"  Iterations Executed: {result.IterationsExecuted}");
            Console.WriteLine($"  Tokens Consumed: {result.TokensConsumed}");
            Console.WriteLine($"  Duration: {(result.CompletedAt - result.StartedAt)?.TotalSeconds:F2}s");
            Console.WriteLine();

            if (result.Steps.Count > 0)
            {
                Console.WriteLine($"EXECUTION STEPS ({result.Steps.Count}):");
                foreach (var step in result.Steps)
                {
                    Console.WriteLine($"  Step {step.StepNumber}: {step.StepType}");
                    Console.WriteLine($"    Description: {step.Description}");
                    Console.WriteLine($"    Status: {(step.Success ? "✓" : "✗")}");
                    Console.WriteLine($"    Tokens: {step.TokensConsumed}");
                    if (!string.IsNullOrEmpty(step.Output))
                    {
                        Console.WriteLine($"    Output: {step.Output}");
                    }
                    if (!string.IsNullOrEmpty(step.ErrorMessage))
                    {
                        Console.WriteLine($"    Error: {step.ErrorMessage}");
                    }
                }
                Console.WriteLine();
            }

            if (!string.IsNullOrEmpty(result.Output))
            {
                Console.WriteLine("FINAL OUTPUT:");
                Console.WriteLine(result.Output);
                Console.WriteLine();
            }

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine("ERROR:");
                Console.WriteLine(result.ErrorMessage);
                Console.WriteLine();
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to execute agent task: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentLoadCommand(
        IHost host,
        string configPath,
        bool recursive,
        bool validateOnly)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();
            var loader = new Daiv3.Orchestration.Configuration.AgentConfigFileLoader(
                host.Services.GetRequiredService<ILogger<Daiv3.Orchestration.Configuration.AgentConfigFileLoader>>(),
                host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.ISkillRegistry>());

            Console.WriteLine("LOADING AGENT CONFIGURATION(S)");
            Console.WriteLine("=============================");
            Console.WriteLine();

            // Load configuration file(s)
            var batch = await loader.LoadAgentBatchAsync(configPath, recursive);
            
            if (batch.Agents.Count == 0)
            {
                Console.WriteLine("✗ No agent configurations found at the specified path.");
                return 1;
            }

            Console.WriteLine($"Found {batch.Agents.Count} agent configuration(s)");
            Console.WriteLine();

            // Validate all configurations
            var validationResults = new List<(AgentConfigurationFile config, AgentConfigurationValidationResult result)>();
            var hasErrors = false;

            foreach (var config in batch.Agents)
            {
                var validationResult = loader.ValidateConfiguration(config);
                validationResults.Add((config, validationResult));

                if (!validationResult.IsValid)
                {
                    hasErrors = true;
                    Console.WriteLine($"✗ {config.Name}:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.WriteLine($"    Error: {error}");
                    }
                }

                if (validationResult.Warnings.Count > 0)
                {
                    Console.WriteLine($"⚠ {config.Name}:");
                    foreach (var warning in validationResult.Warnings)
                    {
                        Console.WriteLine($"    Warning: {warning}");
                    }
                }
            }

            if (hasErrors)
            {
                Console.WriteLine();
                Console.WriteLine("✗ Configuration validation failed. Fix errors and try again.");
                return 1;
            }

            if (validateOnly)
            {
                Console.WriteLine();
                Console.WriteLine("✓ All configurations are valid.");
                return 0;
            }

            // Create agents
            var createdAgents = new List<Daiv3.Orchestration.Interfaces.Agent>();
            var failedAgents = new List<(string name, string error)>();

            Console.WriteLine();
            Console.WriteLine("Creating agents...");
            Console.WriteLine();

            foreach (var (config, _) in validationResults)
            {
                try
                {
                    var definition = loader.ToAgentDefinition(config);
                    var agent = await agentManager.CreateAgentAsync(definition);
                    createdAgents.Add(agent);
                    Console.WriteLine($"✓ Created agent: {config.Name} (ID: {agent.Id})");
                }
                catch (Exception ex)
                {
                    failedAgents.Add((config.Name, ex.Message));
                    Console.WriteLine($"✗ Failed to create agent '{config.Name}': {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("SUMMARY:");
            Console.WriteLine($"  Successfully created: {createdAgents.Count}");
            Console.WriteLine($"  Failed to create: {failedAgents.Count}");

            if (failedAgents.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Failed agents:");
                foreach (var (name, error) in failedAgents)
                {
                    Console.WriteLine($"    - {name}: {error}");
                }
                return 1;
            }

            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to load agent configuration: {ex.Message}");
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

    private static async Task<int> MultimodalTextCommand(IHost host, string text)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Testing CLIP text encoding with text: {Text}", text);

            Console.WriteLine("CLIP MULTIMODAL TEXT ENCODING TEST");
            Console.WriteLine("==================================");
            Console.WriteLine($"Input text: {text}");
            Console.WriteLine();
            
            // TODO: Integrate with actual CLIP text encoder when available
            Console.WriteLine("Status: CLIP text encoder integration pending");
            Console.WriteLine();
            Console.WriteLine("Expected capabilities:");
            Console.WriteLine("  • Text encoding into 512-dimensional vectors");
            Console.WriteLine("  • Normalized L2 distance for similarity comparison");
            Console.WriteLine("  • Image-text similarity matching for vision tasks");
            Console.WriteLine();
            Console.WriteLine("Model Information:");
            Console.WriteLine("  • Model: xenova/clip-vit-base-patch32");
            Console.WriteLine("  • Text Encoder Output Dims: 512");
            Console.WriteLine("  • Vision Encoder Output Dims: 512");
            Console.WriteLine("  • Hardware: NPU/GPU (full precision), CPU (quantized)");
            Console.WriteLine();
            
            logger.LogInformation("CLIP text encoding test completed (integration pending)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to test CLIP text encoding: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "CLIP text encoding test failed");
            return 1;
        }
    }

    private static async Task<int> OcrTestCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Testing OCR capabilities");

            Console.WriteLine("OCR (OPTICAL CHARACTER RECOGNITION) TEST");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            // TODO: Integrate with actual TrOCR encoder-decoder when available
            Console.WriteLine("Status: TrOCR integration pending");
            Console.WriteLine();
            Console.WriteLine("Expected capabilities:");
            Console.WriteLine("  • Document and handwriting text recognition");
            Console.WriteLine("  • Support for multiple languages");
            Console.WriteLine("  • Encoder-decoder architecture for accurate transcription");
            Console.WriteLine();
            Console.WriteLine("Model Information:");
            Console.WriteLine("  • Base Model: microsoft/trocr-base-printed");
            Console.WriteLine("  • Architecture: Vision Encoder (ViT) + Text Decoder (LSTM)");
            Console.WriteLine("  • Input: Normalized image patches");
            Console.WriteLine("  • Output: Text tokens (character sequences)");
            Console.WriteLine();
            Console.WriteLine("Hardware Variants:");
            Console.WriteLine("  • NPU/GPU: FP16 precision for accelerated inference");
            Console.WriteLine("  • CPU: Quantized (int8) for efficient CPU execution");
            Console.WriteLine();
            Console.WriteLine("Usage Example:");
            Console.WriteLine("  ocr test");
            Console.WriteLine("    Demonstrates OCR capabilities on sample images");
            Console.WriteLine();
            
            logger.LogInformation("OCR test completed (integration pending)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to test OCR: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "OCR test failed");
            return 1;
        }
    }

    private static async Task EnsureEmbeddingModelAsync()
    {
        // Tier 1: all-MiniLM-L6-v2 (384 dimensions) - Topic/Summary level
        const string Tier1ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/all-MiniLM-L6-v2/model.onnx";
        
        // Tier 2: nomic-embed-text-v1.5 (768 dimensions) - Chunk level
        const string Tier2ModelDownloadUrl = "https://stdaiv3.blob.core.windows.net/models/embedding/onnx/nomic-embed-text-v1.5/model.onnx";
        
        try
        {
            var tier1Path = GetTier1ModelPath();
            var tier2Path = GetTier2ModelPath();
            
            // Check if both models already exist
            var tier1Exists = File.Exists(tier1Path);
            var tier2Exists = File.Exists(tier2Path);
            
            if (tier1Exists && tier2Exists)
            {
                return; // Both models already exist, no need to download
            }

            // Create a temporary host just for bootstrapping
            using var tempHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                    services.AddEmbeddingServices();
                })
                .Build();

            var downloadService = tempHost.Services.GetRequiredService<IEmbeddingModelDownloadService>();
            var logger = tempHost.Services.GetRequiredService<ILogger<Program>>();

            // Download Tier 1 model if needed
            if (!tier1Exists)
            {
                Console.WriteLine("Tier 1 embedding model (all-MiniLM-L6-v2) not found.");
                Console.WriteLine("Downloading from Azure Blob Storage...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var success = await downloadService.EnsureModelExistsAsync(
                    tier1Path,
                    Tier1ModelDownloadUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"Tier 1 Progress: {percent:F1}% ({progress.BytesDownloaded / 1024.0 / 1024.0:F2} MB / {(progress.TotalBytes ?? 0) / 1024.0 / 1024.0:F2} MB)");
                                lastPercent = percent;
                            }
                        }
                        else if (!string.IsNullOrEmpty(progress.Status))
                        {
                            Console.WriteLine($"Tier 1: {progress.Status}");
                        }
                    }));

                if (success)
                {
                    Console.WriteLine("✓ Tier 1 model download completed successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ Tier 1 model download failed. Some features may not be available.");
                    Console.WriteLine();
                }
            }

            // Download Tier 2 model if needed
            if (!tier2Exists)
            {
                Console.WriteLine("Tier 2 embedding model (nomic-embed-text-v1.5) not found.");
                Console.WriteLine("Downloading from Azure Blob Storage...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var success = await downloadService.EnsureModelExistsAsync(
                    tier2Path,
                    Tier2ModelDownloadUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"Tier 2 Progress: {percent:F1}% ({progress.BytesDownloaded / 1024.0 / 1024.0:F2} MB / {(progress.TotalBytes ?? 0) / 1024.0 / 1024.0:F2} MB)");
                                lastPercent = percent;
                            }
                        }
                        else if (!string.IsNullOrEmpty(progress.Status))
                        {
                            Console.WriteLine($"Tier 2: {progress.Status}");
                        }
                    }));

                if (success)
                {
                    Console.WriteLine("✓ Tier 2 model download completed successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ Tier 2 model download failed. Some features may not be available.");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error bootstrapping embedding models: {ex.Message}");
            Console.WriteLine("Some features may not be available.");
            Console.WriteLine();
        }
    }

    private static string GetTier1ModelPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "all-MiniLM-L6-v2", "model.onnx");
    }

    private static string GetTier2ModelPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Daiv3", "models", "embeddings", "nomic-embed-text-v1.5", "model.onnx");
    }

    private static async Task EnsureOcrModelsAsync()
    {
        // TrOCR: Microsoft Transformer-based OCR for printed and handwritten text
        const string OcrEncoderFp16Url = "https://stdaiv3.blob.core.windows.net/models/ocr/trocr-base-printed/fp16/encoder_model.onnx";
        const string OcrDecoderFp16Url = "https://stdaiv3.blob.core.windows.net/models/ocr/trocr-base-printed/fp16/decoder_model.onnx";
        const string OcrEncoderQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/ocr/trocr-base-printed/quantized/encoder_model_int8.onnx";
        const string OcrDecoderQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/ocr/trocr-base-printed/quantized/decoder_model_int8.onnx";

        try
        {
            var encoderFp16Path = GetOcrEncoderModelPath(useFp16: true);
            var decoderFp16Path = GetOcrDecoderModelPath(useFp16: true);
            var encoderQuantizedPath = GetOcrEncoderModelPath(useFp16: false);
            var decoderQuantizedPath = GetOcrDecoderModelPath(useFp16: false);

            // Check if models already exist
            var fp16Exists = File.Exists(encoderFp16Path) && File.Exists(decoderFp16Path);
            var quantizedExists = File.Exists(encoderQuantizedPath) && File.Exists(decoderQuantizedPath);

            if (fp16Exists && quantizedExists)
            {
                return; // Both variants exist
            }

            using var tempHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                    services.AddEmbeddingServices();
                })
                .Build();

            var downloadService = tempHost.Services.GetRequiredService<IEmbeddingModelDownloadService>();

            // Download FP16 variants for NPU/GPU if needed
            if (!fp16Exists)
            {
                Console.WriteLine("OCR FP16 models (encoder/decoder) not found.");
                Console.WriteLine("Downloading for NPU/GPU acceleration...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var encoderSuccess = await downloadService.EnsureModelExistsAsync(
                    encoderFp16Path,
                    OcrEncoderFp16Url,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"OCR Encoder (FP16): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                lastPercent = -1.0;
                var decoderSuccess = await downloadService.EnsureModelExistsAsync(
                    decoderFp16Path,
                    OcrDecoderFp16Url,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"OCR Decoder (FP16): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                if (encoderSuccess && decoderSuccess)
                {
                    Console.WriteLine("✓ OCR FP16 models downloaded successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ OCR FP16 model download failed.");
                    Console.WriteLine();
                }
            }

            // Download quantized variants for CPU if needed
            if (!quantizedExists)
            {
                Console.WriteLine("OCR quantized models (encoder/decoder) not found.");
                Console.WriteLine("Downloading for CPU execution...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var encoderSuccess = await downloadService.EnsureModelExistsAsync(
                    encoderQuantizedPath,
                    OcrEncoderQuantizedUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"OCR Encoder (quantized): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                lastPercent = -1.0;
                var decoderSuccess = await downloadService.EnsureModelExistsAsync(
                    decoderQuantizedPath,
                    OcrDecoderQuantizedUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"OCR Decoder (quantized): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                if (encoderSuccess && decoderSuccess)
                {
                    Console.WriteLine("✓ OCR quantized models downloaded successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ OCR quantized model download failed.");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error bootstrapping OCR models: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static async Task EnsureMultimodalModelsAsync()
    {
        // CLIP: Contrastive Language-Image Pre-training for multimodal embeddings
        const string MultimodalTextFullUrl = "https://stdaiv3.blob.core.windows.net/models/multimodal/clip-vit-base-patch32/full-precision/model.onnx";
        const string MultimodalVisionFullUrl = "https://stdaiv3.blob.core.windows.net/models/multimodal/clip-vit-base-patch32/full-precision/vision_model.onnx";
        const string MultimodalTextQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/multimodal/clip-vit-base-patch32/quantized/model_uint8.onnx";
        const string MultimodalVisionQuantizedUrl = "https://stdaiv3.blob.core.windows.net/models/multimodal/clip-vit-base-patch32/quantized/vision_model_int8.onnx";

        try
        {
            var textFullPath = GetMultimodalTextModelPath(useFullPrecision: true);
            var visionFullPath = GetMultimodalVisionModelPath(useFullPrecision: true);
            var textQuantizedPath = GetMultimodalTextModelPath(useFullPrecision: false);
            var visionQuantizedPath = GetMultimodalVisionModelPath(useFullPrecision: false);

            // Check if models already exist
            var fullExists = File.Exists(textFullPath) && File.Exists(visionFullPath);
            var quantizedExists = File.Exists(textQuantizedPath) && File.Exists(visionQuantizedPath);

            if (fullExists && quantizedExists)
            {
                return; // Both variants exist
            }

            using var tempHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                    services.AddEmbeddingServices();
                })
                .Build();

            var downloadService = tempHost.Services.GetRequiredService<IEmbeddingModelDownloadService>();

            // Download full-precision variants for NPU/GPU if needed
            if (!fullExists)
            {
                Console.WriteLine("CLIP full-precision models (text/vision encoders) not found.");
                Console.WriteLine("Downloading for NPU/GPU acceleration...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var textSuccess = await downloadService.EnsureModelExistsAsync(
                    textFullPath,
                    MultimodalTextFullUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"CLIP Text Encoder (full): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                lastPercent = -1.0;
                var visionSuccess = await downloadService.EnsureModelExistsAsync(
                    visionFullPath,
                    MultimodalVisionFullUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"CLIP Vision Encoder (full): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                if (textSuccess && visionSuccess)
                {
                    Console.WriteLine("✓ CLIP full-precision models downloaded successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ CLIP full-precision model download failed.");
                    Console.WriteLine();
                }
            }

            // Download quantized variants for CPU if needed
            if (!quantizedExists)
            {
                Console.WriteLine("CLIP quantized models (text/vision encoders) not found.");
                Console.WriteLine("Downloading for CPU execution...");
                Console.WriteLine();

                var lastPercent = -1.0;
                var textSuccess = await downloadService.EnsureModelExistsAsync(
                    textQuantizedPath,
                    MultimodalTextQuantizedUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"CLIP Text Encoder (quantized): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                lastPercent = -1.0;
                var visionSuccess = await downloadService.EnsureModelExistsAsync(
                    visionQuantizedPath,
                    MultimodalVisionQuantizedUrl,
                    new Progress<DownloadProgress>(progress =>
                    {
                        if (progress.PercentComplete.HasValue)
                        {
                            var percent = progress.PercentComplete.Value;
                            if (Math.Abs(percent - lastPercent) >= 5.0)
                            {
                                Console.WriteLine($"CLIP Vision Encoder (quantized): {percent:F1}%");
                                lastPercent = percent;
                            }
                        }
                    }));

                if (textSuccess && visionSuccess)
                {
                    Console.WriteLine("✓ CLIP quantized models downloaded successfully");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("✗ CLIP quantized model download failed.");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error bootstrapping multimodal models: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static string GetOcrEncoderModelPath(bool useFp16)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var variant = useFp16 ? "fp16" : "quantized";
        return Path.Combine(baseDir, "Daiv3", "models", "ocr", "trocr-base-printed", variant, "encoder_model.onnx");
    }

    private static string GetOcrDecoderModelPath(bool useFp16)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var variant = useFp16 ? "fp16" : "quantized";
        return Path.Combine(baseDir, "Daiv3", "models", "ocr", "trocr-base-printed", variant, "decoder_model.onnx");
    }

    private static string GetMultimodalTextModelPath(bool useFullPrecision)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var variant = useFullPrecision ? "full-precision" : "quantized";
        return Path.Combine(baseDir, "Daiv3", "models", "multimodal", "clip-vit-base-patch32", variant, "model.onnx");
    }

    private static string GetMultimodalVisionModelPath(bool useFullPrecision)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var variant = useFullPrecision ? "full-precision" : "quantized";
        return Path.Combine(baseDir, "Daiv3", "models", "multimodal", "clip-vit-base-patch32", variant, "vision_model.onnx");
    }

    private static async Task EnsureAllModelsAsync()
    {
        try
        {
            // Create a temporary host just for bootstrapping models
            using var tempHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddFileLogging("cli", LogLevel.Debug);
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                    services.AddEmbeddingServices();
                })
                .Build();

            var bootstrapService = tempHost.Services.GetRequiredService<EmbeddingModelBootstrapService>();
            var logger = tempHost.Services.GetRequiredService<ILogger<Program>>();
            
            // Bootstrap all models (embeddings Tier 1/2, OCR, multimodal) with progress reporting
            var success = await bootstrapService.EnsureModelsAsync(progress =>
            {
                if (progress.PercentComplete.HasValue)
                {
                    var percent = progress.PercentComplete.Value;
                    var mb = progress.BytesDownloaded / 1024.0 / 1024.0;
                    var totalMb = (progress.TotalBytes ?? 0) / 1024.0 / 1024.0;
                    var message = $"Download: {percent:F1}% ({mb:F2} MB / {totalMb:F2} MB)";
                    Console.WriteLine(message);
                    logger.LogInformation(message);
                }
                else if (!string.IsNullOrEmpty(progress.Status))
                {
                    var message = $"• {progress.Status}";
                    Console.WriteLine(message);
                    logger.LogInformation(message);
                }
            });

            if (!success)
            {
                var message = "✗ Some models failed to download. Some features may not be available.";
                Console.WriteLine(message);
                logger.LogWarning(message);
                Console.WriteLine();
            }
            else
            {
                logger.LogInformation("All models bootstrapped successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error bootstrapping models: {ex.Message}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demo scheduled job for testing scheduler functionality.
    /// In production, replace this with actual business logic jobs.
    /// </summary>
    private class DemoScheduledJob : IScheduledJob
    {
        public string Name { get; }
        public IDictionary<string, object>? Metadata { get; set; }

        public DemoScheduledJob(string name)
        {
            Name = name;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            // Simulate some work
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Executing scheduled job: {Name}");
            await Task.Delay(100, cancellationToken);
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Completed scheduled job: {Name}");
        }
    }
}
