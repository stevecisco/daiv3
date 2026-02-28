using Daiv3.Persistence;
using System.Reflection;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for SchemaScripts SQL validation.
/// Validates SQL syntax and ensures all required tables are defined.
/// </summary>
public class SchemaScriptsTests
{
    [Fact]
    public void Migration001_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var sql = GetMigration001Sql();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(sql));
        Assert.True(sql.Length > 100, "Migration SQL should be substantial");
    }

    [Fact]
    public void Migration001_ContainsSchemaVersionTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema_version", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version INTEGER PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applied_at INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("description TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsDocumentsTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("documents", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("doc_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source_path TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_hash TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("format TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("size_bytes INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsTopicIndexTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("topic_index", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("doc_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("summary_text TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embedding_blob BLOB NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embedding_dimensions INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOREIGN KEY (doc_id) REFERENCES documents(doc_id)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsChunkIndexTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunk_index", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunk_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("doc_id TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunk_text TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embedding_blob BLOB NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunk_order INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOREIGN KEY (doc_id) REFERENCES documents(doc_id)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsProjectsTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("projects", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root_paths TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsTasksTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tasks", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_id TEXT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("title TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("priority INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dependencies_json TEXT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOREIGN KEY (project_id) REFERENCES projects(project_id)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration002_IsNotNullOrEmpty()
    {
        var sql = GetMigration002Sql();

        Assert.False(string.IsNullOrWhiteSpace(sql));
        Assert.True(sql.Length > 50, "Migration SQL should include task timestamp column updates");
    }

    [Fact]
    public void Migration002_AddsTaskNextRunAndLastRunColumns()
    {
        var sql = GetMigration002Sql();

        Assert.Contains("ALTER TABLE tasks ADD COLUMN next_run_at INTEGER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ALTER TABLE tasks ADD COLUMN last_run_at INTEGER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration002_ContainsTaskTimestampIndexes()
    {
        var sql = GetMigration002Sql();

        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_tasks_next_run_at ON tasks(next_run_at)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_tasks_last_run_at ON tasks(last_run_at)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsSessionsTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sessions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("session_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_id TEXT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("started_at INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FOREIGN KEY (project_id) REFERENCES projects(project_id)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsModelQueueTable()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model_queue", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request_id TEXT PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model_id TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("priority INTEGER NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("payload_json TEXT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsIndexes()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        Assert.Contains("CREATE INDEX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_documents_source_path", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_documents_status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_topic_index_source_path", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_chunk_index_doc_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_projects_status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_tasks_project_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_tasks_status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_sessions_project_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_model_queue_status", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsForeignKeyConstraints()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        // Verify cascade deletes are configured
        Assert.Contains("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON DELETE SET NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_ContainsCheckConstraints()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act & Assert
        // Verify status check constraints exist
        Assert.Contains("CHECK(status IN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration001_AllTablesHaveIfNotExists()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act - Count CREATE TABLE statements
        var createTableCount = CountOccurrences(sql, "CREATE TABLE");
        var ifNotExistsCount = CountOccurrences(sql, "IF NOT EXISTS");

        // Assert - All CREATE TABLE statements should have IF NOT EXISTS
        Assert.True(ifNotExistsCount >= createTableCount, 
            $"Expected at least {createTableCount} IF NOT EXISTS clauses, found {ifNotExistsCount}");
    }

    [Fact]
    public void Migration001_NoSyntaxErrors_ValidSemicolonUsage()
    {
        // Arrange
        var sql = GetMigration001Sql();

        // Act - Split by semicolons
        var statements = sql
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--"))
            .ToList();

        // Assert - Should have multiple valid SQL statements
        Assert.True(statements.Count > 8, $"Expected more than 8 SQL statements, found {statements.Count}");
        
        // Each statement should start with a SQL keyword
        foreach (var statement in statements)
        {
            var startsWithKeyword = statement.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase) ||
                                   statement.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase) ||
                                   statement.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase);
            Assert.True(startsWithKeyword, $"Statement doesn't start with expected keyword: {statement.Substring(0, Math.Min(50, statement.Length))}");
        }
    }

    private static string GetMigration001Sql()
    {
        var field = typeof(SchemaScripts).GetField("Migration001_InitialSchema", 
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        return field?.GetValue(null) as string ?? throw new InvalidOperationException("Could not access Migration001_InitialSchema");
    }

    private static string GetMigration002Sql()
    {
        var field = typeof(SchemaScripts).GetField("Migration002_TaskSchedulingTimestamps",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        return field?.GetValue(null) as string ?? throw new InvalidOperationException("Could not access Migration002_TaskSchedulingTimestamps");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
