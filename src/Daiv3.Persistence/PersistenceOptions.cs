namespace Daiv3.Persistence;

/// <summary>
/// Configuration options for the persistence layer.
/// </summary>
public sealed class PersistenceOptions
{
    /// <summary>
    /// Path to the SQLite database file.
    /// Supports environment variable expansion (e.g., %LOCALAPPDATA%).
    /// </summary>
    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Daiv3",
        "daiv3.db");

    /// <summary>
    /// Enable Write-Ahead Logging (WAL) mode for better concurrency.
    /// Default: true
    /// </summary>
    public bool EnableWAL { get; set; } = true;

    /// <summary>
    /// Busy timeout in milliseconds when database is locked.
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int BusyTimeout { get; set; } = 5000;

    /// <summary>
    /// Maximum number of connections in the pool.
    /// Default: 10
    /// </summary>
    public int MaxPoolSize { get; set; } = 10;

    /// <summary>
    /// Gets the fully resolved database path with environment variables expanded.
    /// </summary>
    public string GetExpandedDatabasePath()
    {
        return Environment.ExpandEnvironmentVariables(DatabasePath);
    }

    /// <summary>
    /// Builds the connection string from the configured options.
    /// </summary>
    public string BuildConnectionString()
    {
        var path = GetExpandedDatabasePath();
        return $"Data Source={path};Mode=ReadWriteCreate;Cache=Shared;Pooling=True;";
    }
}
