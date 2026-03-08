namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for local-first routing behavior.
/// </summary>
/// <remarks>
/// Implements ES-REQ-001: "The system SHALL process user requests using local models by default."
/// Controls how the system makes routing decisions between local and online model execution.
/// </remarks>
public class LocalFirstRouteOptions
{
    /// <summary>
    /// When true, the system preferentially routes requests to local models by default.
    /// </summary>
    /// <remarks>
    /// Default: true (local-first principle)
    /// When false, requests without explicit model preference may be routed to online providers.
    /// </remarks>
    public bool PreferLocalModelsDefault { get; set; } = true;

    /// <summary>
    /// List of task types that MUST use local models (never route online).
    /// </summary>
    /// <remarks>
    /// Examples: "PrivacySensitive", "OfflineOnly", "Search" (for knowledge base search)
    /// These task types will always use local models regardless of OnlinePreference setting.
    /// </remarks>
    public List<string> LocalOnlyTaskTypes { get; set; } = new()
    {
        "Search",           // Knowledge base search always local
        "PrivacySensitive", // Never send private data online
        "OfflineOnly"       // Explicitly offline tasks
    };

    /// <summary>
    /// List of task types that require online models (never route to local).
    /// </summary>
    /// <remarks>
    /// Examples: "OnlineSearch", "OnlineTranslation", "OnlineAnalysis"
    /// These task types will always use online models regardless of PreferLocalModelsDefault.
    /// </remarks>
    public List<string> OnlineOnlyTaskTypes { get; set; } = new();

    /// <summary>
    /// List of local model IDs that support the specific task types.
    /// </summary>
    /// <remarks>
    /// Used to verify if a local model is available for the requested task.
    /// If empty, any local model is considered capable.
    /// </remarks>
    public List<string> AvailableLocalModels { get; set; } = new()
    {
        "phi-3-mini",
        "phi-3", 
        "phi-4",
        "mistral-7b",
        "llama-2-7b"
    };

    /// <summary>
    /// When true, route to online provider if the local model queue has excessive backlog.
    /// </summary>
    /// <remarks>
    /// Default: false (always use local unless unavailable)
    /// When true, if the local queue has more than QueueBacklogThreshold pending requests,
    /// new requests may be routed to online providers to reduce latency.
    /// </remarks>
    public bool RouteOnlineOnQueueBacklog { get; set; } = false;

    /// <summary>
    /// Maximum queue depth before considering online routing due to backlog (when RouteOnlineOnQueueBacklog = true).
    /// </summary>
    /// <remarks>
    /// Default: 10
    /// If local queue has >= this many pending requests, new requests may go online instead.
    /// </remarks>
    public int QueueBacklogThreshold { get; set; } = 10;

    /// <summary>
    /// When true, enforce local-first by failing the request if local model is unavailable and online fallback is disabled.
    /// </summary>
    /// <remarks>
    /// Default: false (allow graceful fallback to online)
    /// When true, requests that need local but local is unavailable will error instead of using online.
    /// </remarks>
    public bool FailIfLocalUnavailable { get; set; } = false;

    /// <summary>
    /// Logging level for local-first routing decisions.
    /// </summary>
    /// <remarks>
    /// Options: "Debug", "Information", "Warning"
    /// Controls verbosity of logging about why requests are routed to local or online.
    /// </remarks>
    public string RoutingDecisionLogLevel { get; set; } = "Information";
}
