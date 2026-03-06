using System.CommandLine;
using System.Text.Json;
using Daiv3.Core.Settings;
using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Infrastructure.Shared.Logging;
using Daiv3.Knowledge;
using Daiv3.Knowledge.Embedding;
using Daiv3.ModelExecution;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Daiv3.Scheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            using var host = CreateHost();
            var exitCode = await DatabaseInitCommand(host);
            Environment.Exit(exitCode);
        });

        var dbStatusCommand = new Command("status", "Show database status");
        dbStatusCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await DatabaseStatusCommand(host);
            Environment.Exit(exitCode);
        });

        dbCommand.AddCommand(dbInitCommand);
        dbCommand.AddCommand(dbStatusCommand);
        rootCommand.AddCommand(dbCommand);

        // Dashboard command (CT-REQ-003, CT-REQ-006)
        var dashboardCommand = new Command("dashboard", "Show system dashboard and status");
        dashboardCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await Task.FromResult(DashboardCommand(host));
            Environment.Exit(exitCode);
        });

        // dashboard agents subcommand (CT-REQ-006: Agent activity)
        var dashboardAgentsCommand = new Command("agents", "Show agent definitions and active execution summary");
        dashboardAgentsCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await DashboardAgentsCommand(host);
            Environment.Exit(exitCode);
        });
        dashboardCommand.AddCommand(dashboardAgentsCommand);

        // dashboard resources subcommand (CT-REQ-006: System resource metrics)
        var dashboardResourcesCommand = new Command("resources", "Show system resource metrics (CPU, memory, disk)");
        dashboardResourcesCommand.SetHandler(() =>
        {
            var exitCode = DashboardResourcesCommand();
            Environment.Exit(exitCode);
        });
        dashboardCommand.AddCommand(dashboardResourcesCommand);

        // dashboard admin subcommand (CT-REQ-010: System admin dashboard)
        var dashboardAdminCommand = new Command("admin", "Show system admin dashboard metrics");
        var adminJsonOption = new Option<bool>(
            aliases: new[] { "--json" },
            description: "Output metrics as JSON",
            getDefaultValue: () => false);
        var adminWatchOption = new Option<bool>(
            aliases: new[] { "--watch" },
            description: "Continuously refresh metrics every 3 seconds",
            getDefaultValue: () => false);
        var adminHistoryOption = new Option<bool>(
            aliases: new[] { "--history" },
            description: "Show 24-hour trend summary from persisted snapshots",
            getDefaultValue: () => false);
        dashboardAdminCommand.AddOption(adminJsonOption);
        dashboardAdminCommand.AddOption(adminWatchOption);
        dashboardAdminCommand.AddOption(adminHistoryOption);
        dashboardAdminCommand.SetHandler(async (bool json, bool watch, bool history) =>
        {
            using var host = CreateHost();
            var exitCode = await DashboardAdminCommand(host, json, watch, history);
            Environment.Exit(exitCode);
        }, adminJsonOption, adminWatchOption, adminHistoryOption);
        dashboardCommand.AddCommand(dashboardAdminCommand);

        rootCommand.AddCommand(dashboardCommand);

        // Chat command
        var chatCommand = new Command("chat", "Interactive chat interface");
        var messageOption = new Option<string?>(
            aliases: new[] { "--message", "-m" },
            description: "Send a single message and exit");
        chatCommand.AddOption(messageOption);
        chatCommand.SetHandler(async (string? message) =>
        {
            using var host = CreateHost();
            var exitCode = await Task.FromResult(ChatCommand(host, message));
            Environment.Exit(exitCode);
        }, messageOption);
        rootCommand.AddCommand(chatCommand);

        // Projects command
        var projectsCommand = new Command("projects", "Project management commands");

        var projectsListCommand = new Command("list", "List all projects");
        projectsListCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ProjectsListCommand(host);
            Environment.Exit(exitCode);
        });

        var projectsCreateCommand = new Command("create", "Create a new project");
        var projectNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Project name")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await ProjectsCreateCommand(host, name, desc, rootPaths, instructions, preferredModel, fallbackModel);
            Environment.Exit(exitCode);
        }, projectNameOption, projectDescOption, projectRootPathOption, projectInstructionsOption, preferredModelOption, fallbackModelOption);

        // CT-REQ-011: Project dashboard commands
        var projectsTreeCommand = new Command("tree", "Display projects in hierarchical tree view");
        projectsTreeCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ProjectsTreeCommand(host);
            Environment.Exit(exitCode);
        });

        var projectsByStatusCommand = new Command("by-status", "Display projects grouped by status");
        projectsByStatusCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ProjectsByStatusCommand(host);
            Environment.Exit(exitCode);
        });

        var projectsByAgentCommand = new Command("by-agent", "Display projects grouped by assigned agent");
        projectsByAgentCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ProjectsByAgentCommand(host);
            Environment.Exit(exitCode);
        });

        var projectsAnalyticsCommand = new Command("analytics", "Display project dashboard metrics and analytics");
        projectsAnalyticsCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ProjectsAnalyticsCommand(host);
            Environment.Exit(exitCode);
        });

        projectsCommand.AddCommand(projectsListCommand);
        projectsCommand.AddCommand(projectsCreateCommand);
        projectsCommand.AddCommand(projectsTreeCommand);
        projectsCommand.AddCommand(projectsByStatusCommand);
        projectsCommand.AddCommand(projectsByAgentCommand);
        projectsCommand.AddCommand(projectsAnalyticsCommand);
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
            using var host = CreateHost();
            var exitCode = await TasksListCommand(host, projectId, status);
            Environment.Exit(exitCode);
        }, taskProjectIdOption, taskStatusOption);

        var tasksCreateCommand = new Command("create", "Create a new task");
        var taskTitleOption = new Option<string>(
            aliases: new[] { "--title", "-t" },
            description: "Task title")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await TasksCreateCommand(host, title, description, projectId, priority, dependencies);
            Environment.Exit(exitCode);
        }, taskTitleOption, taskDescriptionOption, taskProjectOption, taskPriorityOption, taskDependenciesOption);

        var tasksUpdateCommand = new Command("update", "Update a task");
        var taskIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Task ID")
        { IsRequired = true };
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
            using var host = CreateHost();
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
            using var host = CreateHost();
            var exitCode = await ScheduleListCommand(host, status);
            Environment.Exit(exitCode);
        }, scheduleStatusFilter);

        var scheduleCronCommand = new Command("cron", "Schedule a job using cron expression");
        var cronJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name")
        { IsRequired = true };
        var cronExpressionOption = new Option<string>(
            aliases: new[] { "--expression", "-e" },
            description: "Cron expression (e.g., '0 0 * * *' for daily at midnight)")
        { IsRequired = true };
        scheduleCronCommand.AddOption(cronJobNameOption);
        scheduleCronCommand.AddOption(cronExpressionOption);
        scheduleCronCommand.SetHandler(async (string jobName, string cronExpression) =>
        {
            using var host = CreateHost();
            var exitCode = await ScheduleCronCommand(host, jobName, cronExpression);
            Environment.Exit(exitCode);
        }, cronJobNameOption, cronExpressionOption);

        var scheduleOnceCommand = new Command("once", "Schedule a one-time job");
        var onceJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name")
        { IsRequired = true };
        var onceTimeOption = new Option<DateTime>(
            aliases: new[] { "--time", "-t" },
            description: "UTC time to run (ISO 8601 format, e.g., '2026-03-01T10:30:00Z')")
        { IsRequired = true };
        scheduleOnceCommand.AddOption(onceJobNameOption);
        scheduleOnceCommand.AddOption(onceTimeOption);
        scheduleOnceCommand.SetHandler(async (string jobName, DateTime time) =>
        {
            using var host = CreateHost();
            var exitCode = await ScheduleOnceCommand(host, jobName, time);
            Environment.Exit(exitCode);
        }, onceJobNameOption, onceTimeOption);

        var scheduleEventCommand = new Command("on-event", "Schedule a job to run on event");
        var eventJobNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Job name")
        { IsRequired = true };
        var eventTypeOption = new Option<string>(
            aliases: new[] { "--event-type", "-e" },
            description: "Event type to listen for")
        { IsRequired = true };
        scheduleEventCommand.AddOption(eventJobNameOption);
        scheduleEventCommand.AddOption(eventTypeOption);
        scheduleEventCommand.SetHandler(async (string jobName, string eventType) =>
        {
            using var host = CreateHost();
            var exitCode = await ScheduleEventCommand(host, jobName, eventType);
            Environment.Exit(exitCode);
        }, eventJobNameOption, eventTypeOption);

        var scheduleCancelCommand = new Command("cancel", "Cancel a scheduled job");
        var cancelJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID")
        { IsRequired = true };
        scheduleCancelCommand.AddOption(cancelJobIdOption);
        scheduleCancelCommand.SetHandler(async (string jobId) =>
        {
            using var host = CreateHost();
            var exitCode = await ScheduleCancelCommand(host, jobId);
            Environment.Exit(exitCode);
        }, cancelJobIdOption);

        var scheduleInfoCommand = new Command("info", "Show detailed information about a scheduled job");
        var infoJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID")
        { IsRequired = true };
        scheduleInfoCommand.AddOption(infoJobIdOption);
        scheduleInfoCommand.SetHandler(async (string jobId) =>
        {
            using var host = CreateHost();
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
            description: "Job ID")
        { IsRequired = true };
        schedulePauseCommand.AddOption(pauseJobIdOption);
        schedulePauseCommand.SetHandler(async (string jobId) =>
        {
            using var host = CreateHost();
            var exitCode = await SchedulePauseCommand(host, jobId);
            Environment.Exit(exitCode);
        }, pauseJobIdOption);

        var scheduleResumeCommand = new Command("resume", "Resume a paused job");
        var resumeJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID")
        { IsRequired = true };
        scheduleResumeCommand.AddOption(resumeJobIdOption);
        scheduleResumeCommand.SetHandler(async (string jobId) =>
        {
            using var host = CreateHost();
            var exitCode = await ScheduleResumeCommand(host, jobId);
            Environment.Exit(exitCode);
        }, resumeJobIdOption);

        var scheduleModifyCommand = new Command("modify", "Modify a scheduled job");
        var modifyJobIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Job ID")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await ScheduleModifyCommand(host, jobId, time, interval, cron, eventType);
            Environment.Exit(exitCode);
        }, modifyJobIdOption, modifyTimeOption, modifyIntervalOption, modifyCronOption, modifyEventTypeOption);

        scheduleCommand.AddCommand(schedulePauseCommand);
        scheduleCommand.AddCommand(scheduleResumeCommand);
        scheduleCommand.AddCommand(scheduleModifyCommand);
        rootCommand.AddCommand(scheduleCommand);

        // Settings commands (CT-REQ-001: Local settings storage)
        var settingsCommand = new Command("settings", "Application settings management commands");

        var settingsInitCommand = new Command("init", "Initialize settings with default values");
        settingsInitCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsInitCommand(host);
            Environment.Exit(exitCode);
        });

        var settingsListCommand = new Command("list", "List all settings or settings in a category");
        var settingsCategoryOption = new Option<string?>(
            aliases: new[] { "--category", "-c" },
            description: "Filter by category (general, paths, models, providers, hardware, ui, knowledge)");
        var settingsShowSensitiveOption = new Option<bool>(
            aliases: new[] { "--show-sensitive" },
            description: "Show sensitive values (default: false)",
            getDefaultValue: () => false);
        settingsListCommand.AddOption(settingsCategoryOption);
        settingsListCommand.AddOption(settingsShowSensitiveOption);
        settingsListCommand.SetHandler(async (string? category, bool showSensitive) =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsListCommand(host, category, showSensitive);
            Environment.Exit(exitCode);
        }, settingsCategoryOption, settingsShowSensitiveOption);

        var settingsGetCommand = new Command("get", "Get a specific setting value");
        var settingsKeyOption = new Option<string>(
            aliases: new[] { "--key", "-k" },
            description: "Setting key")
        { IsRequired = true };
        settingsGetCommand.AddOption(settingsKeyOption);
        settingsGetCommand.SetHandler(async (string key) =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsGetCommand(host, key);
            Environment.Exit(exitCode);
        }, settingsKeyOption);

        var settingsSetCommand = new Command("set", "Set a setting value");
        var settingsSetKeyOption = new Option<string>(
            aliases: new[] { "--key", "-k" },
            description: "Setting key")
        { IsRequired = true };
        var settingsSetValueOption = new Option<string>(
            aliases: new[] { "--value", "-v" },
            description: "Setting value")
        { IsRequired = true };
        var settingsSetReasonOption = new Option<string>(
            aliases: new[] { "--reason", "-r" },
            description: "Reason for change (optional)",
            getDefaultValue: () => "user_cli");
        settingsSetCommand.AddOption(settingsSetKeyOption);
        settingsSetCommand.AddOption(settingsSetValueOption);
        settingsSetCommand.AddOption(settingsSetReasonOption);
        settingsSetCommand.SetHandler(async (string key, string value, string reason) =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsSetCommand(host, key, value, reason);
            Environment.Exit(exitCode);
        }, settingsSetKeyOption, settingsSetValueOption, settingsSetReasonOption);

        var settingsResetCommand = new Command("reset", "Reset all settings to defaults");
        var settingsConfirmOption = new Option<bool>(
            aliases: new[] { "--confirm", "-y" },
            description: "Confirm reset without prompting",
            getDefaultValue: () => false);
        settingsResetCommand.AddOption(settingsConfirmOption);
        settingsResetCommand.SetHandler(async (bool confirm) =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsResetCommand(host, confirm);
            Environment.Exit(exitCode);
        }, settingsConfirmOption);

        var settingsHistoryCommand = new Command("history", "Show change history for a setting");
        var settingsHistoryKeyOption = new Option<string>(
            aliases: new[] { "--key", "-k" },
            description: "Setting key")
        { IsRequired = true };
        settingsHistoryCommand.AddOption(settingsHistoryKeyOption);
        settingsHistoryCommand.SetHandler(async (string key) =>
        {
            using var host = CreateHost();
            var exitCode = await SettingsHistoryCommand(host, key);
            Environment.Exit(exitCode);
        }, settingsHistoryKeyOption);

        settingsCommand.AddCommand(settingsInitCommand);
        settingsCommand.AddCommand(settingsListCommand);
        settingsCommand.AddCommand(settingsGetCommand);
        settingsCommand.AddCommand(settingsSetCommand);
        settingsCommand.AddCommand(settingsResetCommand);
        settingsCommand.AddCommand(settingsHistoryCommand);
        rootCommand.AddCommand(settingsCommand);

        // Agent commands
        var agentCommand = new Command("agent", "Agent management and execution commands");

        var agentListCommand = new Command("list", "List all agents");
        agentListCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await AgentListCommand(host);
            Environment.Exit(exitCode);
        });

        var agentCreateCommand = new Command("create", "Create a new agent");
        var agentNameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Agent name")
        { IsRequired = true };
        var agentPurposeOption = new Option<string>(
            aliases: new[] { "--purpose", "-p" },
            description: "Agent purpose or description")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await AgentCreateCommand(host, name, purpose, skills);
            Environment.Exit(exitCode);
        }, agentNameOption, agentPurposeOption, agentSkillsOption);

        var agentCreateForTaskCommand = new Command("create-for-task", "Create or reuse a dynamic agent for a task type");
        var agentTaskTypeOption = new Option<string>(
            aliases: new[] { "--task-type", "-t" },
            description: "Task type to map to a dynamic agent")
        { IsRequired = true };
        var agentTaskNameOption = new Option<string?>(
            aliases: new[] { "--name", "-n" },
            description: "Optional explicit agent name override");
        var agentTaskPurposeOption = new Option<string?>(
            aliases: new[] { "--purpose", "-p" },
            description: "Optional explicit purpose override");
        var agentTaskSkillsOption = new Option<string[]>(
            aliases: new[] { "--skills", "-s" },
            description: "Optional explicit skills (repeat option for multiple)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        agentCreateForTaskCommand.AddOption(agentTaskTypeOption);
        agentCreateForTaskCommand.AddOption(agentTaskNameOption);
        agentCreateForTaskCommand.AddOption(agentTaskPurposeOption);
        agentCreateForTaskCommand.AddOption(agentTaskSkillsOption);
        agentCreateForTaskCommand.SetHandler(async (string taskType, string? name, string? purpose, string[] skills) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentCreateForTaskTypeCommand(host, taskType, name, purpose, skills);
            Environment.Exit(exitCode);
        }, agentTaskTypeOption, agentTaskNameOption, agentTaskPurposeOption, agentTaskSkillsOption);

        var agentGetCommand = new Command("get", "Get agent details");
        var agentIdGetOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Agent ID")
        { IsRequired = true };
        agentGetCommand.AddOption(agentIdGetOption);
        agentGetCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentGetCommand(host, id);
            Environment.Exit(exitCode);
        }, agentIdGetOption);

        var agentDeleteCommand = new Command("delete", "Delete an agent");
        var agentIdDeleteOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Agent ID")
        { IsRequired = true };
        agentDeleteCommand.AddOption(agentIdDeleteOption);
        agentDeleteCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentDeleteCommand(host, id);
            Environment.Exit(exitCode);
        }, agentIdDeleteOption);

        var agentExecuteCommand = new Command("execute", "Execute a task using an agent");
        var agentIdExecuteOption = new Option<string>(
            aliases: new[] { "--agent-id", "-a" },
            description: "Agent ID to use for execution")
        { IsRequired = true };
        var agentGoalOption = new Option<string>(
            aliases: new[] { "--goal", "-g" },
            description: "Task goal or objective")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await AgentExecuteCommand(host, agentId, goal, maxIterations, timeout, tokenBudget);
            Environment.Exit(exitCode);
        }, agentIdExecuteOption, agentGoalOption, agentMaxIterationsOption, agentTimeoutOption, agentTokenBudgetOption);

        var agentLoadCommand = new Command("load", "Load agent(s) from a configuration file or directory");
        var agentConfigPathOption = new Option<string>(
            aliases: new[] { "--path", "-p" },
            description: "Path to configuration file (.json) or directory of configuration files")
        { IsRequired = true };
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
            using var host = CreateHost();
            var exitCode = await AgentLoadCommand(host, path, recursive, validateOnly);
            Environment.Exit(exitCode);
        }, agentConfigPathOption, agentRecursiveOption, agentValidateOnlyOption);

        agentCommand.AddCommand(agentListCommand);
        agentCommand.AddCommand(agentCreateCommand);
        agentCommand.AddCommand(agentCreateForTaskCommand);
        agentCommand.AddCommand(agentGetCommand);
        agentCommand.AddCommand(agentDeleteCommand);
        agentCommand.AddCommand(agentExecuteCommand);
        agentCommand.AddCommand(agentLoadCommand);
        rootCommand.AddCommand(agentCommand);

        // Learning command
        var learningCommand = new Command("learning", "Learning management commands");

        var learningListCommand = new Command("list", "List learnings with optional filters");
        var learningStatusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status (Active, Suppressed, Superseded, Archived)");
        var learningScopeOption = new Option<string?>(
            aliases: new[] { "--scope", "-c" },
            description: "Filter by scope (Global, Project, Agent, Task, User)");
        var learningAgentOption = new Option<string?>(
            aliases: new[] { "--agent", "-a" },
            description: "Filter by source agent ID");
        var learningMinConfidenceOption = new Option<double?>(
            aliases: new[] { "--min-confidence", "-m" },
            description: "Filter by minimum confidence (0.0-1.0)");
        learningListCommand.AddOption(learningStatusOption);
        learningListCommand.AddOption(learningScopeOption);
        learningListCommand.AddOption(learningAgentOption);
        learningListCommand.AddOption(learningMinConfidenceOption);
        learningListCommand.SetHandler(async (string? status, string? scope, string? agent, double? minConfidence) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningListCommand(host, status, scope, agent, minConfidence);
            Environment.Exit(exitCode);
        }, learningStatusOption, learningScopeOption, learningAgentOption, learningMinConfidenceOption);

        var learningCreateCommand = new Command("create", "Manually create a new learning");
        var learningCreateTitleOption = new Option<string>(
            aliases: new[] { "--title", "-t" },
            description: "Short summary of the learning")
        { IsRequired = true };
        var learningCreateDescOption = new Option<string>(
            aliases: new[] { "--description", "-d" },
            description: "Full explanation of what was learned")
        { IsRequired = true };
        var learningCreateScopeOption = new Option<string>(
            aliases: new[] { "--scope", "-s" },
            description: "Scope where this applies: Global, Agent, Skill, Project, Domain",
            getDefaultValue: () => "Global");
        var learningCreateConfidenceOption = new Option<double>(
            aliases: new[] { "--confidence", "-c" },
            description: "Confidence score (0.0-1.0)",
            getDefaultValue: () => 0.7);
        var learningCreateTagsOption = new Option<string?>(
            aliases: new[] { "--tags", "-g" },
            description: "Comma-separated tags for filtering");
        var learningCreateSourceAgentOption = new Option<string?>(
            aliases: new[] { "--source-agent", "-a" },
            description: "Source agent ID that should benefit from this learning");
        var learningCreateSourceTaskOption = new Option<string?>(
            aliases: new[] { "--source-task" },
            description: "Source task ID for provenance tracking");
        learningCreateCommand.AddOption(learningCreateTitleOption);
        learningCreateCommand.AddOption(learningCreateDescOption);
        learningCreateCommand.AddOption(learningCreateScopeOption);
        learningCreateCommand.AddOption(learningCreateConfidenceOption);
        learningCreateCommand.AddOption(learningCreateTagsOption);
        learningCreateCommand.AddOption(learningCreateSourceAgentOption);
        learningCreateCommand.AddOption(learningCreateSourceTaskOption);
        learningCreateCommand.SetHandler(async (string title, string description, string scope, double confidence, string? tags, string? sourceAgent, string? sourceTask) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningCreateCommand(host, title, description, scope, confidence, tags, sourceAgent, sourceTask);
            Environment.Exit(exitCode);
        }, learningCreateTitleOption, learningCreateDescOption, learningCreateScopeOption, learningCreateConfidenceOption, learningCreateTagsOption, learningCreateSourceAgentOption, learningCreateSourceTaskOption);

        var learningViewCommand = new Command("view", "View detailed information about a specific learning");
        var learningIdViewOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Learning ID")
        { IsRequired = true };
        learningViewCommand.AddOption(learningIdViewOption);
        learningViewCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningViewCommand(host, id);
            Environment.Exit(exitCode);
        }, learningIdViewOption);

        var learningEditCommand = new Command("edit", "Edit learning properties");
        var learningIdEditOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Learning ID")
        { IsRequired = true };
        var learningEditTitleOption = new Option<string?>(
            aliases: new[] { "--title" },
            description: "Update title");
        var learningEditDescOption = new Option<string?>(
            aliases: new[] { "--description", "-d" },
            description: "Update description");
        var learningEditConfidenceOption = new Option<double?>(
            aliases: new[] { "--confidence", "-c" },
            description: "Update confidence (0.0-1.0)");
        var learningEditTagsOption = new Option<string?>(
            aliases: new[] { "--tags", "-t" },
            description: "Update tags (comma-separated)");
        var learningEditStatusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Update status (Active, Suppressed, Superseded, Archived)");
        var learningEditScopeOption = new Option<string?>(
            aliases: new[] { "--scope" },
            description: "Update scope (Global, Project, Agent, Task, User)");
        learningEditCommand.AddOption(learningIdEditOption);
        learningEditCommand.AddOption(learningEditTitleOption);
        learningEditCommand.AddOption(learningEditDescOption);
        learningEditCommand.AddOption(learningEditConfidenceOption);
        learningEditCommand.AddOption(learningEditTagsOption);
        learningEditCommand.AddOption(learningEditStatusOption);
        learningEditCommand.AddOption(learningEditScopeOption);
        learningEditCommand.SetHandler(async (string id, string? title, string? description, double? confidence, string? tags, string? status, string? scope) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningEditCommand(host, id, title, description, confidence, tags, status, scope);
            Environment.Exit(exitCode);
        }, learningIdEditOption, learningEditTitleOption, learningEditDescOption, learningEditConfidenceOption, learningEditTagsOption, learningEditStatusOption, learningEditScopeOption);

        var learningStatsCommand = new Command("stats", "Show learning statistics and aggregates");
        learningStatsCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await LearningStatsCommand(host);
            Environment.Exit(exitCode);
        });

        var learningSuppressCommand = new Command("suppress", "Suppress a learning (prevent injection into prompts)");
        var learningIdSuppressOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Learning ID")
        { IsRequired = true };
        learningSuppressCommand.AddOption(learningIdSuppressOption);
        learningSuppressCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningSuppressCommand(host, id);
            Environment.Exit(exitCode);
        }, learningIdSuppressOption);

        var learningPromoteCommand = new Command("promote", "Promote a learning to broader scope (Skill→Agent→Project→Domain→Global)");
        var learningIdPromoteOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Learning ID")
        { IsRequired = true };
        learningPromoteCommand.AddOption(learningIdPromoteOption);
        learningPromoteCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningPromoteCommand(host, id);
            Environment.Exit(exitCode);
        }, learningIdPromoteOption);

        var learningSupersedeCommand = new Command("supersede", "Mark a learning as superseded (replaced by newer learning)");
        var learningIdSupersedeOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Learning ID")
        { IsRequired = true };
        learningSupersedeCommand.AddOption(learningIdSupersedeOption);
        learningSupersedeCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await LearningSupersedeCommand(host, id);
            Environment.Exit(exitCode);
        }, learningIdSupersedeOption);

        learningCommand.AddCommand(learningListCommand);
        learningCommand.AddCommand(learningCreateCommand);
        learningCommand.AddCommand(learningViewCommand);
        learningCommand.AddCommand(learningEditCommand);
        learningCommand.AddCommand(learningStatsCommand);
        learningCommand.AddCommand(learningSuppressCommand);
        learningCommand.AddCommand(learningPromoteCommand);
        learningCommand.AddCommand(learningSupersedeCommand);
        rootCommand.AddCommand(learningCommand);

        // Agent promotion proposal commands (KBP-REQ-003)
        var agentProposalCommand = new Command("agent-proposal", "Agent-proposed learning promotions requiring confirmation");

        var agentProposalListCommand = new Command("list", "List pending agent-proposed promotions");
        var proposalStatusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status (Pending, Approved, Rejected)");
        agentProposalListCommand.AddOption(proposalStatusOption);
        agentProposalListCommand.SetHandler(async (string? status) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentProposalListCommand(host, status);
            Environment.Exit(exitCode);
        }, proposalStatusOption);

        var agentProposalViewCommand = new Command("view", "View detailed information about a proposal");
        var proposalIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Proposal ID")
        { IsRequired = true };
        agentProposalViewCommand.AddOption(proposalIdOption);
        agentProposalViewCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentProposalViewCommand(host, id);
            Environment.Exit(exitCode);
        }, proposalIdOption);

        var agentProposalApproveCommand = new Command("approve", "Approve an agent-proposed promotion");
        var approveProposalIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Proposal ID")
        { IsRequired = true };
        agentProposalApproveCommand.AddOption(approveProposalIdOption);
        agentProposalApproveCommand.SetHandler(async (string id) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentProposalApproveCommand(host, id);
            Environment.Exit(exitCode);
        }, approveProposalIdOption);

        var agentProposalRejectCommand = new Command("reject", "Reject an agent-proposed promotion");
        var rejectProposalIdOption = new Option<string>(
            aliases: new[] { "--id" },
            description: "Proposal ID")
        { IsRequired = true };
        var rejectReasonOption = new Option<string?>(
            aliases: new[] { "--reason", "-r" },
            description: "Optional reason for rejection");
        agentProposalRejectCommand.AddOption(rejectProposalIdOption);
        agentProposalRejectCommand.AddOption(rejectReasonOption);
        agentProposalRejectCommand.SetHandler(async (string id, string? reason) =>
        {
            using var host = CreateHost();
            var exitCode = await AgentProposalRejectCommand(host, id, reason);
            Environment.Exit(exitCode);
        }, rejectProposalIdOption, rejectReasonOption);

        var agentProposalStatsCommand = new Command("stats", "Show statistics about agent promotion proposals");
        agentProposalStatsCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await AgentProposalStatsCommand(host);
            Environment.Exit(exitCode);
        });

        agentProposalCommand.AddCommand(agentProposalListCommand);
        agentProposalCommand.AddCommand(agentProposalViewCommand);
        agentProposalCommand.AddCommand(agentProposalApproveCommand);
        agentProposalCommand.AddCommand(agentProposalRejectCommand);
        agentProposalCommand.AddCommand(agentProposalStatsCommand);
        rootCommand.AddCommand(agentProposalCommand);

        // Knowledge commands
        var knowledgeCommand = new Command("knowledge", "Knowledge layer management (indexing and search)");

        var knowledgeLoadCommand = new Command("load-index", "Load topic embeddings into memory for fast search");
        knowledgeLoadCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await KnowledgeLoadIndexCommand(host);
            Environment.Exit(exitCode);
        });

        // Knowledge indexing status commands (CT-REQ-005)
        var knowledgeIndexCommand = new Command("index", "Indexing status and file browser");
        
        var knowledgeIndexStatusCommand = new Command("status", "Show overall indexing statistics");
        knowledgeIndexStatusCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await KnowledgeIndexStatusCommand(host);
            Environment.Exit(exitCode);
        });

        var knowledgeIndexListCommand = new Command("list", "List indexed files with status");
        var indexFilterOption = new Option<string?>(
            aliases: new[] { "--filter", "-f" },
            description: "Filter by status (indexed, error, pending, warning)");
        var indexFormatOption = new Option<string?>(
            aliases: new[] { "--format" },
            description: "Filter by file format (pdf, docx, md, txt, etc.)");
        var indexSearchOption = new Option<string?>(
            aliases: new[] { "--search", "-s" },
            description: "Search files by path");
        knowledgeIndexListCommand.AddOption(indexFilterOption);
        knowledgeIndexListCommand.AddOption(indexFormatOption);
        knowledgeIndexListCommand.AddOption(indexSearchOption);
        knowledgeIndexListCommand.SetHandler(async (string? filter, string? format, string? search) =>
        {
            using var host = CreateHost();
            var exitCode = await KnowledgeIndexListCommand(host, filter, format, search);
            Environment.Exit(exitCode);
        }, indexFilterOption, indexFormatOption, indexSearchOption);

        var knowledgeIndexDetailsCommand = new Command("details", "Show detailed information about a specific file");
        var filePathOption = new Option<string>(
            aliases: new[] { "--path", "-p" },
            description: "File path to show details for")
        { IsRequired = true };
        knowledgeIndexDetailsCommand.AddOption(filePathOption);
        knowledgeIndexDetailsCommand.SetHandler(async (string filePath) =>
        {
            using var host = CreateHost();
            var exitCode = await KnowledgeIndexDetailsCommand(host, filePath);
            Environment.Exit(exitCode);
        }, filePathOption);

        knowledgeIndexCommand.AddCommand(knowledgeIndexStatusCommand);
        knowledgeIndexCommand.AddCommand(knowledgeIndexListCommand);
        knowledgeIndexCommand.AddCommand(knowledgeIndexDetailsCommand);
        knowledgeCommand.AddCommand(knowledgeIndexCommand);
        knowledgeCommand.AddCommand(knowledgeLoadCommand);
        rootCommand.AddCommand(knowledgeCommand);

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
            using var host = CreateHost();
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
            using var host = CreateHost();
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
            using var host = CreateHost();
            var exitCode = await OcrTestCommand(host);
            Environment.Exit(exitCode);
        });

        ocrCommand.AddCommand(ocrTestCommand);
        rootCommand.AddCommand(ocrCommand);

        // Learning promotion commands (KBP-REQ-002)
        var learningPromotionCommand = new Command("learning-promote", "Learning promotion commands");

        var listTaskLearningsCommand = new Command("list-from-task", "List learnings from a completed task");
        var taskIdArgument = new Argument<string>("taskId", "The task ID to list learnings from");
        listTaskLearningsCommand.AddArgument(taskIdArgument);
        listTaskLearningsCommand.SetHandler(async (string taskId) =>
        {
            using var host = CreateHost();
            var exitCode = await ListTaskLearningsCommand(host, taskId);
            Environment.Exit(exitCode);
        }, taskIdArgument);

        var promoteFromTaskCommand = new Command("from-task", "Promote learnings from a completed task");
        var promoteTaskIdArgument = new Argument<string>("taskId", "The task ID whose learnings to promote");
        var learningIdsOption = new Option<string[]>(
            aliases: new[] { "--learning-ids", "-l" },
            description: "Learning IDs to promote (repeat option for multiple)")
        {
            IsRequired = true,
            Arity = ArgumentArity.OneOrMore
        };
        var targetScopesOption = new Option<string[]>(
            aliases: new[] { "--target-scopes", "-s" },
            description: "Target scope for each learning (must match count of learning-ids)")
        {
            IsRequired = true,
            Arity = ArgumentArity.OneOrMore
        };
        var promotionNotesOption = new Option<string?>(
            aliases: new[] { "--notes", "-n" },
            description: "Optional notes about the promotion");

        promoteFromTaskCommand.AddArgument(promoteTaskIdArgument);
        promoteFromTaskCommand.AddOption(learningIdsOption);
        promoteFromTaskCommand.AddOption(targetScopesOption);
        promoteFromTaskCommand.AddOption(promotionNotesOption);
        promoteFromTaskCommand.SetHandler(async (string taskId, string[] learningIds, string[] targetScopes, string? notes) =>
        {
            using var host = CreateHost();
            var exitCode = await PromoteLearningsFromTaskCommand(host, taskId, learningIds, targetScopes, notes);
            Environment.Exit(exitCode);
        }, promoteTaskIdArgument, learningIdsOption, targetScopesOption, promotionNotesOption);

        learningPromotionCommand.AddCommand(listTaskLearningsCommand);
        learningPromotionCommand.AddCommand(promoteFromTaskCommand);
        rootCommand.AddCommand(learningPromotionCommand);

        // Promotion history commands (KBP-ACC-002)
        var promotionHistoryCommand = new Command("promotion-history", "View promotion history and audit trail");

        var listPromotionsCommand = new Command("list", "List all promotions (most recent first)");
        var limitOption = new Option<int>(
            aliases: ["--limit", "-n"],
            description: "Maximum number of promotions to display",
            getDefaultValue: () => 20);
        listPromotionsCommand.AddOption(limitOption);
        listPromotionsCommand.SetHandler(async (int limit) =>
        {
            using var host = CreateHost();
            var exitCode = await ListPromotionHistoryCommand(host, limit);
            Environment.Exit(exitCode);
        }, limitOption);

        var viewPromotionHistoryCommand = new Command("view", "View promotion history for a specific learning");
        var learningIdArgForHistory = new Argument<string>("learning-id", "Learning ID to view history for");
        viewPromotionHistoryCommand.AddArgument(learningIdArgForHistory);
        viewPromotionHistoryCommand.SetHandler(async (string learningId) =>
        {
            using var host = CreateHost();
            var exitCode = await ViewLearningPromotionHistoryCommand(host, learningId);
            Environment.Exit(exitCode);
        }, learningIdArgForHistory);

        var byTaskCommand = new Command("by-task", "View promotions from a specific task");
        var taskIdArgForHistory = new Argument<string>("task-id", "Task ID to filter promotions");
        byTaskCommand.AddArgument(taskIdArgForHistory);
        byTaskCommand.SetHandler(async (string taskId) =>
        {
            using var host = CreateHost();
            var exitCode = await ViewPromotionsByTaskCommand(host, taskId);
            Environment.Exit(exitCode);
        }, taskIdArgForHistory);

        var byScopeCommand = new Command("by-scope", "View promotions to a specific scope");
        var scopeArgForHistory = new Argument<string>("scope", "Target scope (Skill, Agent, Project, Domain, Global)");
        byScopeCommand.AddArgument(scopeArgForHistory);
        byScopeCommand.SetHandler(async (string scope) =>
        {
            using var host = CreateHost();
            var exitCode = await ViewPromotionsByScopeCommand(host, scope);
            Environment.Exit(exitCode);
        }, scopeArgForHistory);

        var promotionStatsCommand = new Command("stats", "Show statistics about promotion activity");
        promotionStatsCommand.SetHandler(async () =>
        {
            using var host = CreateHost();
            var exitCode = await ShowPromotionStatsCommand(host);
            Environment.Exit(exitCode);
        });

        promotionHistoryCommand.AddCommand(listPromotionsCommand);
        promotionHistoryCommand.AddCommand(viewPromotionHistoryCommand);
        promotionHistoryCommand.AddCommand(byTaskCommand);
        promotionHistoryCommand.AddCommand(byScopeCommand);
        promotionHistoryCommand.AddCommand(promotionStatsCommand);
        rootCommand.AddCommand(promotionHistoryCommand);

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

                // Add knowledge layer (Tier 1/2 vector indexing and search)
                services.AddKnowledgeLayer();

                // Add model execution layer (Foundry Local integration)
                services.AddModelExecutionServices(context.Configuration);

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
            
            // Queue Metrics (CT-REQ-004)
            DisplayQueueMetrics(host);

            Console.WriteLine();
            Console.WriteLine("NOTE: Full hardware detection pending integration.");
            Console.WriteLine("      Use 'db status' to check database, 'projects list' for projects.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show dashboard: {ex.Message}");
            return 1;
        }
    }

    private static void DisplayQueueMetrics(IHost host)
    {
        try
        {
            // This would integrate with the real DashboardService when the ModelQueue service is available
            Console.WriteLine("QUEUE METRICS (CT-REQ-004):");
            Console.WriteLine("  Current Model: Not loaded");
            Console.WriteLine("  Average Wait Time: 0.0 seconds");
            Console.WriteLine("  Throughput: 0.0 requests/minute");
            Console.WriteLine("  Model Utilization: 0%");
            Console.WriteLine();
            
            // Priority Distribution
            Console.WriteLine("PRIORITY DISTRIBUTION:");
            Console.WriteLine("  Immediate: 0");
            Console.WriteLine("  Normal: 0");
            Console.WriteLine("  Background: 0");
            Console.WriteLine();
            
            // Top Items (CT-REQ-004)
            Console.WriteLine("TOP QUEUED ITEMS:");
            Console.WriteLine("  (No queued items)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Failed to display queue metrics: {ex.Message}");
        }
    }

    /// <summary>
    /// CT-REQ-006: Displays agent definitions and active execution summary.
    /// Active in-process executions are only visible when agents are running in this process.
    /// </summary>
    private static async Task<int> DashboardAgentsCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Displaying agent dashboard");

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("              DAIV3 AGENT ACTIVITY (CT-REQ-006)            ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Active in-process executions (populated when agents run in this process)
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();
            var metricsCollector = host.Services.GetRequiredService<Daiv3.Orchestration.AgentExecutionMetricsCollector>();
            var activeExecutions = agentManager.GetActiveExecutions();

            Console.WriteLine($"ACTIVE EXECUTIONS: {activeExecutions.Count}");
            if (activeExecutions.Count > 0)
            {
                Console.WriteLine();
                foreach (var control in activeExecutions)
                {
                    var metrics = metricsCollector.GetMetricsSnapshot(control.ExecutionId);
                    var agent = await agentManager.GetAgentAsync(control.AgentId);
                    var agentName = agent?.Name ?? control.AgentId.ToString("N")[..8];
                    var state = control.IsStopped ? "Stopped" : control.IsPaused ? "Paused" : "Running";

                    Console.WriteLine($"  Agent:      {agentName}");
                    Console.WriteLine($"  State:      {state}");
                    if (metrics != null)
                    {
                        Console.WriteLine($"  Iterations: {metrics.TotalIterations}");
                        Console.WriteLine($"  Tokens:     {metrics.TotalTokensConsumed:N0}");
                        Console.WriteLine($"  Started:    {metrics.StartedAt.ToLocalTime():HH:mm:ss}");
                        var elapsed = metrics.TotalDuration;
                        Console.WriteLine($"  Elapsed:    {(elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m" : elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s" : $"{elapsed.Seconds}s")}");
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("  (No active executions in this process)");
                Console.WriteLine("  NOTE: Agent executions running in the Worker service are tracked separately.");
                Console.WriteLine();
            }

            // Registered agents (from persistence)
            var agents = await agentManager.ListAgentsAsync();
            Console.WriteLine($"REGISTERED AGENTS: {agents.Count}");
            if (agents.Count > 0)
            {
                foreach (var agent in agents)
                {
                    Console.WriteLine($"  [{agent.Id:N}]  {agent.Name}");
                    Console.WriteLine($"    Purpose: {agent.Purpose}");
                    Console.WriteLine($"    Skills:  {(agent.EnabledSkills.Count > 0 ? string.Join(", ", agent.EnabledSkills) : "(none)")}");
                }
            }
            else
            {
                Console.WriteLine("  (No registered agents)");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display agent dashboard: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// CT-REQ-006: Displays system resource metrics (CPU, memory, disk).
    /// Uses process-level and GC metrics available in any .NET process.
    /// </summary>
    private static int DashboardResourcesCommand()
    {
        try
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("           DAIV3 SYSTEM RESOURCES (CT-REQ-006)             ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var coreCount = Environment.ProcessorCount;

            // CPU
            Console.WriteLine("CPU:");
            Console.WriteLine($"  Logical Cores:    {coreCount}");
            Console.WriteLine($"  Process CPU Time: {proc.TotalProcessorTime.TotalSeconds:F1}s total");
            Console.WriteLine();

            // Memory (GC-based system info)
            var gcInfo = GC.GetGCMemoryInfo();
            var totalSystemMemBytes = gcInfo.TotalAvailableMemoryBytes;
            var usedSystemMemBytes = totalSystemMemBytes - gcInfo.MemoryLoadBytes;
            var processWorkingSet = proc.WorkingSet64;

            Console.WriteLine("MEMORY:");
            if (totalSystemMemBytes > 0)
            {
                var usedGb = usedSystemMemBytes / (1024.0 * 1024 * 1024);
                var totalGb = totalSystemMemBytes / (1024.0 * 1024 * 1024);
                var utilPct = (double)(totalSystemMemBytes - gcInfo.MemoryLoadBytes) / totalSystemMemBytes * 100;
                Console.WriteLine($"  Used / Total:     {usedGb:F1} / {totalGb:F1} GB");
                Console.WriteLine($"  Available:        {gcInfo.MemoryLoadBytes / (1024.0 * 1024 * 1024):F1} GB committed");
            }
            Console.WriteLine($"  Process Working:  {processWorkingSet / (1024.0 * 1024):F0} MB");
            Console.WriteLine();

            // Disk
            Console.WriteLine("DISK:");
            try
            {
                var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
                var driveInfo = new DriveInfo(systemDrive);
                var freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var totalGb = driveInfo.TotalSize / (1024.0 * 1024 * 1024);
                var usedGb = totalGb - freeGb;
                var usedPct = usedGb / totalGb * 100;

                Console.WriteLine($"  Drive:            {systemDrive}");
                Console.WriteLine($"  Used / Total:     {usedGb:F1} / {totalGb:F1} GB ({usedPct:F1}% used)");
                Console.WriteLine($"  Free:             {freeGb:F1} GB");

                // Alerts
                if (driveInfo.AvailableFreeSpace < 1L * 1024 * 1024 * 1024)
                    Console.WriteLine("  ⚠ WARNING: Less than 1 GB disk space remaining!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  (Could not read disk info: {ex.Message})");
            }
            Console.WriteLine();

            // Storage: KB/model cache
            Console.WriteLine("APPLICATION STORAGE:");
            var appDataBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3");
            PrintDirectorySize("  Knowledge Base:", Path.Combine(appDataBase, "knowledge"));
            PrintDirectorySize("  Model Cache:   ", Path.Combine(appDataBase, "models"));
            Console.WriteLine();

            Console.WriteLine("NOTE: System-wide CPU% requires sustained sampling; GPU/NPU metrics pending HW-NFR-002.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display resource dashboard: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> DashboardAdminCommand(IHost host, bool asJson, bool watch, bool history)
    {
        try
        {
            if (watch && history)
            {
                Console.WriteLine("✗ --watch and --history cannot be used together.");
                return 1;
            }

            if (history)
            {
                return await DashboardAdminHistoryCommand(asJson).ConfigureAwait(false);
            }

            if (watch)
            {
                return await DashboardAdminWatchCommand(host, asJson).ConfigureAwait(false);
            }

            var snapshot = await CollectAdminDashboardSnapshotAsync(host).ConfigureAwait(false);
            await AppendDashboardSnapshotAsync(snapshot).ConfigureAwait(false);
            PrintDashboardAdminSnapshot(snapshot, asJson);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display admin dashboard: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> DashboardAdminWatchCommand(IHost host, bool asJson)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Starting admin dashboard watch mode (refresh every 3 seconds). Press Ctrl+C to stop.");

        while (!cts.IsCancellationRequested)
        {
            var snapshot = await CollectAdminDashboardSnapshotAsync(host).ConfigureAwait(false);
            await AppendDashboardSnapshotAsync(snapshot).ConfigureAwait(false);

            if (!asJson)
            {
                Console.Clear();
                Console.WriteLine($"Last refresh: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }

            PrintDashboardAdminSnapshot(snapshot, asJson);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return 0;
    }

    private static async Task<int> DashboardAdminHistoryCommand(bool asJson)
    {
        var snapshots = await LoadDashboardSnapshotsAsync().ConfigureAwait(false);
        var summary = AdminDashboardCliHistory.BuildSummary(snapshots, DateTimeOffset.UtcNow);

        if (asJson)
        {
            var payload = new
            {
                WindowHours = 24,
                summary.WindowStartUtc,
                summary.WindowEndUtc,
                summary.SampleCount,
                summary.CpuPercent,
                summary.MemoryPercent,
                summary.QueueDepth,
                summary.DiskFreeGb
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("         DAIV3 ADMIN DASHBOARD HISTORY (24 HOURS)         ");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Window: {summary.WindowStartUtc:yyyy-MM-dd HH:mm:ss} UTC to {summary.WindowEndUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Samples: {summary.SampleCount}");
        Console.WriteLine();

        if (summary.SampleCount == 0)
        {
            Console.WriteLine("No snapshots available yet. Run 'dashboard admin' or 'dashboard admin --watch' to collect metrics.");
            return 0;
        }

        Console.WriteLine("CPU % (min/avg/max):");
        Console.WriteLine($"  {summary.CpuPercent.Min:F1} / {summary.CpuPercent.Avg:F1} / {summary.CpuPercent.Max:F1}");
        Console.WriteLine("Memory % (min/avg/max):");
        Console.WriteLine($"  {summary.MemoryPercent.Min:F1} / {summary.MemoryPercent.Avg:F1} / {summary.MemoryPercent.Max:F1}");
        Console.WriteLine("Queue Depth (min/avg/max):");
        Console.WriteLine($"  {summary.QueueDepth.Min:F0} / {summary.QueueDepth.Avg:F1} / {summary.QueueDepth.Max:F0}");
        Console.WriteLine("Disk Free GB (min/avg/max):");
        Console.WriteLine($"  {summary.DiskFreeGb.Min:F2} / {summary.DiskFreeGb.Avg:F2} / {summary.DiskFreeGb.Max:F2}");

        return 0;
    }

    private static async Task<AdminDashboardCliSnapshot> CollectAdminDashboardSnapshotAsync(IHost host)
    {
        var now = DateTimeOffset.UtcNow;

        // Process-level CPU estimate sampled over a short window.
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wallStart = DateTime.UtcNow;
        await Task.Delay(200).ConfigureAwait(false);
        proc.Refresh();
        var cpuEnd = proc.TotalProcessorTime;
        var wallEnd = DateTime.UtcNow;

        var wallMs = Math.Max(1.0, (wallEnd - wallStart).TotalMilliseconds);
        var cpuMs = (cpuEnd - cpuStart).TotalMilliseconds;
        var cpuPercent = Math.Clamp((cpuMs / (wallMs * Environment.ProcessorCount)) * 100.0, 0.0, 100.0);

        var gcInfo = GC.GetGCMemoryInfo();
        var totalSystemMemBytes = gcInfo.TotalAvailableMemoryBytes;
        var usedSystemMemBytes = Math.Max(0, totalSystemMemBytes - gcInfo.MemoryLoadBytes);
        var memoryPercent = totalSystemMemBytes > 0
            ? Math.Clamp((double)usedSystemMemBytes / totalSystemMemBytes * 100.0, 0.0, 100.0)
            : 0.0;

        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
        var driveInfo = new DriveInfo(systemDrive);
        var totalGb = driveInfo.TotalSize / (1024.0 * 1024 * 1024);
        var freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        var usedGb = Math.Max(0.0, totalGb - freeGb);
        var diskUsedPercent = totalGb > 0 ? Math.Clamp(usedGb / totalGb * 100.0, 0.0, 100.0) : 0.0;

        var appDataBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Daiv3");
        var knowledgeBytes = GetDirectorySizeBytes(Path.Combine(appDataBase, "knowledge"));
        var modelCacheBytes = GetDirectorySizeBytes(Path.Combine(appDataBase, "models"));

        var hardware = host.Services.GetService<IHardwareDetectionProvider>();
        var tiers = hardware?.GetAvailableTiers() ?? Array.Empty<HardwareAccelerationTier>();
        var isGpuAvailable = tiers.Contains(HardwareAccelerationTier.Gpu);
        var isNpuAvailable = tiers.Contains(HardwareAccelerationTier.Npu);
        var activeProvider = hardware?.GetBestAvailableTier().ToString() ?? "Cpu";

        var queue = host.Services.GetService<IModelQueue>();
        var queueStatus = queue is null ? null : await queue.GetQueueStatusAsync().ConfigureAwait(false);
        var queueTotal = queueStatus is null
            ? 0
            : queueStatus.ImmediateCount + queueStatus.NormalCount + queueStatus.BackgroundCount;

        var agentManager = host.Services.GetService<IAgentManager>();
        var activeAgents = agentManager?.GetActiveExecutions().Count ?? 0;
        var registeredAgents = agentManager is null
            ? 0
            : (await agentManager.ListAgentsAsync(ct: CancellationToken.None).ConfigureAwait(false)).Count;

        return new AdminDashboardCliSnapshot(
            TimestampUtc: now,
            CpuPercent: cpuPercent,
            MemoryPercent: memoryPercent,
            DiskUsedPercent: diskUsedPercent,
            DiskFreeGb: freeGb,
            IsGpuAvailable: isGpuAvailable,
            IsNpuAvailable: isNpuAvailable,
            ActiveExecutionProvider: activeProvider,
            QueueTotal: queueTotal,
            QueueImmediate: queueStatus?.ImmediateCount ?? 0,
            QueueNormal: queueStatus?.NormalCount ?? 0,
            QueueBackground: queueStatus?.BackgroundCount ?? 0,
            ActiveAgents: activeAgents,
            RegisteredAgents: registeredAgents,
            KnowledgeBaseBytes: knowledgeBytes,
            ModelCacheBytes: modelCacheBytes);
    }

    private static void PrintDashboardAdminSnapshot(AdminDashboardCliSnapshot snapshot, bool asJson)
    {
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("            DAIV3 SYSTEM ADMIN DASHBOARD (CT-REQ-010)     ");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Updated: {snapshot.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine();

        Console.WriteLine("INFRASTRUCTURE:");
        Console.WriteLine($"  CPU Utilization:      {snapshot.CpuPercent:F1}%");
        Console.WriteLine($"  Memory Utilization:   {snapshot.MemoryPercent:F1}%");
        Console.WriteLine($"  Disk Utilization:     {snapshot.DiskUsedPercent:F1}% used ({snapshot.DiskFreeGb:F2} GB free)");
        Console.WriteLine($"  Execution Provider:   {snapshot.ActiveExecutionProvider}");
        Console.WriteLine($"  GPU Available:        {(snapshot.IsGpuAvailable ? "Yes" : "No")}");
        Console.WriteLine($"  NPU Available:        {(snapshot.IsNpuAvailable ? "Yes" : "No")}");
        Console.WriteLine();

        Console.WriteLine("QUEUE STATUS:");
        Console.WriteLine($"  Total:                {snapshot.QueueTotal}");
        Console.WriteLine($"  Immediate:            {snapshot.QueueImmediate}");
        Console.WriteLine($"  Normal:               {snapshot.QueueNormal}");
        Console.WriteLine($"  Background:           {snapshot.QueueBackground}");
        Console.WriteLine();

        Console.WriteLine("AGENT WORKLOAD:");
        Console.WriteLine($"  Active Agents:        {snapshot.ActiveAgents}");
        Console.WriteLine($"  Registered Agents:    {snapshot.RegisteredAgents}");
        Console.WriteLine();

        Console.WriteLine("STORAGE:");
        Console.WriteLine($"  Knowledge Base:       {FormatBytes(snapshot.KnowledgeBaseBytes)}");
        Console.WriteLine($"  Model Cache:          {FormatBytes(snapshot.ModelCacheBytes)}");
        Console.WriteLine();

        Console.WriteLine("Hints: --json for structured output, --watch for live updates, --history for 24h trends.");
    }

    private static string GetDashboardHistoryPath()
    {
        var appDataBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Daiv3",
            "metrics");
        Directory.CreateDirectory(appDataBase);
        return Path.Combine(appDataBase, "admin-dashboard-history.jsonl");
    }

    private static async Task<List<AdminDashboardCliSnapshot>> LoadDashboardSnapshotsAsync()
    {
        var path = GetDashboardHistoryPath();
        if (!File.Exists(path))
        {
            return new List<AdminDashboardCliSnapshot>();
        }

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddHours(-24);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var snapshots = new List<AdminDashboardCliSnapshot>();

        foreach (var line in await File.ReadAllLinesAsync(path).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var snapshot = JsonSerializer.Deserialize<AdminDashboardCliSnapshot>(line, options);
                if (snapshot is not null && snapshot.TimestampUtc >= cutoff)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch
            {
                // Ignore malformed lines to keep history resilient.
            }
        }

        // Re-write a trimmed file so history remains bounded to recent samples.
        var rewritten = snapshots
            .OrderBy(s => s.TimestampUtc)
            .Select(s => JsonSerializer.Serialize(s));
        await File.WriteAllLinesAsync(path, rewritten).ConfigureAwait(false);

        return snapshots;
    }

    private static async Task AppendDashboardSnapshotAsync(AdminDashboardCliSnapshot snapshot)
    {
        var snapshots = await LoadDashboardSnapshotsAsync().ConfigureAwait(false);
        snapshots.Add(snapshot);

        var path = GetDashboardHistoryPath();
        var lines = snapshots
            .OrderBy(s => s.TimestampUtc)
            .Select(s => JsonSerializer.Serialize(s));
        await File.WriteAllLinesAsync(path, lines).ConfigureAwait(false);
    }

    private static long GetDirectorySizeBytes(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f =>
            {
                try { return new FileInfo(f).Length; } catch { return 0L; }
            });
        }
        catch
        {
            return 0;
        }
    }

    private static void PrintDirectorySize(string label, string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"{label} (not found)");
                return;
            }
            var bytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f =>
            {
                try { return new FileInfo(f).Length; } catch { return 0L; }
            });
            var gb = bytes / (1024.0 * 1024 * 1024);
            var mb = bytes / (1024.0 * 1024);
            Console.WriteLine($"{label} {(gb >= 1.0 ? $"{gb:F2} GB" : $"{mb:F1} MB")}");
        }
        catch
        {
            Console.WriteLine($"{label} (error reading)");
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

    // CT-REQ-011: Project dashboard command handlers
    private static async Task<int> ProjectsTreeCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Displaying project tree");

            var rootProjects = await projectRepository.GetRootProjectsAsync().ConfigureAwait(false);

            Console.WriteLine("PROJECT TREE:");
            if (rootProjects.Count == 0)
            {
                Console.WriteLine("  (No projects found)");
                return 0;
            }

            foreach (var project in rootProjects)
            {
                await DisplayProjectTree(project, projectRepository, indent: 0).ConfigureAwait(false);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display project tree: {ex.Message}");
            return 1;
        }
    }

    private static async Task DisplayProjectTree(Project project, ProjectRepository repository, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var statusBadge = GetStatusBadge(project.Status);
        var progressBar = GetProgressBar(project.ProgressPercent);
        var deadlineInfo = project.Deadline.HasValue
            ? $" (Due: {FromUnixSeconds(project.Deadline.Value):yyyy-MM-dd})"
            : "";

        Console.WriteLine($"{indentStr}{statusBadge} {project.Name} [{progressBar}] P{project.Priority}{deadlineInfo}");

        // Display sub-projects
        var subProjects = await repository.GetSubProjectsAsync(project.ProjectId).ConfigureAwait(false);
        foreach (var subProject in subProjects)
        {
            await DisplayProjectTree(subProject, repository, indent + 1).ConfigureAwait(false);
        }
    }

    private static string GetStatusBadge(string status) => status.ToLowerInvariant() switch
    {
        "active" => "🟢",
        "pending" => "🔵",
        "blocked" => "🟡",
        "completed" => "⚫",
        "archived" => "📦",
        "deleted" => "❌",
        _ => "⚪"
    };

    private static string GetProgressBar(double percent)
    {
        var filled = (int)(percent / 10);
        var empty = 10 - filled;
        return $"{new string('█', filled)}{new string('░', empty)} {percent:F0}%";
    }

    private static async Task<int> ProjectsByStatusCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Displaying projects by status");

            var allProjects = await projectRepository.GetAllAsync().ConfigureAwait(false);
            var groupedByStatus = allProjects.GroupBy(p => p.Status).OrderBy(g => g.Key);

            Console.WriteLine("PROJECTS BY STATUS:");
            if (allProjects.Count == 0)
            {
                Console.WriteLine("  (No projects found)");
                return 0;
            }

            foreach (var statusGroup in groupedByStatus)
            {
                Console.WriteLine($"\n{GetStatusBadge(statusGroup.Key)} {statusGroup.Key.ToUpper()} ({statusGroup.Count()})");
                foreach (var project in statusGroup.OrderBy(p => p.Priority).ThenByDescending(p => p.UpdatedAt))
                {
                    var progressBar = GetProgressBar(project.ProgressPercent);
                    var assignedAgent = project.AssignedAgent ?? "Unassigned";
                    Console.WriteLine($"  P{project.Priority} {project.Name} [{progressBar}] - {assignedAgent}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display projects by status: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ProjectsByAgentCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Displaying projects by agent");

            var allProjects = await projectRepository.GetAllAsync().ConfigureAwait(false);
            var groupedByAgent = allProjects.GroupBy(p => p.AssignedAgent ?? "Unassigned")
                .OrderBy(g => g.Key == "Unassigned" ? "zzz" : g.Key);

            Console.WriteLine("PROJECTS BY ASSIGNMENT:");
            if (allProjects.Count == 0)
            {
                Console.WriteLine("  (No projects found)");
                return 0;
            }

            foreach (var agentGroup in groupedByAgent)
            {
                var agentName = agentGroup.Key == "Unassigned" ? "📋 Unassigned" : $"🤖 {agentGroup.Key}";
                Console.WriteLine($"\n{agentName} ({agentGroup.Count()})");
                foreach (var project in agentGroup.OrderBy(p => p.Priority).ThenByDescending(p => p.UpdatedAt))
                {
                    var statusBadge = GetStatusBadge(project.Status);
                    var progressBar = GetProgressBar(project.ProgressPercent);
                    Console.WriteLine($"  {statusBadge} P{project.Priority} {project.Name} [{progressBar}]");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display projects by agent: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ProjectsAnalyticsCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var projectRepository = host.Services.GetRequiredService<ProjectRepository>();
            logger.LogInformation("Displaying project analytics");

            var allProjects = await projectRepository.GetAllAsync().ConfigureAwait(false);
            var activeProjects = allProjects.Where(p => p.Status == "active").ToList();

            Console.WriteLine("PROJECT ANALYTICS:");
            Console.WriteLine();

            // Total counts by status
            Console.WriteLine("📊 PROJECT COUNT BY STATUS:");
            var statusGroups = allProjects.GroupBy(p => p.Status).OrderBy(g => g.Key);
            foreach (var group in statusGroups)
            {
                Console.WriteLine($"  {GetStatusBadge(group.Key)} {group.Key}: {group.Count()}");
            }
            Console.WriteLine($"  Total: {allProjects.Count}");
            Console.WriteLine();

            // Progress metrics
            if (activeProjects.Count > 0)
            {
                var avgProgress = activeProjects.Average(p => p.ProgressPercent);
                Console.WriteLine($"📈 AVERAGE PROGRESS (Active Projects): {avgProgress:F1}%");
                Console.WriteLine();
            }

            // Priority distribution
            Console.WriteLine("🎯 PRIORITY DISTRIBUTION:");
            var priorityGroups = allProjects.GroupBy(p => p.Priority).OrderBy(g => g.Key);
            foreach (var group in priorityGroups)
            {
                var priorityLabel = GetPriorityLabel(group.Key);
                Console.WriteLine($"  {priorityLabel}: {group.Count()}");
            }
            Console.WriteLine();

            // Deadline alerts
            var upcomingDeadlines = await projectRepository.GetProjectsApproachingDeadlineAsync(7).ConfigureAwait(false);
            if (upcomingDeadlines.Count > 0)
            {
                Console.WriteLine($"⚠️  UPCOMING DEADLINES (Next 7 Days): {upcomingDeadlines.Count}");
                foreach (var project in upcomingDeadlines.Take(5))
                {
                    var daysUntil = (FromUnixSeconds(project.Deadline!.Value) - DateTimeOffset.UtcNow).Days;
                    Console.WriteLine($"  {project.Name} - {daysUntil} days ({FromUnixSeconds(project.Deadline.Value):MMM dd})");
                }
                Console.WriteLine();
            }

            // Cost summary
            var totalEstimatedCost = allProjects.Where(p => p.EstimatedCost.HasValue).Sum(p => p.EstimatedCost!.Value);
            var totalActualCost = allProjects.Where(p => p.ActualCost.HasValue).Sum(p => p.ActualCost!.Value);
            if (totalEstimatedCost > 0 || totalActualCost > 0)
            {
                Console.WriteLine("💰 COST SUMMARY:");
                Console.WriteLine($"  Estimated: ${totalEstimatedCost:F2}");
                Console.WriteLine($"  Actual: ${totalActualCost:F2}");
                if (totalEstimatedCost > 0)
                {
                    var costVariance = totalActualCost - totalEstimatedCost;
                    var variancePercent = (costVariance / totalEstimatedCost) * 100;
                    Console.WriteLine($"  Variance: ${costVariance:F2} ({variancePercent:+0.0;-0.0}%)");
                }
                Console.WriteLine();
            }

            // Agent workload
            var agentWorkload = allProjects.Where(p => p.AssignedAgent != null && p.Status == "active")
                .GroupBy(p => p.AssignedAgent)
                .OrderByDescending(g => g.Count());
            if (agentWorkload.Any())
            {
                Console.WriteLine("🤖 AGENT WORKLOAD (Active Projects):");
                foreach (var group in agentWorkload.Take(5))
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()} active projects");
                }
                Console.WriteLine();
            }

            // Throughput
            var completedProjects = allProjects.Where(p => p.Status == "completed" && p.CompletedAt.HasValue).ToList();
            if (completedProjects.Count > 0)
            {
                var thisWeek = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
                var completedThisWeek = completedProjects.Count(p => p.CompletedAt >= thisWeek);
                Console.WriteLine($"✅ THROUGHPUT:");
                Console.WriteLine($"  Completed this week: {completedThisWeek}");
                Console.WriteLine($"  Total completed: {completedProjects.Count}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to display project analytics: {ex.Message}");
            return 1;
        }
    }

    private static string GetPriorityLabel(int priority) => priority switch
    {
        0 => "P0 (Critical)",
        1 => "P1 (High)",
        2 => "P2 (Normal)",
        3 => "P3 (Low)",
        _ => $"P{priority}"
    };

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

    private static async Task<int> AgentCreateForTaskTypeCommand(
        IHost host,
        string taskType,
        string? name,
        string? purpose,
        string[] skills)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var agentManager = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentManager>();
            logger.LogInformation("Resolving dynamic agent for task type '{TaskType}'", taskType);

            var options = new Daiv3.Orchestration.Interfaces.DynamicAgentCreationOptions
            {
                AgentName = string.IsNullOrWhiteSpace(name) ? null : name,
                Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose,
                EnabledSkills = skills.Length == 0 ? null : new List<string>(skills)
            };

            var agent = await agentManager.GetOrCreateAgentForTaskTypeAsync(taskType, options);

            Console.WriteLine("✓ Dynamic task-type agent resolved successfully");
            Console.WriteLine($"  Task Type: {taskType}");
            Console.WriteLine($"  Agent ID: {agent.Id}");
            Console.WriteLine($"  Name: {agent.Name}");
            Console.WriteLine($"  Purpose: {agent.Purpose}");
            Console.WriteLine($"  Enabled Skills: {(agent.EnabledSkills.Count > 0 ? string.Join(", ", agent.EnabledSkills) : "(none)")}");
            Console.WriteLine($"  Created: {agent.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to resolve dynamic agent for task type: {ex.Message}");
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

    private static async Task<int> LearningListCommand(IHost host, string? status, string? scope, string? agent, double? minConfidence)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Listing learnings with filters - status: {Status}, scope: {Scope}, agent: {Agent}, minConfidence: {MinConfidence}",
                status, scope, agent, minConfidence);

            IReadOnlyList<Learning> learnings;

            // Apply filters based on provided options
            if (!string.IsNullOrWhiteSpace(status))
            {
                learnings = await learningService.GetLearningsByStatusAsync(status);
            }
            else if (!string.IsNullOrWhiteSpace(scope))
            {
                learnings = await learningService.GetLearningsByScopeAsync(scope);
            }
            else if (!string.IsNullOrWhiteSpace(agent))
            {
                learnings = await learningService.GetLearningsBySourceAgentAsync(agent);
            }
            else
            {
                learnings = await learningService.GetAllLearningsAsync();
            }

            // Apply additional filters in-memory
            if (minConfidence.HasValue)
            {
                learnings = learnings.Where(l => l.Confidence >= minConfidence.Value).ToList();
            }

            Console.WriteLine("LEARNINGS:");
            Console.WriteLine("==========");
            if (learnings.Count == 0)
            {
                Console.WriteLine("  (No learnings found)");
                Console.WriteLine();
                Console.WriteLine("Learnings are created automatically when agents learn from feedback or corrections.");
                return 0;
            }

            Console.WriteLine($"Found {learnings.Count} learning(s)");
            Console.WriteLine();

            foreach (var learning in learnings.OrderByDescending(l => l.Confidence).ThenByDescending(l => l.TimesApplied))
            {
                Console.WriteLine($"ID: {learning.LearningId}");
                Console.WriteLine($"  Title: {learning.Title}");
                Console.WriteLine($"  Scope: {learning.Scope}");
                Console.WriteLine($"  Status: {learning.Status}");
                Console.WriteLine($"  Confidence: {learning.Confidence:F3}");
                Console.WriteLine($"  Trigger: {learning.TriggerType}");
                Console.WriteLine($"  Times Applied: {learning.TimesApplied}");
                Console.WriteLine($"  Created: {DateTimeOffset.FromUnixTimeSeconds(learning.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                if (!string.IsNullOrEmpty(learning.SourceAgent))
                {
                    Console.WriteLine($"  Source Agent: {learning.SourceAgent}");
                }
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list learnings: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningCreateCommand(IHost host, string title, string description, string scope, double confidence, string? tags, string? sourceAgent, string? sourceTask)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningOrchService = host.Services.GetRequiredService<LearningService>();

            logger.LogInformation("Creating manual learning: {Title}", title);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine("✗ Title is required and cannot be empty.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                Console.WriteLine("✗ Description is required and cannot be empty.");
                return 1;
            }

            if (confidence < 0.0 || confidence > 1.0)
            {
                Console.WriteLine("✗ Confidence must be between 0.0 and 1.0.");
                return 1;
            }

            // Validate scope
            var validScopes = new[] { "Global", "Agent", "Skill", "Project", "Domain" };
            if (!validScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"✗ Invalid scope '{scope}'. Must be one of: {string.Join(", ", validScopes)}");
                return 1;
            }

            // Create explicit trigger context for manual creation
            var context = new ExplicitTriggerContext
            {
                Title = title,
                Description = description,
                Scope = scope,
                Confidence = confidence,
                Tags = tags,
                SourceAgent = sourceAgent,
                SourceTaskId = sourceTask,
                CreatedBy = "user",
                AgentReasoning = "Manually created by user"
            };

            // Create the learning
            var learning = await learningOrchService.CreateExplicitLearningAsync(context, CancellationToken.None).ConfigureAwait(false);

            Console.WriteLine("✓ Learning created successfully!");
            Console.WriteLine();
            Console.WriteLine($"Learning ID: {learning.LearningId}");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Scope: {learning.Scope}");
            Console.WriteLine($"  Status: {learning.Status}");
            Console.WriteLine($"  Confidence: {learning.Confidence:F3}");
            Console.WriteLine($"  Trigger Type: {learning.TriggerType}");
            Console.WriteLine($"  Created: {DateTimeOffset.FromUnixTimeSeconds(learning.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
            if (!string.IsNullOrEmpty(tags))
            {
                Console.WriteLine($"  Tags: {tags}");
            }
            if (!string.IsNullOrEmpty(sourceAgent))
            {
                Console.WriteLine($"  Source Agent: {sourceAgent}");
            }
            Console.WriteLine();
            Console.WriteLine($"The learning can be injected into prompts for similar tasks. Use 'learning view --id {learning.LearningId}' to view details.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to create learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningViewCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Viewing learning {LearningId}", id);

            var learning = await learningService.GetLearningAsync(id);

            if (learning == null)
            {
                Console.WriteLine($"✗ Learning not found: {id}");
                Console.WriteLine();
                Console.WriteLine("Use 'learning list' to see all available learnings.");
                return 1;
            }

            Console.WriteLine("LEARNING DETAILS:");
            Console.WriteLine("=================");
            Console.WriteLine($"ID: {learning.LearningId}");
            Console.WriteLine($"Title: {learning.Title}");
            Console.WriteLine($"Description: {learning.Description}");
            Console.WriteLine();
            Console.WriteLine("METADATA:");
            Console.WriteLine($"  Trigger Type: {learning.TriggerType}");
            Console.WriteLine($"  Scope: {learning.Scope}");
            Console.WriteLine($"  Status: {learning.Status}");
            Console.WriteLine($"  Confidence: {learning.Confidence:F3}");
            Console.WriteLine($"  Times Applied: {learning.TimesApplied}");

            if (!string.IsNullOrEmpty(learning.Tags))
            {
                Console.WriteLine($"  Tags: {learning.Tags}");
            }

            Console.WriteLine();
            Console.WriteLine("PROVENANCE:");
            if (!string.IsNullOrEmpty(learning.SourceAgent))
            {
                Console.WriteLine($"  Source Agent: {learning.SourceAgent}");
            }
            if (!string.IsNullOrEmpty(learning.SourceTaskId))
            {
                Console.WriteLine($"  Source Task: {learning.SourceTaskId}");
            }
            Console.WriteLine($"  Created By: {learning.CreatedBy}");
            Console.WriteLine($"  Created At: {DateTimeOffset.FromUnixTimeSeconds(learning.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Updated At: {DateTimeOffset.FromUnixTimeSeconds(learning.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");

            Console.WriteLine();
            Console.WriteLine("EMBEDDING:");
            if (learning.EmbeddingBlob != null && learning.EmbeddingBlob.Length > 0)
            {
                Console.WriteLine($"  Dimensions: {learning.EmbeddingDimensions ?? 0}");
                Console.WriteLine($"  Size: {learning.EmbeddingBlob.Length} bytes");
                Console.WriteLine($"  Status: Ready for semantic search");
            }
            else
            {
                Console.WriteLine($"  Status: No embedding (semantic search not available)");
            }

            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to view learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningEditCommand(IHost host, string id, string? title, string? description,
        double? confidence, string? tags, string? status, string? scope)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();

            // Validate at least one update parameter is provided
            if (title == null && description == null && confidence == null && tags == null && status == null && scope == null)
            {
                Console.WriteLine("✗ No updates specified. Provide at least one of: --title, --description, --confidence, --tags, --status, --scope");
                return 1;
            }

            logger.LogInformation("Editing learning {LearningId}", id);

            var learning = await learningService.GetLearningAsync(id);

            if (learning == null)
            {
                Console.WriteLine($"✗ Learning not found: {id}");
                Console.WriteLine();
                Console.WriteLine("Use 'learning list' to see all available learnings.");
                return 1;
            }

            // Show current state
            Console.WriteLine("CURRENT STATE:");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Description: {learning.Description}");
            Console.WriteLine($"  Confidence: {learning.Confidence:F3}");
            Console.WriteLine($"  Tags: {learning.Tags ?? "(none)"}");
            Console.WriteLine($"  Status: {learning.Status}");
            Console.WriteLine($"  Scope: {learning.Scope}");
            Console.WriteLine();

            // Validate and apply updates
            bool hasChanges = false;

            if (title != null)
            {
                learning.Title = title;
                hasChanges = true;
            }

            if (description != null)
            {
                learning.Description = description;
                hasChanges = true;
            }

            if (confidence.HasValue)
            {
                if (confidence.Value < 0.0 || confidence.Value > 1.0)
                {
                    Console.WriteLine($"✗ Invalid confidence: {confidence.Value}. Must be between 0.0 and 1.0.");
                    return 1;
                }
                learning.Confidence = confidence.Value;
                hasChanges = true;
            }

            if (tags != null)
            {
                learning.Tags = tags;
                hasChanges = true;
            }

            if (status != null)
            {
                var validStatuses = new[] { "Active", "Suppressed", "Superseded", "Archived" };
                if (!validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"✗ Invalid status: {status}. Valid options: {string.Join(", ", validStatuses)}");
                    return 1;
                }
                learning.Status = status;
                hasChanges = true;
            }

            if (scope != null)
            {
                var validScopes = new[] { "Global", "Project", "Agent", "Task", "User" };
                if (!validScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"✗ Invalid scope: {scope}. Valid options: {string.Join(", ", validScopes)}");
                    return 1;
                }
                learning.Scope = scope;
                hasChanges = true;
            }

            if (!hasChanges)
            {
                Console.WriteLine("No changes detected.");
                return 0;
            }

            // Update the learning
            await learningService.UpdateLearningAsync(learning);

            Console.WriteLine("✓ Learning updated successfully");
            Console.WriteLine();
            Console.WriteLine("UPDATED STATE:");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Description: {learning.Description}");
            Console.WriteLine($"  Confidence: {learning.Confidence:F3}");
            Console.WriteLine($"  Tags: {learning.Tags ?? "(none)"}");
            Console.WriteLine($"  Status: {learning.Status}");
            Console.WriteLine($"  Scope: {learning.Scope}");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to edit learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningStatsCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Generating learning statistics");

            var allLearnings = await learningService.GetAllLearningsAsync();

            if (allLearnings.Count == 0)
            {
                Console.WriteLine("LEARNING STATISTICS:");
                Console.WriteLine("===================");
                Console.WriteLine("  No learnings found.");
                Console.WriteLine();
                Console.WriteLine("Learnings are created automatically when agents learn from feedback or corrections.");
                return 0;
            }

            Console.WriteLine("LEARNING STATISTICS:");
            Console.WriteLine("===================");
            Console.WriteLine($"Total Learnings: {allLearnings.Count}");
            Console.WriteLine();

            // By Status
            var byStatus = allLearnings.GroupBy(l => l.Status).ToList();
            Console.WriteLine("BY STATUS:");
            foreach (var group in byStatus.OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // By Scope
            var byScope = allLearnings.GroupBy(l => l.Scope).ToList();
            Console.WriteLine("BY SCOPE:");
            foreach (var group in byScope.OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // By Trigger Type
            var byTrigger = allLearnings.GroupBy(l => l.TriggerType).ToList();
            Console.WriteLine("BY TRIGGER TYPE:");
            foreach (var group in byTrigger.OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // Averages
            var avgConfidence = allLearnings.Average(l => l.Confidence);
            var avgTimesApplied = allLearnings.Average(l => l.TimesApplied);
            Console.WriteLine("AVERAGES:");
            Console.WriteLine($"  Average Confidence: {avgConfidence:F3}");
            Console.WriteLine($"  Average Times Applied: {avgTimesApplied:F1}");
            Console.WriteLine();

            // Most Applied
            var mostApplied = allLearnings.OrderByDescending(l => l.TimesApplied).Take(5).ToList();
            if (mostApplied.Any(l => l.TimesApplied > 0))
            {
                Console.WriteLine("MOST APPLIED (Top 5):");
                foreach (var learning in mostApplied.Where(l => l.TimesApplied > 0))
                {
                    Console.WriteLine($"  [{learning.TimesApplied}x] {learning.Title}");
                }
                Console.WriteLine();
            }

            // Embedding Status
            var withEmbedding = allLearnings.Count(l => l.EmbeddingBlob != null && l.EmbeddingBlob.Length > 0);
            Console.WriteLine("EMBEDDING STATUS:");
            Console.WriteLine($"  With Embeddings: {withEmbedding}");
            Console.WriteLine($"  Without Embeddings: {allLearnings.Count - withEmbedding}");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to generate statistics: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningSuppressCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Suppressing learning {LearningId}", id);

            var learning = await learningService.GetLearningAsync(id);

            if (learning == null)
            {
                Console.WriteLine($"✗ Learning not found: {id}");
                Console.WriteLine();
                Console.WriteLine("Use 'learning list' to see all available learnings.");
                return 1;
            }

            Console.WriteLine("SUPPRESSING LEARNING:");
            Console.WriteLine($"  ID: {learning.LearningId}");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Current Status: {learning.Status}");
            Console.WriteLine();

            if (learning.Status == "Suppressed")
            {
                Console.WriteLine("⚠ Learning is already suppressed.");
                return 0;
            }

            await learningService.SuppressLearningAsync(id);

            Console.WriteLine("✓ Learning suppressed successfully");
            Console.WriteLine();
            Console.WriteLine("This learning will no longer be injected into agent prompts.");
            Console.WriteLine("To reactivate, use: learning edit --id {0} --status Active", id);
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to suppress learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningPromoteCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Promoting learning {LearningId}", id);

            var learning = await learningService.GetLearningAsync(id);

            if (learning == null)
            {
                Console.WriteLine($"✗ Learning not found: {id}");
                Console.WriteLine();
                Console.WriteLine("Use 'learning list' to see all available learnings.");
                return 1;
            }

            Console.WriteLine("PROMOTING LEARNING:");
            Console.WriteLine($"  ID: {learning.LearningId}");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Current Scope: {learning.Scope}");
            Console.WriteLine();

            var newScope = await learningService.PromoteLearningAsync(id);

            if (newScope == null)
            {
                Console.WriteLine("⚠ Learning is already at Global scope (highest level).");
                Console.WriteLine();
                Console.WriteLine("Scope hierarchy: Task → Agent → Project → Domain → Global");
                return 0;
            }

            Console.WriteLine("✓ Learning promoted successfully");
            Console.WriteLine($"  New Scope: {newScope}");
            Console.WriteLine();
            Console.WriteLine("Scope hierarchy: Skill → Agent → Project → Domain → Global");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to promote learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> LearningSupersedeCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();
            logger.LogInformation("Superseding learning {LearningId}", id);

            var learning = await learningService.GetLearningAsync(id);

            if (learning == null)
            {
                Console.WriteLine($"✗ Learning not found: {id}");
                Console.WriteLine();
                Console.WriteLine("Use 'learning list' to see all available learnings.");
                return 1;
            }

            Console.WriteLine("SUPERSEDING LEARNING:");
            Console.WriteLine($"  ID: {learning.LearningId}");
            Console.WriteLine($"  Title: {learning.Title}");
            Console.WriteLine($"  Current Status: {learning.Status}");
            Console.WriteLine();

            if (learning.Status == "Superseded")
            {
                Console.WriteLine("⚠ Learning is already superseded.");
                return 0;
            }

            await learningService.SupersedeLearningAsync(id);

            Console.WriteLine("✓ Learning marked as superseded successfully");
            Console.WriteLine();
            Console.WriteLine("This learning has been replaced by a newer, more accurate learning.");
            Console.WriteLine("It will no longer be injected into agent prompts.");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to supersede learning: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> KnowledgeLoadIndexCommand(IHost host)
    {
        try
        {
            Console.WriteLine("KNOWLEDGE INDEX LOADING");
            Console.WriteLine("=======================");
            Console.WriteLine();
            Console.Write("Loading topic embeddings into memory... ");

            await host.Services.InitializeKnowledgeLayerAsync().ConfigureAwait(false);

            Console.WriteLine("✓ Success!");
            Console.WriteLine();

            var indexService = host.Services.GetRequiredService<ITwoTierIndexService>();
            var stats = await indexService.GetStatisticsAsync().ConfigureAwait(false);

            Console.WriteLine("INDEX STATISTICS");
            Console.WriteLine("================");
            Console.WriteLine($"Total documents: {stats.DocumentCount}");
            Console.WriteLine($"Total chunks (Tier 2): {stats.ChunkCount}");
            Console.WriteLine($"Cached topic embeddings: {stats.CachedTopicEmbeddings}");

            if (stats.EstimatedMemoryBytes > 0)
            {
                var memoryMB = stats.EstimatedMemoryBytes / (1024.0 * 1024.0);
                Console.WriteLine($"Memory usage: {memoryMB:F2} MB");
            }

            Console.WriteLine();
            Console.WriteLine("✓ Knowledge index is ready for semantic search");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to load knowledge index:");
            Console.WriteLine($"  Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Details: {ex.InnerException.Message}");
            }
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

    /// <summary>
    /// Lists learnings from a completed task for promotion selection (KBP-REQ-002).
    /// </summary>
    private static async Task<int> ListTaskLearningsCommand(IHost host, string taskId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var service = host.Services.GetRequiredService<LearningStorageService>();

            logger.LogInformation("Listing learnings from task {TaskId}", taskId);

            Console.WriteLine("LEARNINGS FROM TASK");
            Console.WriteLine("===================");
            Console.WriteLine($"Task ID: {taskId}");
            Console.WriteLine();

            var learnings = await service.GetLearningsBySourceTaskAsync(taskId);

            if (learnings.Count == 0)
            {
                Console.WriteLine("No learnings found from this task.");
                return 0;
            }

            Console.WriteLine($"Found {learnings.Count} learnings:");
            Console.WriteLine();

            foreach (var learning in learnings.OrderByDescending(l => l.Confidence))
            {
                Console.WriteLine($"ID: {learning.LearningId}");
                Console.WriteLine($"  Title: {learning.Title}");
                Console.WriteLine($"  Description: {learning.Description}");
                Console.WriteLine($"  Trigger Type: {learning.TriggerType}");
                Console.WriteLine($"  Current Scope: {learning.Scope}");
                Console.WriteLine($"  Confidence: {learning.Confidence:P0}");
                Console.WriteLine($"  Status: {learning.Status}");
                Console.WriteLine($"  Created: {DateTimeOffset.FromUnixTimeSeconds(learning.CreatedAt):u}");

                if (!string.IsNullOrEmpty(learning.Tags))
                {
                    Console.WriteLine($"  Tags: {learning.Tags}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("To promote learnings, use: learning-promote from-task <taskId> -l <learningId1> <learningId2> ... -s <scope1> <scope2> ...");
            logger.LogInformation("✓ Listed {Count} learnings from task {TaskId}", learnings.Count, taskId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list learnings: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to list learnings from task");
            return 1;
        }
    }

    /// <summary>
    /// Promotes selected learnings from a completed task (KBP-REQ-002).
    /// </summary>
    private static async Task<int> PromoteLearningsFromTaskCommand(
        IHost host,
        string taskId,
        string[] learningIds,
        string[] targetScopes,
        string? notes)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var service = host.Services.GetRequiredService<LearningStorageService>();
            var promotionService = host.Services.GetRequiredService<IKnowledgePromotionService>();

            logger.LogInformation(
                "Promoting {Count} learnings from task {TaskId}",
                learningIds.Length, taskId);

            // Validate input
            if (learningIds.Length != targetScopes.Length)
            {
                Console.WriteLine("✗ Error: The number of learning IDs must match the number of target scopes.");
                Console.WriteLine($"  Provided {learningIds.Length} learning IDs but {targetScopes.Length} target scopes");
                return 1;
            }

            var requestedScopeByLearningId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Create promotion selections. Internet-level targets are exported via KBP-REQ-005
            // but persisted as Global scope in learning storage.
            var promotions = new List<LearningPromotionSelection>(learningIds.Length);
            for (var i = 0; i < learningIds.Length; i++)
            {
                var learningId = learningIds[i];
                var requestedScope = targetScopes[i];
                requestedScopeByLearningId[learningId] = requestedScope;

                var storageScope = requestedScope;
                if (promotionService.TryParseLevel(requestedScope, out var level)
                    && level == KnowledgePromotionLevel.Internet)
                {
                    storageScope = "Global";
                }

                promotions.Add(new LearningPromotionSelection
                {
                    LearningId = learningId,
                    TargetScope = storageScope,
                    Notes = notes
                });
            }

            // Execute batch promotion
            var result = await service.PromoteLearningsFromTaskAsync(taskId, promotions.AsReadOnly());

            Console.WriteLine("LEARNING PROMOTIONS");
            Console.WriteLine("==================");
            Console.WriteLine($"Task ID: {taskId}");
            Console.WriteLine($"Total Promotions: {result.TotalCount}");
            Console.WriteLine();

            // Show successful promotions
            if (result.SuccessfulPromotions.Count > 0)
            {
                Console.WriteLine($"✓ Successful Promotions ({result.SuccessfulPromotions.Count}):");
                foreach (var promo in result.SuccessfulPromotions)
                {
                    var displayScope = requestedScopeByLearningId.TryGetValue(promo.LearningId, out var requestedScope)
                        ? requestedScope
                        : promo.TargetScope;
                    Console.WriteLine($"  • Learning {promo.LearningId} → {displayScope}");
                }
                Console.WriteLine();
            }

            // Show failed promotions
            if (result.FailedPromotions.Count > 0)
            {
                Console.WriteLine($"✗ Failed Promotions ({result.FailedPromotions.Count}):");
                foreach (var (promo, error) in result.FailedPromotions)
                {
                    Console.WriteLine($"  • Learning {promo.LearningId} → {promo.TargetScope}");
                    Console.WriteLine($"    Error: {error.Message}");
                }
                Console.WriteLine();
            }

            // Generate and display knowledge summary (KBP-REQ-004)
            if (result.SuccessfulPromotions.Count > 0)
            {
                try
                {
                    var summaryService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IKnowledgeSummaryService>();

                    // Fetch promoted learning entities for summary generation
                    var promotedLearnings = new List<Daiv3.Persistence.Entities.Learning>();
                    foreach (var promo in result.SuccessfulPromotions)
                    {
                        var learning = await service.GetLearningAsync(promo.LearningId);
                        if (learning != null)
                        {
                            promotedLearnings.Add(learning);
                        }
                    }

                    // Build target scope map
                    var targetScopeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var promo in result.SuccessfulPromotions)
                    {
                        var targetScope = requestedScopeByLearningId.TryGetValue(promo.LearningId, out var requestedScope)
                            ? requestedScope
                            : promo.TargetScope;
                        targetScopeMap[promo.LearningId] = targetScope;
                    }

                    // Generate summary
                    var summary = await summaryService.GenerateSummaryAsync(
                        promotedLearnings.AsReadOnly(),
                        targetScopeMap,
                        taskId,
                        "user");

                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine("KNOWLEDGE SUMMARY");
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine(summary.SummaryText);
                    Console.WriteLine();

                    // Create Internet-level draft artifact for review (KBP-REQ-005)
                    var hasInternetPromotion = result.SuccessfulPromotions.Any(promo =>
                        targetScopeMap.TryGetValue(promo.LearningId, out var targetScope)
                        && promotionService.TryParseLevel(targetScope, out var level)
                        && level == KnowledgePromotionLevel.Internet);

                    if (hasInternetPromotion)
                    {
                        var draftService = host.Services.GetRequiredService<IKnowledgeInternetDraftService>();
                        var draft = await draftService.CreateDraftArtifactAsync(
                            promotedLearnings.AsReadOnly(),
                            targetScopeMap,
                            summary,
                            taskId,
                            "user");

                        Console.WriteLine("INTERNET DRAFT ARTIFACT");
                        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Console.WriteLine("✓ Draft created for user review");
                        Console.WriteLine($"  File: {draft.ArtifactPath}");
                        Console.WriteLine();
                    }
                }
                catch (Exception summaryEx)
                {
                    logger.LogWarning(summaryEx, "Failed to generate knowledge summary, but promotions succeeded");
                    Console.WriteLine("⚠ Warning: Summary generation failed, but promotions were successful");
                }
            }

            logger.LogInformation(
                "✓ Promotion completed: {SuccessCount} successful, {FailureCount} failed",
                result.SuccessfulPromotions.Count, result.FailedPromotions.Count);

            return result.FailedPromotions.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to promote learnings: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to promote learnings from task");
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

    // ===== Settings Command Handlers (CT-REQ-001: Local settings storage) =====

    private static async Task<int> SettingsInitCommand(IHost host)
    {
        try
        {
            Console.WriteLine("Initializing application settings...");

            var settingsInitializer = host.Services.GetRequiredService<Daiv3.Persistence.Services.ISettingsInitializer>();
            var count = await settingsInitializer.InitializeDefaultSettingsAsync();

            Console.WriteLine($"✓ Settings initialized successfully");
            Console.WriteLine($"  Initialized {count} settings with default values");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to initialize settings: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SettingsListCommand(IHost host, string? category, bool showSensitive)
    {
        try
        {
            var settingsService = host.Services.GetRequiredService<ISettingsService>();

            IReadOnlyList<Daiv3.Persistence.Entities.AppSetting> settings;
            if (!string.IsNullOrWhiteSpace(category))
            {
                settings = await settingsService.GetSettingsByCategoryAsync(category);
                Console.WriteLine($"SETTINGS (Category: {category}):");
            }
            else
            {
                settings = await settingsService.GetAllSettingsAsync();
                Console.WriteLine("ALL SETTINGS:");
            }

            if (settings.Count == 0)
            {
                Console.WriteLine("  (No settings found)");
                return 0;
            }

            foreach (var setting in settings.OrderBy(s => s.Category).ThenBy(s => s.SettingKey))
            {
                Console.WriteLine($"\n  Key: {setting.SettingKey}");
                Console.WriteLine($"  Category: {setting.Category}");
                
                if (setting.IsSensitive && !showSensitive)
                {
                    Console.WriteLine($"  Value: *** (sensitive, use --show-sensitive to display)");
                }
                else
                {
                    Console.WriteLine($"  Value: {setting.SettingValue}");
                }
                
                Console.WriteLine($"  Type: {setting.ValueType}");
                Console.WriteLine($"  Description: {setting.Description}");
                Console.WriteLine($"  Updated: {DateTimeOffset.FromUnixTimeSeconds(setting.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Updated By: {setting.UpdatedBy}");
            }

            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list settings: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SettingsGetCommand(IHost host, string key)
    {
        try
        {
            var settingsService = host.Services.GetRequiredService<ISettingsService>();
            var setting = await settingsService.GetSettingAsync(key);

            if (setting == null)
            {
                Console.WriteLine($"✗ Setting '{key}' not found");
                return 1;
            }

            Console.WriteLine($"Key: {setting.SettingKey}");
            Console.WriteLine($"Category: {setting.Category}");
            
            if (setting.IsSensitive)
            {
                Console.WriteLine($"Value: *** (sensitive - use settings list --show-sensitive to display all)");
            }
            else
            {
                Console.WriteLine($"Value: {setting.SettingValue}");
            }
            
            Console.WriteLine($"Type: {setting.ValueType}");
            Console.WriteLine($"Description: {setting.Description}");
            Console.WriteLine($"Schema Version: {setting.SchemaVersion}");
            Console.WriteLine($"Created: {DateTimeOffset.FromUnixTimeSeconds(setting.CreatedAt):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Updated: {DateTimeOffset.FromUnixTimeSeconds(setting.UpdatedAt):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Updated By: {setting.UpdatedBy}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get setting: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SettingsSetCommand(IHost host, string key, string value, string reason)
    {
        try
        {
            var settingsService = host.Services.GetRequiredService<ISettingsService>();
            
            // Get the existing setting to determine its category and check if it exists
            var existing = await settingsService.GetSettingAsync(key);
            string category;
            string description;
            bool isSensitive;

            if (existing != null)
            {
                category = existing.Category ?? "general";
                description = existing.Description ?? "No description";
                isSensitive = existing.IsSensitive;
                Console.WriteLine($"Updating existing setting '{key}'...");
            }
            else
            {
                // Use ApplicationSettings helper to get metadata
                category = Daiv3.Core.Settings.ApplicationSettings.GetCategory(key);
                description = Daiv3.Core.Settings.ApplicationSettings.GetDescription(key);
                isSensitive = Daiv3.Core.Settings.ApplicationSettings.IsSensitive(key);
                Console.WriteLine($"Creating new setting '{key}'...");
            }

            await settingsService.SaveSettingAsync(
                key: key,
                value: value,
                category: category,
                description: description,
                isSensitive: isSensitive,
                reason: reason
            );

            Console.WriteLine($"✓ Setting '{key}' updated successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to set setting: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SettingsResetCommand(IHost host, bool confirm)
    {
        try
        {
            if (!confirm)
            {
                Console.Write("⚠ This will reset ALL settings to their default values. Continue? (y/N): ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Reset cancelled");
                    return 0;
                }
            }

            Console.WriteLine("Resetting all settings to defaults...");

            var settingsInitializer = host.Services.GetRequiredService<Daiv3.Persistence.Services.ISettingsInitializer>();
            var count = await settingsInitializer.ResetToDefaultsAsync();

            Console.WriteLine($"✓ Settings reset successfully");
            Console.WriteLine($"  Reset {count} settings to default values");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to reset settings: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SettingsHistoryCommand(IHost host, string key)
    {
        try
        {
            var settingsService = host.Services.GetRequiredService<ISettingsService>();
            var history = await settingsService.GetSettingHistoryAsync(key);

            if (history.Count == 0)
            {
                Console.WriteLine($"No history found for setting '{key}'");
                return 0;
            }

            Console.WriteLine($"CHANGE HISTORY for '{key}':");
            Console.WriteLine();

            foreach (var entry in history.OrderByDescending(h => h.ChangedAt))
            {
                Console.WriteLine($"  Time: {DateTimeOffset.FromUnixTimeSeconds(entry.ChangedAt):yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Changed By: {entry.ChangedBy}");
                Console.WriteLine($"  Reason: {entry.Reason}");
                Console.WriteLine($"  Old Value: {entry.OldValue ?? "(null)"}");
                Console.WriteLine($"  New Value: {entry.NewValue}");
                Console.WriteLine($"  Schema Version: {entry.SchemaVersion}");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get settings history: {ex.Message}");
            return 1;
        }
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

    // Agent Promotion Proposal Command Handlers (KBP-REQ-003)

    private static async Task<int> AgentProposalListCommand(IHost host, string? status)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var proposalService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentPromotionProposalService>();
            logger.LogInformation("Listing agent promotion proposals with status filter: {Status}", status ?? "all");

            IReadOnlyList<Daiv3.Persistence.Entities.AgentPromotionProposal> proposals;

            if (string.IsNullOrEmpty(status))
            {
                proposals = await proposalService.GetPendingProposalsAsync();
                Console.WriteLine("PENDING PROMOTION PROPOSALS:");
            }
            else
            {
                proposals = await proposalService.GetPendingProposalsAsync();
                proposals = proposals.Where(p => p.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
                Console.WriteLine($"PROMOTION PROPOSALS ({status}):");
            }

            if (!proposals.Any())
            {
                Console.WriteLine("  No proposals found.");
                return 0;
            }

            foreach (var proposal in proposals)
            {
                Console.WriteLine();
                Console.WriteLine($"  ID: {proposal.ProposalId}");
                Console.WriteLine($"  Agent: {proposal.ProposingAgent}");
                Console.WriteLine($"  Learning: {proposal.LearningId}");
                Console.WriteLine($"  Scope: {proposal.FromScope} → {proposal.SuggestedTargetScope}");
                Console.WriteLine($"  Confidence: {proposal.ConfidenceScore:P}");
                Console.WriteLine($"  Status: {proposal.Status}");
                if (!string.IsNullOrEmpty(proposal.Justification))
                {
                    Console.WriteLine($"  Justification: {proposal.Justification}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {proposals.Count} proposal(s)");
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list proposals: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentProposalViewCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var proposalService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentPromotionProposalService>();
            logger.LogInformation("Viewing agent promotion proposal {ProposalId}", id);

            var proposal = await proposalService.GetProposalAsync(id);
            if (proposal == null)
            {
                Console.WriteLine($"✗ Proposal not found: {id}");
                return 1;
            }

            Console.WriteLine("PROMOTION PROPOSAL DETAILS:");
            Console.WriteLine($"  ID: {proposal.ProposalId}");
            Console.WriteLine($"  Status: {proposal.Status}");
            Console.WriteLine();
            Console.WriteLine("PROPOSING AGENT:");
            Console.WriteLine($"  Agent ID: {proposal.ProposingAgent}");
            if (!string.IsNullOrEmpty(proposal.SourceTaskId))
            {
                Console.WriteLine($"  Source Task: {proposal.SourceTaskId}");
            }
            Console.WriteLine();
            Console.WriteLine("LEARNING:");
            Console.WriteLine($"  ID: {proposal.LearningId}");
            Console.WriteLine($"  Current Scope: {proposal.FromScope}");
            Console.WriteLine($"  Proposed Target Scope: {proposal.SuggestedTargetScope}");
            Console.WriteLine();
            Console.WriteLine("ASSESSMENT:");
            Console.WriteLine($"  Confidence Score: {proposal.ConfidenceScore:P}");
            if (!string.IsNullOrEmpty(proposal.Justification))
            {
                Console.WriteLine($"  Justification: {proposal.Justification}");
            }
            Console.WriteLine();
            Console.WriteLine("TIMELINE:");
            var createdTime = DateTimeOffset.FromUnixTimeSeconds(proposal.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss UTC");
            Console.WriteLine($"  Created: {createdTime}");
            if (proposal.ReviewedAt.HasValue)
            {
                var reviewedTime = DateTimeOffset.FromUnixTimeSeconds(proposal.ReviewedAt.Value).ToString("yyyy-MM-dd HH:mm:ss UTC");
                Console.WriteLine($"  Reviewed: {reviewedTime}");
                Console.WriteLine($"  Reviewed By: {proposal.ReviewedBy}");
                if (!string.IsNullOrEmpty(proposal.RejectionReason))
                {
                    Console.WriteLine($"  Rejection Reason: {proposal.RejectionReason}");
                }
            }
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to view proposal: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentProposalApproveCommand(IHost host, string id)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var proposalService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentPromotionProposalService>();
            logger.LogInformation("Approving agent promotion proposal {ProposalId}", id);

            var proposal = await proposalService.GetProposalAsync(id);
            if (proposal == null)
            {
                Console.WriteLine($"✗ Proposal not found: {id}");
                return 1;
            }

            Console.WriteLine("APPROVING PROMOTION PROPOSAL:");
            Console.WriteLine($"  Agent: {proposal.ProposingAgent}");
            Console.WriteLine($"  Scope: {proposal.FromScope} → {proposal.SuggestedTargetScope}");
            Console.WriteLine($"  Confidence: {proposal.ConfidenceScore:P}");
            Console.WriteLine();

            var result = await proposalService.ApproveProposalAsync(id, "user");

            if (!result)
            {
                Console.WriteLine("✗ Failed to approve proposal. It may already be processed.");
                return 1;
            }

            Console.WriteLine("✓ Proposal approved successfully");
            Console.WriteLine($"  Learning promoted to {proposal.SuggestedTargetScope} scope");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to approve proposal: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentProposalRejectCommand(IHost host, string id, string? reason)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var proposalService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentPromotionProposalService>();
            logger.LogInformation("Rejecting agent promotion proposal {ProposalId}", id);

            var proposal = await proposalService.GetProposalAsync(id);
            if (proposal == null)
            {
                Console.WriteLine($"✗ Proposal not found: {id}");
                return 1;
            }

            Console.WriteLine("REJECTING PROMOTION PROPOSAL:");
            Console.WriteLine($"  Agent: {proposal.ProposingAgent}");
            Console.WriteLine($"  Scope: {proposal.FromScope} → {proposal.SuggestedTargetScope}");
            if (!string.IsNullOrEmpty(reason))
            {
                Console.WriteLine($"  Reason: {reason}");
            }
            Console.WriteLine();

            var result = await proposalService.RejectProposalAsync(id, reason, "user");

            if (!result)
            {
                Console.WriteLine("✗ Failed to reject proposal. It may already be processed.");
                return 1;
            }

            Console.WriteLine("✓ Proposal rejected successfully");
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to reject proposal: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> AgentProposalStatsCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var proposalService = host.Services.GetRequiredService<Daiv3.Orchestration.Interfaces.IAgentPromotionProposalService>();
            logger.LogInformation("Getting agent promotion proposal statistics");

            var stats = await proposalService.GetStatisticsAsync();

            Console.WriteLine("PROMOTION PROPOSAL STATISTICS:");
            Console.WriteLine();
            Console.WriteLine("COUNTS:");
            Console.WriteLine($"  Pending: {stats.PendingCount}");
            Console.WriteLine($"  Approved: {stats.ApprovedCount}");
            Console.WriteLine($"  Rejected: {stats.RejectedCount}");
            Console.WriteLine();

            if (stats.PendingCount > 0)
            {
                Console.WriteLine("PENDING ANALYSIS:");
                Console.WriteLine($"  Average Confidence: {stats.AveragePendingConfidence:P}");
                Console.WriteLine();
            }

            if (stats.ProposalsByAgent.Any())
            {
                Console.WriteLine("PROPOSALS BY AGENT:");
                foreach (var kvp in stats.ProposalsByAgent.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to get statistics: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Lists all promotions in chronological order (KBP-ACC-002).
    /// </summary>
    private static async Task<int> ListPromotionHistoryCommand(IHost host, int limit)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var repository = host.Services.GetRequiredService<PromotionRepository>();

            logger.LogInformation("Listing promotion history (limit: {Limit})", limit);

            Console.WriteLine("PROMOTION HISTORY");
            Console.WriteLine("=================");
            Console.WriteLine();

            var promotions = await repository.GetAllAsync();

            if (promotions.Count == 0)
            {
                Console.WriteLine("No promotions found.");
                return 0;
            }

            var displayedPromotions = promotions.Take(limit).ToList();

            Console.WriteLine($"Showing {displayedPromotions.Count} of {promotions.Count} promotions:");
            Console.WriteLine();

            foreach (var promo in displayedPromotions)
            {
                Console.WriteLine($"Promotion ID: {promo.PromotionId}");
                Console.WriteLine($"  Learning ID: {promo.LearningId}");
                Console.WriteLine($"  Scope Change: {promo.FromScope} → {promo.ToScope}");
                Console.WriteLine($"  Promoted By: {promo.PromotedBy}");
                Console.WriteLine($"  Promoted At: {DateTimeOffset.FromUnixTimeSeconds(promo.PromotedAt):u}");

                if (!string.IsNullOrEmpty(promo.SourceTaskId))
                {
                    Console.WriteLine($"  Source Task: {promo.SourceTaskId}");
                }

                if (!string.IsNullOrEmpty(promo.SourceAgent))
                {
                    Console.WriteLine($"  Source Agent: {promo.SourceAgent}");
                }

                if (!string.IsNullOrEmpty(promo.Notes))
                {
                    Console.WriteLine($"  Notes: {promo.Notes}");
                }

                Console.WriteLine();
            }

            if (promotions.Count > limit)
            {
                Console.WriteLine($"... and {promotions.Count - limit} more promotions.");
                Console.WriteLine($"Use --limit {promotions.Count} to see all promotions.");
            }

            logger.LogInformation("✓ Listed {Count} promotions", displayedPromotions.Count);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list promotion history: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to list promotion history");
            return 1;
        }
    }

    /// <summary>
    /// Views promotion history for a specific learning (KBP-ACC-002).
    /// </summary>
    private static async Task<int> ViewLearningPromotionHistoryCommand(IHost host, string learningId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var repository = host.Services.GetRequiredService<PromotionRepository>();
            var learningService = host.Services.GetRequiredService<LearningStorageService>();

            logger.LogInformation("Viewing promotion history for learning {LearningId}", learningId);

            // Get learning details
            var learning = await learningService.GetLearningAsync(learningId);
            if (learning == null)
            {
                Console.WriteLine($"✗ Learning {learningId} not found.");
                return 1;
            }

            Console.WriteLine("PROMOTION HISTORY FOR LEARNING");
            Console.WriteLine("==============================");
            Console.WriteLine();
            Console.WriteLine($"Learning ID: {learning.LearningId}");
            Console.WriteLine($"Title: {learning.Title}");
            Console.WriteLine($"Current Scope: {learning.Scope}");
            Console.WriteLine($"Confidence: {learning.Confidence:P0}");
            Console.WriteLine();

            var promotions = await repository.GetByLearningIdAsync(learningId);

            if (promotions.Count == 0)
            {
                Console.WriteLine("This learning has never been promoted.");
                return 0;
            }

            Console.WriteLine($"Promotion History ({promotions.Count} promotions):");
            Console.WriteLine();

            foreach (var promo in promotions)
            {
                Console.WriteLine($"{promo.FromScope} → {promo.ToScope}");
                Console.WriteLine($"  Promoted By: {promo.PromotedBy}");
                Console.WriteLine($"  Promoted At: {DateTimeOffset.FromUnixTimeSeconds(promo.PromotedAt):u}");

                if (!string.IsNullOrEmpty(promo.SourceTaskId))
                {
                    Console.WriteLine($"  Source Task: {promo.SourceTaskId}");
                }

                if (!string.IsNullOrEmpty(promo.Notes))
                {
                    Console.WriteLine($"  Notes: {promo.Notes}");
                }

                Console.WriteLine();
            }

            logger.LogInformation("✓ Viewed promotion history for learning {LearningId}", learningId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to view promotion history: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to view learning promotion history");
            return 1;
        }
    }

    /// <summary>
    /// Views promotions from a specific task (KBP-ACC-002).
    /// </summary>
    private static async Task<int> ViewPromotionsByTaskCommand(IHost host, string taskId)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var repository = host.Services.GetRequiredService<PromotionRepository>();

            logger.LogInformation("Viewing promotions from task {TaskId}", taskId);

            Console.WriteLine("PROMOTIONS FROM TASK");
            Console.WriteLine("====================");
            Console.WriteLine($"Task ID: {taskId}");
            Console.WriteLine();

            var promotions = await repository.GetBySourceTaskIdAsync(taskId);

            if (promotions.Count == 0)
            {
                Console.WriteLine("No promotions found from this task.");
                return 0;
            }

            Console.WriteLine($"Found {promotions.Count} promotions:");
            Console.WriteLine();

            foreach (var promo in promotions)
            {
                Console.WriteLine($"Learning ID: {promo.LearningId}");
                Console.WriteLine($"  Scope Change: {promo.FromScope} → {promo.ToScope}");
                Console.WriteLine($"  Promoted By: {promo.PromotedBy}");
                Console.WriteLine($"  Promoted At: {DateTimeOffset.FromUnixTimeSeconds(promo.PromotedAt):u}");

                if (!string.IsNullOrEmpty(promo.Notes))
                {
                    Console.WriteLine($"  Notes: {promo.Notes}");
                }

                Console.WriteLine();
            }

            logger.LogInformation("✓ Found {Count} promotions from task {TaskId}", promotions.Count, taskId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to view promotions by task: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to view promotions by task");
            return 1;
        }
    }

    /// <summary>
    /// Views promotions to a specific scope (KBP-ACC-002).
    /// </summary>
    private static async Task<int> ViewPromotionsByScopeCommand(IHost host, string scope)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var repository = host.Services.GetRequiredService<PromotionRepository>();

            // Normalize scope input (case-insensitive)
            var validScopes = new[] { "Skill", "Agent", "Project", "Domain", "Global" };
            var normalizedScope = validScopes.FirstOrDefault(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase));

            if (normalizedScope == null)
            {
                Console.WriteLine($"✗ Invalid scope '{scope}'. Valid scopes: {string.Join(", ", validScopes)}");
                return 1;
            }

            logger.LogInformation("Viewing promotions to scope {Scope}", normalizedScope);

            Console.WriteLine("PROMOTIONS TO SCOPE");
            Console.WriteLine("===================");
            Console.WriteLine($"Target Scope: {normalizedScope}");
            Console.WriteLine();

            var promotions = await repository.GetByToScopeAsync(normalizedScope);

            if (promotions.Count == 0)
            {
                Console.WriteLine($"No promotions found to {normalizedScope} scope.");
                return 0;
            }

            Console.WriteLine($"Found {promotions.Count} promotions:");
            Console.WriteLine();

            foreach (var promo in promotions)
            {
                Console.WriteLine($"Learning ID: {promo.LearningId}");
                Console.WriteLine($"  From Scope: {promo.FromScope}");
                Console.WriteLine($"  Promoted By: {promo.PromotedBy}");
                Console.WriteLine($"  Promoted At: {DateTimeOffset.FromUnixTimeSeconds(promo.PromotedAt):u}");

                if (!string.IsNullOrEmpty(promo.SourceTaskId))
                {
                    Console.WriteLine($"  Source Task: {promo.SourceTaskId}");
                }

                if (!string.IsNullOrEmpty(promo.Notes))
                {
                    Console.WriteLine($"  Notes: {promo.Notes}");
                }

                Console.WriteLine();
            }

            logger.LogInformation("✓ Found {Count} promotions to scope {Scope}", promotions.Count, normalizedScope);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to view promotions by scope: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to view promotions by scope");
            return 1;
        }
    }

    /// <summary>
    /// Shows statistics about promotion activity (KBP-ACC-002).
    /// </summary>
    private static async Task<int> ShowPromotionStatsCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var repository = host.Services.GetRequiredService<PromotionRepository>();

            logger.LogInformation("Showing promotion statistics");

            Console.WriteLine("PROMOTION STATISTICS");
            Console.WriteLine("====================");
            Console.WriteLine();

            var allPromotions = await repository.GetAllAsync();

            if (allPromotions.Count == 0)
            {
                Console.WriteLine("No promotions recorded yet.");
                return 0;
            }

            Console.WriteLine($"Total Promotions: {allPromotions.Count}");
            Console.WriteLine();

            // Group by target scope
            var byScopeGroups = allPromotions.GroupBy(p => p.ToScope).OrderBy(g => g.Key);
            Console.WriteLine("PROMOTIONS BY TARGET SCOPE:");
            foreach (var group in byScopeGroups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // Group by source scope
            var fromScopeGroups = allPromotions.GroupBy(p => p.FromScope).OrderBy(g => g.Key);
            Console.WriteLine("PROMOTIONS BY SOURCE SCOPE:");
            foreach (var group in fromScopeGroups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // Group by promoter
            var byPromoterGroups = allPromotions.GroupBy(p => p.PromotedBy)
                .OrderByDescending(g => g.Count())
                .Take(10);
            Console.WriteLine("TOP PROMOTERS:");
            foreach (var group in byPromoterGroups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
            Console.WriteLine();

            // Time-based statistics
            var oldestPromotion = allPromotions.OrderBy(p => p.PromotedAt).FirstOrDefault();
            var newestPromotion = allPromotions.OrderByDescending(p => p.PromotedAt).FirstOrDefault();

            if (oldestPromotion != null && newestPromotion != null)
            {
                Console.WriteLine("TIME RANGE:");
                Console.WriteLine($"  First Promotion: {DateTimeOffset.FromUnixTimeSeconds(oldestPromotion.PromotedAt):u}");
                Console.WriteLine($"  Latest Promotion: {DateTimeOffset.FromUnixTimeSeconds(newestPromotion.PromotedAt):u}");
                Console.WriteLine();
            }

            // Recent activity
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var last24Hours = now - (24 * 60 * 60);
            var last7Days = now - (7 * 24 * 60 * 60);

            var promotionsLast24h = allPromotions.Count(p => p.PromotedAt >= last24Hours);
            var promotionsLast7d = allPromotions.Count(p => p.PromotedAt >= last7Days);

            Console.WriteLine("RECENT ACTIVITY:");
            Console.WriteLine($"  Last 24 hours: {promotionsLast24h}");
            Console.WriteLine($"  Last 7 days: {promotionsLast7d}");
            Console.WriteLine();

            logger.LogInformation("✓ Displayed promotion statistics");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show promotion statistics: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to show promotion statistics");
            return 1;
        }
    }

    // Knowledge Indexing Commands (CT-REQ-005)

    private static async Task<int> KnowledgeIndexStatusCommand(IHost host)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var indexingService = host.Services.GetRequiredService<IIndexingStatusService>();
            logger.LogInformation("Displaying indexing status");

            var stats = await indexingService.GetIndexingStatisticsAsync();

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("                 INDEXING STATUS (CT-REQ-005)              ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("OVERALL STATISTICS:");
            Console.WriteLine($"  Total Indexed: {stats.TotalIndexed}");
            Console.WriteLine($"  Not Indexed: {stats.TotalNotIndexed}");
            Console.WriteLine($"  Errors: {stats.TotalErrors}");
            Console.WriteLine($"  In Progress: {stats.TotalInProgress}");
            Console.WriteLine($"  Warnings: {stats.TotalWarnings}");
            Console.WriteLine($"  Storage Used: {FormatBytes(stats.TotalEmbeddingStorageBytes)}");
            Console.WriteLine();

            Console.WriteLine("WATCHER STATUS:");
            Console.WriteLine($"  Active: {stats.IsWatcherActive}");
            if (stats.LastScanTime.HasValue)
            {
                var lastScan = DateTimeOffset.FromUnixTimeSeconds(stats.LastScanTime.Value);
                Console.WriteLine($"  Last Scan: {lastScan.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Console.WriteLine("  Last Scan: Never");
            }
            Console.WriteLine();

            Console.WriteLine("ORCHESTRATION:");
            Console.WriteLine($"  Files Processed: {stats.OrchestrationStats.FilesProcessed}");
            Console.WriteLine($"  Files Deleted: {stats.OrchestrationStats.FilesDeleted}");
            Console.WriteLine($"  Processing Errors: {stats.OrchestrationStats.ProcessingErrors}");
            Console.WriteLine($"  Deletion Errors: {stats.OrchestrationStats.DeletionErrors}");
            Console.WriteLine();

            logger.LogInformation("✓ Displayed indexing status");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show indexing status: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to show indexing status");
            return 1;
        }
    }

    private static async Task<int> KnowledgeIndexListCommand(
        IHost host,
        string? filter,
        string? format,
        string? search)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var indexingService = host.Services.GetRequiredService<IIndexingStatusService>();
            logger.LogInformation("Listing indexed files");

            IReadOnlyList<FileIndexInfo> files;

            // Apply filters
            if (filter != null)
            {
                var status = filter.ToLowerInvariant() switch
                {
                    "indexed" => FileIndexingStatus.Indexed,
                    "error" => FileIndexingStatus.Error,
                    "pending" => FileIndexingStatus.NotIndexed,
                    "warning" => FileIndexingStatus.Warning,
                    "inprogress" => FileIndexingStatus.InProgress,
                    _ => FileIndexingStatus.Indexed
                };
                files = await indexingService.GetFilesByStatusAsync(status);
            }
            else if (format != null)
            {
                files = await indexingService.GetFilesByFormatAsync(format);
            }
            else if (search != null)
            {
                files = await indexingService.SearchFilesAsync(search);
            }
            else
            {
                files = await indexingService.GetAllFilesAsync();
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("              INDEXED FILES (CT-REQ-005)                    ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Total files: {files.Count}");
            Console.WriteLine();

            if (files.Count == 0)
            {
                Console.WriteLine("No files found matching criteria.");
                return 0;
            }

            foreach (var file in files.Take(50)) // Limit to first 50
            {
                var statusIcon = file.Status switch
                {
                    FileIndexingStatus.Indexed => "✓",
                    FileIndexingStatus.Error => "✗",
                    FileIndexingStatus.Warning => "⚠",
                    FileIndexingStatus.InProgress => "⟳",
                    FileIndexingStatus.NotIndexed => "○",
                    _ => "?"
                };

                Console.WriteLine($"{statusIcon} {file.FilePath}");
                Console.WriteLine($"  Status: {file.Status} | Format: {file.Format ?? "unknown"} | Size: {FormatBytes(file.SizeBytes ?? 0)}");
                
                if (file.ChunkCount.HasValue)
                {
                    Console.WriteLine($"  Chunks: {file.ChunkCount} | Embedding: {file.EmbeddingDimension}D");
                }

                if (!string.IsNullOrEmpty(file.ErrorMessage))
                {
                    Console.WriteLine($"  Error: {file.ErrorMessage}");
                }

                Console.WriteLine();
            }

            if (files.Count > 50)
            {
                Console.WriteLine($"... and {files.Count - 50} more files.");
                Console.WriteLine("Use --search or --filter to narrow results.");
            }

            logger.LogInformation("✓ Listed {Count} indexed files", files.Count);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to list indexed files: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to list indexed files");
            return 1;
        }
    }

    private static async Task<int> KnowledgeIndexDetailsCommand(IHost host, string filePath)
    {
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var indexingService = host.Services.GetRequiredService<IIndexingStatusService>();
            logger.LogInformation("Showing file details for: {FilePath}", filePath);

            var file = await indexingService.GetFileDetailsAsync(filePath);

            if (file == null)
            {
                Console.WriteLine($"✗ File not found: {filePath}");
                return 1;
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("              FILE DETAILS (CT-REQ-005)                     ");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Path: {file.FilePath}");
            Console.WriteLine($"Status: {file.Status}");
            Console.WriteLine($"Format: {file.Format ?? "unknown"}");
            Console.WriteLine($"Size: {FormatBytes(file.SizeBytes ?? 0)}");
            Console.WriteLine();

            if (file.DocId != null)
            {
                Console.WriteLine($"Document ID: {file.DocId}");
            }

            if (file.IndexedAt.HasValue)
            {
                var indexedAt = DateTimeOffset.FromUnixTimeSeconds(file.IndexedAt.Value);
                Console.WriteLine($"Indexed At: {indexedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }

            if (file.LastModified.HasValue)
            {
                var lastModified = DateTimeOffset.FromUnixTimeSeconds(file.LastModified.Value);
                Console.WriteLine($"Last Modified: {lastModified.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }

            Console.WriteLine();

            if (file.ChunkCount.HasValue)
            {
                Console.WriteLine($"Chunks: {file.ChunkCount}");
                Console.WriteLine($"Embedding Dimension: {file.EmbeddingDimension}D");
                Console.WriteLine();
            }

            if (!string.IsNullOrEmpty(file.TopicSummary))
            {
                Console.WriteLine("Topic Summary:");
                Console.WriteLine($"  {file.TopicSummary}");
                Console.WriteLine();
            }

            if (!string.IsNullOrEmpty(file.ErrorMessage))
            {
                Console.WriteLine($"Error: {file.ErrorMessage}");
                Console.WriteLine();
            }

            logger.LogInformation("✓ Displayed file details");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to show file details: {ex.Message}");
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to show file details");
            return 1;
        }
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB)
            return $"{bytes / (double)GB:F2} GB";
        if (bytes >= MB)
            return $"{bytes / (double)MB:F2} MB";
        if (bytes >= KB)
            return $"{bytes / (double)KB:F2} KB";
        return $"{bytes} bytes";
    }

}
