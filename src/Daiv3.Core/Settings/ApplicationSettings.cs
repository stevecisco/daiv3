namespace Daiv3.Core.Settings;

/// <summary>
/// Defines all application settings keys, categories, and default values.
/// Implements CT-REQ-001: The system SHALL store all settings locally.
/// </summary>
public static class ApplicationSettings
{
    // Setting Categories (matches CT-DATA-001 category enum)
    public static class Categories
    {
        public const string General = "general";
        public const string Paths = "paths";
        public const string Models = "models";
        public const string Providers = "providers";
        public const string Hardware = "hardware";
        public const string UI = "ui";
        public const string Knowledge = "knowledge";
    }

    // Paths Settings
    public static class Paths
    {
        public const string DataDirectory = "data_directory";
        public const string WatchedDirectories = "watched_directories";
        public const string IncludePatterns = "include_patterns";
        public const string ExcludePatterns = "exclude_patterns";
        public const string MaxSubDirectoryDepth = "max_subdirectory_depth";
        public const string FileTypeFilters = "file_type_filters";
        public const string KnowledgeBackPropagationPath = "knowledge_backprop_path";
    }

    // Model Settings
    public static class Models
    {
        public const string FoundryLocalDefaultModel = "foundry_local_default_model";
        public const string FoundryLocalChatModel = "foundry_local_chat_model";
        public const string FoundryLocalCodeModel = "foundry_local_code_model";
        public const string FoundryLocalReasoningModel = "foundry_local_reasoning_model";
        public const string ModelToTaskMappings = "model_to_task_mappings";
        public const string EmbeddingModel = "embedding_model";
        public const string EmbeddingDimensions = "embedding_dimensions";
    }

    // Provider Settings
    public static class Providers
    {
        // Online access control
        public const string OnlineAccessMode = "online_access_mode"; // never, ask, auto_within_budget, per_task
        public const string OnlineProvidersEnabled = "online_providers_enabled"; // JSON array
        public const string ForceOfflineMode = "force_offline_mode"; // bool - override to disable all online access (testing, simulation, low connectivity)

        // Token budgets
        public const string DailyTokenBudget = "daily_token_budget";
        public const string MonthlyTokenBudget = "monthly_token_budget";
        public const string TokenBudgetAlertThreshold = "token_budget_alert_threshold"; // percentage
        public const string TokenBudgetMode = "token_budget_mode"; // hard_stop, user_confirm

        // Provider-specific settings (JSON objects with provider configurations)
        public const string OpenAIApiKey = "openai_api_key";
        public const string OpenAIBaseUrl = "openai_base_url";
        public const string AnthropicApiKey = "anthropic_api_key";
        public const string AnthropicBaseUrl = "anthropic_base_url";
        public const string AzureOpenAIApiKey = "azure_openai_api_key";
        public const string AzureOpenAIEndpoint = "azure_openai_endpoint";
        public const string AzureOpenAIDeploymentName = "azure_openai_deployment_name";
    }

    // Hardware Settings
    public static class Hardware
    {
        public const string PreferredExecutionProvider = "preferred_execution_provider"; // npu, gpu, cpu, auto
        public const string ForceDeviceType = "force_device_type"; // null, npu, gpu, cpu
        public const string DisableNpu = "disable_npu";
        public const string DisableGpu = "disable_gpu";
        public const string ForceCpuOnly = "force_cpu_only";
        public const string MaxConcurrentModelRequests = "max_concurrent_model_requests";
    }

    // UI Settings
    public static class UI
    {
        public const string Theme = "theme"; // light, dark, system
        public const string DashboardRefreshInterval = "dashboard_refresh_interval_ms";
        public const string ShowNotifications = "show_notifications";
        public const string MinimizeToTray = "minimize_to_tray";
        public const string AutoStartDashboard = "auto_start_dashboard";
        public const string LogLevel = "log_level"; // verbose, debug, information, warning, error
    }

    // Knowledge Settings
    public static class Knowledge
    {
        public const string AutoIndexOnStartup = "auto_index_on_startup";
        public const string IndexScanInterval = "index_scan_interval_minutes";
        public const string ChunkSize = "chunk_size_tokens";
        public const string ChunkOverlap = "chunk_overlap_tokens";
        public const string MaxDocumentsPerBatch = "max_documents_per_batch";
        public const string CategoryToPathMappings = "category_to_path_mappings"; // JSON object
        public const string EnableKnowledgeGraph = "enable_knowledge_graph";
    }

    // General Settings
    public static class General
    {
        public const string FirstRunCompleted = "first_run_completed";
        public const string LastStartupTime = "last_startup_time";
        public const string EnableAgents = "enable_agents";
        public const string EnableSkills = "enable_skills";
        public const string AgentIterationLimit = "agent_iteration_limit";
        public const string AgentTokenBudget = "agent_token_budget";
        public const string SkillMarketplaceUrls = "skill_marketplace_urls"; // JSON array
        public const string EnableScheduling = "enable_scheduling";
        public const string SchedulerCheckInterval = "scheduler_check_interval_seconds";
    }

    /// <summary>
    /// Default values for all settings.
    /// </summary>
    public static class Defaults
    {
        // Paths
        public static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Daiv3");
        public const string WatchedDirectories = "[]"; // Empty JSON array
        public const string IncludePatterns = "[\"*.txt\",\"*.md\",\"*.pdf\",\"*.docx\",\"*.html\"]";
        public const string ExcludePatterns = "[\"node_modules\",\".git\",\"bin\",\"obj\",\"*.tmp\"]";
        public const int MaxSubDirectoryDepth = 5;
        public const string FileTypeFilters = "[\"txt\",\"md\",\"pdf\",\"docx\",\"html\"]";
        public static readonly string KnowledgeBackPropagationPath = Path.Combine(DataDirectory, "backprop");

        // Models
        public const string FoundryLocalDefaultModel = "phi-3-mini";
        public const string FoundryLocalChatModel = "phi-3-mini";
        public const string FoundryLocalCodeModel = "phi-3-mini";
        public const string FoundryLocalReasoningModel = "phi-3-mini";
        public const string ModelToTaskMappings = "{}"; // Empty JSON object
        public const string EmbeddingModel = "nomic-embed-text";
        public const int EmbeddingDimensions = 768;

        // Providers
        public const string OnlineAccessMode = "ask"; // never, ask, auto_within_budget, per_task
        public const string OnlineProvidersEnabled = "[]"; // Empty JSON array
        public const bool ForceOfflineMode = false;
        public const int DailyTokenBudget = 50000;
        public const int MonthlyTokenBudget = 1000000;
        public const int TokenBudgetAlertThreshold = 80; // 80%
        public const string TokenBudgetMode = "user_confirm"; // hard_stop, user_confirm

        // Provider-specific (sensitive, no defaults)
        public const string OpenAIApiKey = "";
        public const string OpenAIBaseUrl = "https://api.openai.com/v1";
        public const string AnthropicApiKey = "";
        public const string AnthropicBaseUrl = "https://api.anthropic.com";
        public const string AzureOpenAIApiKey = "";
        public const string AzureOpenAIEndpoint = "";
        public const string AzureOpenAIDeploymentName = "";

        // Hardware
        public const string PreferredExecutionProvider = "auto"; // npu, gpu, cpu, auto
        public const string ForceDeviceType = ""; // Empty means auto-detect
        public const bool DisableNpu = false;
        public const bool DisableGpu = false;
        public const bool ForceCpuOnly = false;
        public const int MaxConcurrentModelRequests = 4;

        // UI
        public const string Theme = "system"; // light, dark, system
        public const int DashboardRefreshInterval = 1000; // 1 second
        public const bool ShowNotifications = true;
        public const bool MinimizeToTray = false;
        public const bool AutoStartDashboard = true;
        public const string LogLevel = "information";

        // Knowledge
        public const bool AutoIndexOnStartup = false;
        public const int IndexScanInterval = 60; // 60 minutes
        public const int ChunkSize = 400; // tokens
        public const int ChunkOverlap = 50; // tokens
        public const int MaxDocumentsPerBatch = 100;
        public const string CategoryToPathMappings = "{}"; // Empty JSON object
        public const bool EnableKnowledgeGraph = false;

        // General
        public const bool FirstRunCompleted = false;
        public const string LastStartupTime = ""; // Empty, will be set on first run
        public const bool EnableAgents = true;
        public const bool EnableSkills = true;
        public const int AgentIterationLimit = 10;
        public const int AgentTokenBudget = 10000;
        public const string SkillMarketplaceUrls = "[]"; // Empty JSON array
        public const bool EnableScheduling = true;
        public const int SchedulerCheckInterval = 60; // 60 seconds
    }

    /// <summary>
    /// Setting descriptions for documentation and UI display.
    /// </summary>
    public static class Descriptions
    {
        // Paths
        public const string DataDirectory = "Root directory for application data, databases, and indexes";
        public const string WatchedDirectories = "List of directories to monitor and index for knowledge base";
        public const string IncludePatterns = "File patterns to include during indexing (glob format)";
        public const string ExcludePatterns = "File patterns to exclude during indexing (glob format)";
        public const string MaxSubDirectoryDepth = "Maximum depth to traverse when scanning directories";
        public const string FileTypeFilters = "File extensions to index";
        public const string KnowledgeBackPropagationPath = "Directory for knowledge back-propagation outputs";

        // Models
        public const string FoundryLocalDefaultModel = "Default model for general tasks";
        public const string FoundryLocalChatModel = "Model for chat/conversation tasks";
        public const string FoundryLocalCodeModel = "Model for code generation and analysis";
        public const string FoundryLocalReasoningModel = "Model for complex reasoning tasks";
        public const string ModelToTaskMappings = "JSON mapping of task types to preferred models";
        public const string EmbeddingModel = "ONNX model for generating text embeddings";
        public const string EmbeddingDimensions = "Embedding vector dimensions";

        // Providers
        public const string OnlineAccessMode = "When to allow online API calls (never/ask/auto_within_budget/per_task)";
        public const string OnlineProvidersEnabled = "List of enabled online AI providers";
        public const string ForceOfflineMode = "Force the system into offline-only mode regardless of network connectivity";
        public const string DailyTokenBudget = "Maximum tokens per day across all online providers";
        public const string MonthlyTokenBudget = "Maximum tokens per month across all online providers";
        public const string TokenBudgetAlertThreshold = "Percentage threshold for budget alerts (0-100)";
        public const string TokenBudgetMode = "Action when budget exceeded (hard_stop/user_confirm)";
        public const string OpenAIApiKey = "OpenAI API key (sensitive)";
        public const string OpenAIBaseUrl = "OpenAI API base URL";
        public const string AnthropicApiKey = "Anthropic API key (sensitive)";
        public const string AnthropicBaseUrl = "Anthropic API base URL";
        public const string AzureOpenAIApiKey = "Azure OpenAI API key (sensitive)";
        public const string AzureOpenAIEndpoint = "Azure OpenAI endpoint URL";
        public const string AzureOpenAIDeploymentName = "Azure OpenAI deployment name";

        // Hardware
        public const string PreferredExecutionProvider = "Preferred hardware for ML operations (npu/gpu/cpu/auto)";
        public const string ForceDeviceType = "Force specific device type (empty for auto-detect)";
        public const string DisableNpu = "Disable NPU even if available";
        public const string DisableGpu = "Disable GPU even if available";
        public const string ForceCpuOnly = "Force CPU-only execution";
        public const string MaxConcurrentModelRequests = "Maximum concurrent model inference requests";

        // UI
        public const string Theme = "UI theme (light/dark/system)";
        public const string DashboardRefreshInterval = "Dashboard refresh interval in milliseconds";
        public const string ShowNotifications = "Enable system notifications";
        public const string MinimizeToTray = "Minimize to system tray instead of closing";
        public const string AutoStartDashboard = "Automatically open dashboard on startup";
        public const string LogLevel = "Logging verbosity level";

        // Knowledge
        public const string AutoIndexOnStartup = "Automatically scan and index watched directories on startup";
        public const string IndexScanInterval = "Minutes between automatic directory scans";
        public const string ChunkSize = "Text chunk size in tokens for indexing";
        public const string ChunkOverlap = "Token overlap between consecutive chunks";
        public const string MaxDocumentsPerBatch = "Maximum documents to process in one batch";
        public const string CategoryToPathMappings = "JSON mapping of knowledge categories to filesystem paths";
        public const string EnableKnowledgeGraph = "Enable knowledge graph features (experimental)";

        // General
        public const string FirstRunCompleted = "Has initial setup been completed";
        public const string LastStartupTime = "Last application startup timestamp";
        public const string EnableAgents = "Enable autonomous agents";
        public const string EnableSkills = "Enable skills system";
        public const string AgentIterationLimit = "Maximum iterations for agent loops";
        public const string AgentTokenBudget = "Token budget per agent session";
        public const string SkillMarketplaceUrls = "URLs for skill marketplace repositories";
        public const string EnableScheduling = "Enable task scheduling system";
        public const string SchedulerCheckInterval = "Seconds between scheduler checks";
    }

    /// <summary>
    /// Identifies sensitive settings that should never be logged or displayed in plain text.
    /// </summary>
    public static readonly HashSet<string> SensitiveKeys = new()
    {
        Providers.OpenAIApiKey,
        Providers.AnthropicApiKey,
        Providers.AzureOpenAIApiKey
    };

    /// <summary>
    /// Gets the category for a given setting key.
    /// </summary>
    public static string GetCategory(string settingKey)
    {
        return settingKey switch
        {
            // Paths
            var k when k == Paths.DataDirectory => Categories.Paths,
            var k when k == Paths.WatchedDirectories => Categories.Paths,
            var k when k == Paths.IncludePatterns => Categories.Paths,
            var k when k == Paths.ExcludePatterns => Categories.Paths,
            var k when k == Paths.MaxSubDirectoryDepth => Categories.Paths,
            var k when k == Paths.FileTypeFilters => Categories.Paths,
            var k when k == Paths.KnowledgeBackPropagationPath => Categories.Paths,

            // Models
            var k when k == Models.FoundryLocalDefaultModel => Categories.Models,
            var k when k == Models.FoundryLocalChatModel => Categories.Models,
            var k when k == Models.FoundryLocalCodeModel => Categories.Models,
            var k when k == Models.FoundryLocalReasoningModel => Categories.Models,
            var k when k == Models.ModelToTaskMappings => Categories.Models,
            var k when k == Models.EmbeddingModel => Categories.Models,
            var k when k == Models.EmbeddingDimensions => Categories.Models,

            // Providers
            var k when k == Providers.OnlineAccessMode => Categories.Providers,
            var k when k == Providers.OnlineProvidersEnabled => Categories.Providers,
            var k when k == Providers.ForceOfflineMode => Categories.Providers,
            var k when k == Providers.DailyTokenBudget => Categories.Providers,
            var k when k == Providers.MonthlyTokenBudget => Categories.Providers,
            var k when k == Providers.TokenBudgetAlertThreshold => Categories.Providers,
            var k when k == Providers.TokenBudgetMode => Categories.Providers,
            var k when k == Providers.OpenAIApiKey => Categories.Providers,
            var k when k == Providers.OpenAIBaseUrl => Categories.Providers,
            var k when k == Providers.AnthropicApiKey => Categories.Providers,
            var k when k == Providers.AnthropicBaseUrl => Categories.Providers,
            var k when k == Providers.AzureOpenAIApiKey => Categories.Providers,
            var k when k == Providers.AzureOpenAIEndpoint => Categories.Providers,
            var k when k == Providers.AzureOpenAIDeploymentName => Categories.Providers,

            // Hardware
            var k when k == Hardware.PreferredExecutionProvider => Categories.Hardware,
            var k when k == Hardware.ForceDeviceType => Categories.Hardware,
            var k when k == Hardware.DisableNpu => Categories.Hardware,
            var k when k == Hardware.DisableGpu => Categories.Hardware,
            var k when k == Hardware.ForceCpuOnly => Categories.Hardware,
            var k when k == Hardware.MaxConcurrentModelRequests => Categories.Hardware,

            // UI
            var k when k == UI.Theme => Categories.UI,
            var k when k == UI.DashboardRefreshInterval => Categories.UI,
            var k when k == UI.ShowNotifications => Categories.UI,
            var k when k == UI.MinimizeToTray => Categories.UI,
            var k when k == UI.AutoStartDashboard => Categories.UI,
            var k when k == UI.LogLevel => Categories.UI,

            // Knowledge
            var k when k == Knowledge.AutoIndexOnStartup => Categories.Knowledge,
            var k when k == Knowledge.IndexScanInterval => Categories.Knowledge,
            var k when k == Knowledge.ChunkSize => Categories.Knowledge,
            var k when k == Knowledge.ChunkOverlap => Categories.Knowledge,
            var k when k == Knowledge.MaxDocumentsPerBatch => Categories.Knowledge,
            var k when k == Knowledge.CategoryToPathMappings => Categories.Knowledge,
            var k when k == Knowledge.EnableKnowledgeGraph => Categories.Knowledge,

            // General
            var k when k == General.FirstRunCompleted => Categories.General,
            var k when k == General.LastStartupTime => Categories.General,
            var k when k == General.EnableAgents => Categories.General,
            var k when k == General.EnableSkills => Categories.General,
            var k when k == General.AgentIterationLimit => Categories.General,
            var k when k == General.AgentTokenBudget => Categories.General,
            var k when k == General.SkillMarketplaceUrls => Categories.General,
            var k when k == General.EnableScheduling => Categories.General,
            var k when k == General.SchedulerCheckInterval => Categories.General,

            // Default
            _ => Categories.General
        };
    }

    /// <summary>
    /// Gets the description for a given setting key.
    /// </summary>
    public static string GetDescription(string settingKey)
    {
        return settingKey switch
        {
            // Paths
            var k when k == Paths.DataDirectory => Descriptions.DataDirectory,
            var k when k == Paths.WatchedDirectories => Descriptions.WatchedDirectories,
            var k when k == Paths.IncludePatterns => Descriptions.IncludePatterns,
            var k when k == Paths.ExcludePatterns => Descriptions.ExcludePatterns,
            var k when k == Paths.MaxSubDirectoryDepth => Descriptions.MaxSubDirectoryDepth,
            var k when k == Paths.FileTypeFilters => Descriptions.FileTypeFilters,
            var k when k == Paths.KnowledgeBackPropagationPath => Descriptions.KnowledgeBackPropagationPath,

            // Models
            var k when k == Models.FoundryLocalDefaultModel => Descriptions.FoundryLocalDefaultModel,
            var k when k == Models.FoundryLocalChatModel => Descriptions.FoundryLocalChatModel,
            var k when k == Models.FoundryLocalCodeModel => Descriptions.FoundryLocalCodeModel,
            var k when k == Models.FoundryLocalReasoningModel => Descriptions.FoundryLocalReasoningModel,
            var k when k == Models.ModelToTaskMappings => Descriptions.ModelToTaskMappings,
            var k when k == Models.EmbeddingModel => Descriptions.EmbeddingModel,
            var k when k == Models.EmbeddingDimensions => Descriptions.EmbeddingDimensions,

            // Providers
            var k when k == Providers.OnlineAccessMode => Descriptions.OnlineAccessMode,
            var k when k == Providers.OnlineProvidersEnabled => Descriptions.OnlineProvidersEnabled,
            var k when k == Providers.ForceOfflineMode => Descriptions.ForceOfflineMode,
            var k when k == Providers.DailyTokenBudget => Descriptions.DailyTokenBudget,
            var k when k == Providers.MonthlyTokenBudget => Descriptions.MonthlyTokenBudget,
            var k when k == Providers.TokenBudgetAlertThreshold => Descriptions.TokenBudgetAlertThreshold,
            var k when k == Providers.TokenBudgetMode => Descriptions.TokenBudgetMode,
            var k when k == Providers.OpenAIApiKey => Descriptions.OpenAIApiKey,
            var k when k == Providers.OpenAIBaseUrl => Descriptions.OpenAIBaseUrl,
            var k when k == Providers.AnthropicApiKey => Descriptions.AnthropicApiKey,
            var k when k == Providers.AnthropicBaseUrl => Descriptions.AnthropicBaseUrl,
            var k when k == Providers.AzureOpenAIApiKey => Descriptions.AzureOpenAIApiKey,
            var k when k == Providers.AzureOpenAIEndpoint => Descriptions.AzureOpenAIEndpoint,
            var k when k == Providers.AzureOpenAIDeploymentName => Descriptions.AzureOpenAIDeploymentName,

            // Hardware
            var k when k == Hardware.PreferredExecutionProvider => Descriptions.PreferredExecutionProvider,
            var k when k == Hardware.ForceDeviceType => Descriptions.ForceDeviceType,
            var k when k == Hardware.DisableNpu => Descriptions.DisableNpu,
            var k when k == Hardware.DisableGpu => Descriptions.DisableGpu,
            var k when k == Hardware.ForceCpuOnly => Descriptions.ForceCpuOnly,
            var k when k == Hardware.MaxConcurrentModelRequests => Descriptions.MaxConcurrentModelRequests,

            // UI
            var k when k == UI.Theme => Descriptions.Theme,
            var k when k == UI.DashboardRefreshInterval => Descriptions.DashboardRefreshInterval,
            var k when k == UI.ShowNotifications => Descriptions.ShowNotifications,
            var k when k == UI.MinimizeToTray => Descriptions.MinimizeToTray,
            var k when k == UI.AutoStartDashboard => Descriptions.AutoStartDashboard,
            var k when k == UI.LogLevel => Descriptions.LogLevel,

            // Knowledge
            var k when k == Knowledge.AutoIndexOnStartup => Descriptions.AutoIndexOnStartup,
            var k when k == Knowledge.IndexScanInterval => Descriptions.IndexScanInterval,
            var k when k == Knowledge.ChunkSize => Descriptions.ChunkSize,
            var k when k == Knowledge.ChunkOverlap => Descriptions.ChunkOverlap,
            var k when k == Knowledge.MaxDocumentsPerBatch => Descriptions.MaxDocumentsPerBatch,
            var k when k == Knowledge.CategoryToPathMappings => Descriptions.CategoryToPathMappings,
            var k when k == Knowledge.EnableKnowledgeGraph => Descriptions.EnableKnowledgeGraph,

            // General
            var k when k == General.FirstRunCompleted => Descriptions.FirstRunCompleted,
            var k when k == General.LastStartupTime => Descriptions.LastStartupTime,
            var k when k == General.EnableAgents => Descriptions.EnableAgents,
            var k when k == General.EnableSkills => Descriptions.EnableSkills,
            var k when k == General.AgentIterationLimit => Descriptions.AgentIterationLimit,
            var k when k == General.AgentTokenBudget => Descriptions.AgentTokenBudget,
            var k when k == General.SkillMarketplaceUrls => Descriptions.SkillMarketplaceUrls,
            var k when k == General.EnableScheduling => Descriptions.EnableScheduling,
            var k when k == General.SchedulerCheckInterval => Descriptions.SchedulerCheckInterval,

            // Default
            _ => $"Setting: {settingKey}"
        };
    }

    /// <summary>
    /// Determines if a setting key contains sensitive data.
    /// </summary>
    public static bool IsSensitive(string settingKey)
    {
        return SensitiveKeys.Contains(settingKey);
    }
}
