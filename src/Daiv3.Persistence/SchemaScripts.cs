namespace Daiv3.Persistence;

/// <summary>
/// Contains SQL scripts for database schema migrations.
/// </summary>
public static class SchemaScripts
{
    /// <summary>
    /// Migration 001: Initial database schema
    /// Creates all core tables for document management, knowledge indexing, projects, tasks, and model queue.
    /// </summary>
    public const string Migration001_InitialSchema = @"
-- Schema version tracking table
CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at INTEGER NOT NULL,
    description TEXT NOT NULL
);

-- Documents: Track source files and change detection
CREATE TABLE IF NOT EXISTS documents (
    doc_id TEXT PRIMARY KEY,
    source_path TEXT NOT NULL UNIQUE,
    file_hash TEXT NOT NULL,
    format TEXT NOT NULL,
    size_bytes INTEGER NOT NULL,
    last_modified INTEGER NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('pending', 'indexed', 'error', 'deleted')),
    created_at INTEGER NOT NULL,
    metadata_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_documents_source_path ON documents(source_path);
CREATE INDEX IF NOT EXISTS idx_documents_status ON documents(status);
CREATE INDEX IF NOT EXISTS idx_documents_file_hash ON documents(file_hash);

-- Topic Index: Tier 1 - One embedding per document for fast coarse search
CREATE TABLE IF NOT EXISTS topic_index (
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

CREATE INDEX IF NOT EXISTS idx_topic_index_source_path ON topic_index(source_path);
CREATE INDEX IF NOT EXISTS idx_topic_index_ingested_at ON topic_index(ingested_at);

-- Chunk Index: Tier 2 - Multiple embeddings per document for fine-grained search
CREATE TABLE IF NOT EXISTS chunk_index (
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

CREATE INDEX IF NOT EXISTS idx_chunk_index_doc_id ON chunk_index(doc_id);
CREATE INDEX IF NOT EXISTS idx_chunk_index_chunk_order ON chunk_index(doc_id, chunk_order);
CREATE INDEX IF NOT EXISTS idx_chunk_index_created_at ON chunk_index(created_at);

-- Projects: Scoped knowledge bases with configuration
CREATE TABLE IF NOT EXISTS projects (
    project_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    root_paths TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('active', 'archived', 'deleted')),
    config_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_projects_status ON projects(status);
CREATE INDEX IF NOT EXISTS idx_projects_name ON projects(name);

-- Tasks: Work items with dependencies and scheduling
CREATE TABLE IF NOT EXISTS tasks (
    task_id TEXT PRIMARY KEY,
    project_id TEXT,
    title TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL CHECK(status IN ('pending', 'queued', 'in_progress', 'complete', 'failed', 'blocked')),
    priority INTEGER NOT NULL DEFAULT 1,
    scheduled_at INTEGER,
    completed_at INTEGER,
    dependencies_json TEXT,
    result_json TEXT,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_tasks_project_id ON tasks(project_id);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
CREATE INDEX IF NOT EXISTS idx_tasks_scheduled_at ON tasks(scheduled_at);

-- Sessions: Conversation and interaction tracking for context
CREATE TABLE IF NOT EXISTS sessions (
    session_id TEXT PRIMARY KEY,
    project_id TEXT,
    started_at INTEGER NOT NULL,
    ended_at INTEGER,
    summary TEXT,
    key_knowledge_json TEXT,
    FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_sessions_project_id ON sessions(project_id);
CREATE INDEX IF NOT EXISTS idx_sessions_started_at ON sessions(started_at);

-- Model Queue: Task queue for model execution with priority
CREATE TABLE IF NOT EXISTS model_queue (
    request_id TEXT PRIMARY KEY,
    model_id TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 1,
    status TEXT NOT NULL CHECK(status IN ('pending', 'queued', 'running', 'complete', 'error', 'cancelled')),
    payload_json TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    started_at INTEGER,
    completed_at INTEGER,
    error_message TEXT
);

CREATE INDEX IF NOT EXISTS idx_model_queue_status ON model_queue(status);
CREATE INDEX IF NOT EXISTS idx_model_queue_priority ON model_queue(priority DESC);
CREATE INDEX IF NOT EXISTS idx_model_queue_model_id ON model_queue(model_id, status);
CREATE INDEX IF NOT EXISTS idx_model_queue_created_at ON model_queue(created_at);
";
}
