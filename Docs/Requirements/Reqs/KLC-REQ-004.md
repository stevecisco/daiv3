# KLC-REQ-004

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use Microsoft.Data.Sqlite for persistence.

## Implementation Design

### Owning Component
**Project:** `Daiv3.Persistence`  
**Namespace:** `Daiv3.Persistence`

### Architecture
The persistence layer provides:
1. **Database Context** - Connection management, migrations, and transaction support  
2. **Repository Pattern** - Data access abstractions for each entity type
3. **Schema Management** - Version-based migration system
4. **Connection Pooling** - Efficient connection reuse

### Database Schema (from Design Document Section 4.4)

```sql
-- Documents table: Track source files and change detection
CREATE TABLE documents (
    doc_id TEXT PRIMARY KEY,
    source_path TEXT NOT NULL UNIQUE,
    file_hash TEXT NOT NULL,
    format TEXT NOT NULL,
    size_bytes INTEGER NOT NULL,
    last_modified INTEGER NOT NULL,
    status TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    metadata_json TEXT
);

-- Topic Index: Tier 1 - One embedding per document
CREATE TABLE topic_index (
    doc_id TEXT PRIMARY KEY,
    summary_text TEXT NOT NULL,
    embedding_blob BLOB NOT NULL,
    embedding_dimensions INTEGER NOT NULL,
    source_path TEXT NOT NULL,
    file_hash TEXT NOT NULL,
    ingested_at INTEGER NOT NULL,
    metadata_json TEXT,
    FOREIGN KEY (doc_id) REFERENCES documents(doc_id) ON DELETE CASCADE
);

-- Chunk Index: Tier 2 - Multiple embeddings per document
CREATE TABLE chunk_index (
    chunk_id TEXT PRIMARY KEY,
    doc_id TEXT NOT NULL,
    chunk_text TEXT NOT NULL,
    embedding_blob BLOB NOT NULL,
    embedding_dimensions INTEGER NOT NULL,
    chunk_order INTEGER NOT NULL,
    topic_tags TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (doc_id) REFERENCES documents(doc_id) ON DELETE CASCADE
);

-- Projects: Scoped knowledge bases
CREATE TABLE projects (
    project_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    root_paths TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    status TEXT NOT NULL,
    config_json TEXT
);

-- Tasks: Work items with dependencies
CREATE TABLE tasks (
    task_id TEXT PRIMARY KEY,
    project_id TEXT,
    title TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL,
    priority INTEGER NOT NULL,
    scheduled_at INTEGER,
    completed_at INTEGER,
    dependencies_json TEXT,
    result_json TEXT,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE SET NULL
);

-- Sessions: Conversation and interaction tracking
CREATE TABLE sessions (
    session_id TEXT PRIMARY KEY,
    project_id TEXT,
    started_at INTEGER NOT NULL,
    ended_at INTEGER,
    summary TEXT,
    key_knowledge_json TEXT,
    FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE SET NULL
);

-- Model Queue: Task queue for model execution
CREATE TABLE model_queue (
    request_id TEXT PRIMARY KEY,
    model_id TEXT NOT NULL,
    priority INTEGER NOT NULL,
    status TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    started_at INTEGER,
    completed_at INTEGER,
    error_message TEXT
);

-- Schema version tracking
CREATE TABLE schema_version (
    version INTEGER PRIMARY KEY,
    applied_at INTEGER NOT NULL,
    description TEXT NOT NULL
);
```

### Key Interfaces

```csharp
// Core database context
public interface IDatabaseContext : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default);
    Task<SqliteTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task MigrateToLatestAsync(CancellationToken ct = default);
}

// Generic repository interface
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<string> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
```

### Configuration

**appsettings.json:**
```json
{
  "Persistence": {
    "DatabasePath": "%LOCALAPPDATA%\\Daiv3\\daiv3.db",
    "ConnectionString": "Data Source={DatabasePath};Mode=ReadWriteCreate;Cache=Shared;",
    "EnableWAL": true,
    "BusyTimeout": 5000,
    "MaxPoolSize": 10
  }
}
```

### Implementation Tasks

- [X] **Task 1**: Define database schema SQL and migration framework (2 hours)
- [ ] **Task 2**: Implement `DatabaseContext` with connection management (3 hours)
- [ ] **Task 3**: Implement migration system with version tracking (2 hours)
- [ ] **Task 4**: Implement generic repository base class (2 hours)
- [ ] **Task 5**: Implement entity-specific repositories (documents, topics, chunks, etc.) (4 hours)
- [ ] **Task 6**: Add comprehensive logging (1 hour)
- [ ] **Task 7**: Create unit tests for repositories (3 hours)
- [ ] **Task 8**: Create integration tests with SQLite (2 hours)
- [ ] **Task 9**: Add CLI commands for database management (2 hours)

**Total Estimated Effort:** 21 hours

## Testing Plan

### Unit Tests
- Schema creation from SQL scripts
- Migration version tracking
- Connection management and pooling
- Transaction begin/commit/rollback
- Entity CRUD operations
- Concurrent access scenarios
- Error handling for constraint violations

### Integration Tests  
- Full database initialization
- Multi-table operations with foreign keys
- Cascade delete behavior
- Large blob storage (embeddings)
- Query performance with realistic data volumes
- Database backup and restore

### Performance Tests
- 10,000 document insertions
- Large embedding blob storage (768-dim * 10,000 docs)
- Concurrent reader/writer scenarios
- Query performance for vector search (full table scan benchmarks)

## Usage and Operational Notes

### CLI Commands (to be implemented)
- `daiv3 db init` - Initialize database
- `daiv3 db migrate` - Run migrations
- `daiv3 db status` - Show schema version and stats
- `daiv3 db backup <path>` - Backup database
- `daiv3 db restore <path>` - Restore from backup

### Observability
- Log all schema migrations with versions
- Log connection pool statistics
- Log slow queries (>100ms threshold)
- Track database file size growth

### Error Handling
- Handle database locked scenarios (busy timeout)
- Automatic retry for transient SQLite errors
- Clear error messages for constraint violations
- Graceful degradation if database is readonly

## Dependencies
- None (foundation layer)

## Related Requirements
- KM-DATA-001: Database schema for knowledge management
- ARCH-REQ-006: Persistence layer implementation

## Implementation Status
- **Status:** In Progress (Design Complete, Implementation Pending)
- **Progress:** 5% (Design documentation)
- **Updated:** 2026-02-22
