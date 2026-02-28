namespace Daiv3.Orchestration.Messaging;

/// <summary>
/// Configuration options for the message broker.
/// </summary>
public class MessageBrokerOptions
{
    /// <summary>
    /// Gets or sets the storage backend type for message persistence.
    /// Supported values: "FileSystem" (default), "AzureBlob" (future).
    /// </summary>
    public string StorageBackend { get; set; } = "FileSystem";

    /// <summary>
    /// Gets or sets the number of days to retain completed messages before archival.
    /// Default: 7 days.
    /// </summary>
    public int RetentionDaysCompleted { get; set; } = 7;

    /// <summary>
    /// Gets or sets the number of days to retain failed messages before archival.
    /// Default: 30 days (longer retention for debugging).
    /// </summary>
    public int RetentionDaysFailed { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum message size in bytes.
    /// Default: 5 MB.
    /// </summary>
    public long MaxMessageSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Gets or sets the options for the file system message store backend.
    /// </summary>
    public FileSystemMessageStoreOptions FileSystemOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for the Azure Blob message store backend.
    /// </summary>
    public AzureBlobMessageStoreOptions AzureBlobOptions { get; set; } = new();

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StorageBackend))
            throw new ArgumentException("StorageBackend must not be empty", nameof(StorageBackend));

        if (RetentionDaysCompleted < 1)
            throw new ArgumentException("RetentionDaysCompleted must be >= 1", nameof(RetentionDaysCompleted));

        if (RetentionDaysFailed < 1)
            throw new ArgumentException("RetentionDaysFailed must be >= 1", nameof(RetentionDaysFailed));

        if (MaxMessageSizeBytes < 1024) // At least 1 KB
            throw new ArgumentException("MaxMessageSizeBytes must be >= 1024", nameof(MaxMessageSizeBytes));

        if (StorageBackend.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
            FileSystemOptions.Validate();
        else if (StorageBackend.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
            AzureBlobOptions.Validate();
        else
            throw new ArgumentException(
                $"Unknown StorageBackend '{StorageBackend}'. Supported values: FileSystem, AzureBlob",
                nameof(StorageBackend));
    }
}

/// <summary>
/// Configuration options for the file system message store backend.
/// </summary>
public class FileSystemMessageStoreOptions
{
    /// <summary>
    /// Gets or sets the base directory where messages are stored.
    /// Default: %LOCALAPPDATA%\Daiv3\messages
    /// </summary>
    public string StorageDirectory { get; set; } = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "Daiv3", "messages");

    /// <summary>
    /// Gets or sets the cleanup interval in seconds.
    /// Default: 3600 (1 hour).
    /// </summary>
    public int CleanupIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StorageDirectory))
            throw new ArgumentException("StorageDirectory must not be empty", nameof(StorageDirectory));

        if (CleanupIntervalSeconds < 60) // At least 1 minute
            throw new ArgumentException("CleanupIntervalSeconds must be >= 60", nameof(CleanupIntervalSeconds));
    }
}

/// <summary>
/// Configuration options for the Azure Blob message store backend (future).
/// </summary>
public class AzureBlobMessageStoreOptions
{
    /// <summary>
    /// Gets or sets the Azure Storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the container name for storing messages.
    /// Default: "agent-messages"
    /// </summary>
    public string ContainerName { get; set; } = "agent-messages";

    /// <summary>
    /// Gets or sets the polling interval in milliseconds for checking new messages.
    /// Default: 1000 (1 second).
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        // ConnectionString is optional if using DefaultAzureCredential (managed identity)
        if (string.IsNullOrWhiteSpace(ContainerName))
            throw new ArgumentException("ContainerName must not be empty", nameof(ContainerName));

        if (PollingIntervalMs < 100) // At least 100ms
            throw new ArgumentException("PollingIntervalMs must be >= 100", nameof(PollingIntervalMs));
    }

    /// <summary>
    /// Extracts the Azure Storage account name from the connection string.
    /// </summary>
    /// <returns>The account name (e.g., "myaccount" from "DefaultEndpointsProtocol=https;AccountName=myaccount;...").</returns>
    public string ExtractAccountName()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required to extract account name");

        foreach (var part in ConnectionString.Split(';'))
        {
            if (part.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring(12);
            }
        }

        throw new InvalidOperationException("AccountName not found in ConnectionString");
    }

    /// <summary>
    /// Extracts the Azure Storage account key from the connection string.
    /// </summary>
    /// <returns>The account key.</returns>
    public string ExtractAccountKey()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required to extract account key");

        foreach (var part in ConnectionString.Split(';'))
        {
            if (part.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring(11);
            }
        }

        throw new InvalidOperationException("AccountKey not found in ConnectionString");
    }

    /// <summary>
    /// Gets the blob service URI (e.g., https://myaccount.blob.core.windows.net).
    /// </summary>
    /// <returns>The blob service URI.</returns>
    public string GetStorageUri()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required to get storage URI");

        var accountName = ExtractAccountName();
        return $"https://{accountName}.blob.core.windows.net";
    }
}
