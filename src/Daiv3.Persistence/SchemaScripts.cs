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

    /// <summary>
    /// Migration 002: Scheduled task execution timestamps
    /// Adds columns for tracking next-run and last-run times on tasks.
    /// </summary>
    public const string Migration002_TaskSchedulingTimestamps = @"
ALTER TABLE tasks ADD COLUMN next_run_at INTEGER;
ALTER TABLE tasks ADD COLUMN last_run_at INTEGER;

CREATE INDEX IF NOT EXISTS idx_tasks_next_run_at ON tasks(next_run_at);
CREATE INDEX IF NOT EXISTS idx_tasks_last_run_at ON tasks(last_run_at);
";

    /// <summary>
    /// Migration 003: Agent definitions and configuration
    /// Adds agents table for storing agent definitions in user-editable JSON format.
    /// </summary>
    public const string Migration003_AgentDefinitions = @"
-- Agents: Agent definitions stored in user-editable config format
CREATE TABLE IF NOT EXISTS agents (
    agent_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    purpose TEXT NOT NULL,
    enabled_skills_json TEXT,
    config_json TEXT,
    status TEXT NOT NULL CHECK(status IN ('active', 'archived', 'deleted')) DEFAULT 'active',
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_agents_status ON agents(status);
CREATE INDEX IF NOT EXISTS idx_agents_name ON agents(name);
CREATE INDEX IF NOT EXISTS idx_agents_created_at ON agents(created_at);
";

    /// <summary>
    /// Migration 004: Learning memory table
    /// Adds learnings table for storing agent learnings with provenance and timestamps.
    /// Supports semantic retrieval, filtering by scope, and user visibility/control.
    /// </summary>
    public const string Migration004_LearningMemory = @"
-- Learnings: AI learning records with provenance tracking
CREATE TABLE IF NOT EXISTS learnings (
    learning_id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    trigger_type TEXT NOT NULL CHECK(trigger_type IN ('UserFeedback', 'SelfCorrection', 'CompilationError', 'ToolFailure', 'KnowledgeConflict', 'Explicit')),
    scope TEXT NOT NULL CHECK(scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    source_agent TEXT,
    source_task_id TEXT,
    embedding_blob BLOB,
    embedding_dimensions INTEGER,
    tags TEXT,
    confidence REAL NOT NULL CHECK(confidence >= 0.0 AND confidence <= 1.0),
    status TEXT NOT NULL CHECK(status IN ('Active', 'Suppressed', 'Superseded', 'Archived')) DEFAULT 'Active',
    times_applied INTEGER NOT NULL DEFAULT 0,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    created_by TEXT NOT NULL
);

-- Performance indexes for semantic search and filtering
CREATE INDEX IF NOT EXISTS idx_learnings_status ON learnings(status);
CREATE INDEX IF NOT EXISTS idx_learnings_scope ON learnings(scope);
CREATE INDEX IF NOT EXISTS idx_learnings_trigger_type ON learnings(trigger_type);
CREATE INDEX IF NOT EXISTS idx_learnings_source_agent ON learnings(source_agent);
CREATE INDEX IF NOT EXISTS idx_learnings_source_task_id ON learnings(source_task_id);
CREATE INDEX IF NOT EXISTS idx_learnings_confidence ON learnings(confidence DESC);
CREATE INDEX IF NOT EXISTS idx_learnings_times_applied ON learnings(times_applied DESC);
CREATE INDEX IF NOT EXISTS idx_learnings_created_at ON learnings(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_learnings_created_by ON learnings(created_by);

-- Composite index for active learnings by scope (most common query pattern)
CREATE INDEX IF NOT EXISTS idx_learnings_status_scope ON learnings(status, scope);
";

    /// <summary>
    /// Migration 005: Learning promotions tracking
    /// Adds promotions table for tracking learning scope promotions with provenance.
    /// Implements KBP-DATA-001 (source task/session IDs) and KBP-DATA-002 (target scope and timestamps).
    /// </summary>
    public const string Migration005_LearningPromotions = @"
-- Promotions: Track learning scope promotions for audit trail
CREATE TABLE IF NOT EXISTS promotions (
    promotion_id TEXT PRIMARY KEY,
    learning_id TEXT NOT NULL,
    from_scope TEXT NOT NULL CHECK(from_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    to_scope TEXT NOT NULL CHECK(to_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    promoted_at INTEGER NOT NULL,
    promoted_by TEXT NOT NULL,
    source_task_id TEXT,
    source_agent TEXT,
    notes TEXT,
    FOREIGN KEY (learning_id) REFERENCES learnings(learning_id) ON DELETE CASCADE
);

-- Performance indexes for promotion history queries
CREATE INDEX IF NOT EXISTS idx_promotions_learning_id ON promotions(learning_id);
CREATE INDEX IF NOT EXISTS idx_promotions_promoted_at ON promotions(promoted_at DESC);
CREATE INDEX IF NOT EXISTS idx_promotions_source_task_id ON promotions(source_task_id);
CREATE INDEX IF NOT EXISTS idx_promotions_promoted_by ON promotions(promoted_by);
CREATE INDEX IF NOT EXISTS idx_promotions_to_scope ON promotions(to_scope);

-- Composite index for learning promotion history (most common query pattern)
CREATE INDEX IF NOT EXISTS idx_promotions_learning_promoted_at ON promotions(learning_id, promoted_at DESC);
";

    /// <summary>
    /// Migration 006: Agent promotion proposals
    /// Adds agent_promotion_proposals table for tracking agent-proposed promotions requiring user confirmation.
    /// Implements KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
    /// </summary>
    public const string Migration006_AgentPromotionProposals = @"
-- Agent Promotion Proposals: Track agent-proposed learning promotions requiring user confirmation
CREATE TABLE IF NOT EXISTS agent_promotion_proposals (
    proposal_id TEXT PRIMARY KEY,
    learning_id TEXT NOT NULL,
    proposing_agent TEXT NOT NULL,
    source_task_id TEXT,
    from_scope TEXT NOT NULL CHECK(from_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    suggested_target_scope TEXT NOT NULL CHECK(suggested_target_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    justification TEXT,
    confidence_score REAL NOT NULL DEFAULT 0.5,
    status TEXT NOT NULL CHECK(status IN ('Pending', 'Approved', 'Rejected')) DEFAULT 'Pending',
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    reviewed_by TEXT,
    reviewed_at INTEGER,
    rejection_reason TEXT,
    FOREIGN KEY (learning_id) REFERENCES learnings(learning_id) ON DELETE CASCADE
);

-- Performance indexes for proposal query patterns
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_learning_id ON agent_promotion_proposals(learning_id);
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_status ON agent_promotion_proposals(status);
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_proposing_agent ON agent_promotion_proposals(proposing_agent);
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_source_task_id ON agent_promotion_proposals(source_task_id);
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_created_at ON agent_promotion_proposals(created_at DESC);

-- Composite index for pending proposals (most common query pattern)
CREATE INDEX IF NOT EXISTS idx_agent_promotion_proposals_status_created_at ON agent_promotion_proposals(status, created_at DESC);
";

    /// <summary>
    /// Migration 007: Promotion revert tracking and metrics
    /// Adds revert_promotions table for reversibility (KBP-NFR-001) and promotion_metrics for instrumentation.
    /// Implements KBP-NFR-001: Promotions SHOULD be transparent and reversible.
    /// </summary>
    public const string Migration007_PromotionRevertAndMetrics = @"
-- Promotion Reverts: Track when promotions are undone (reversibility - KBP-NFR-001)
CREATE TABLE IF NOT EXISTS revert_promotions (
    revert_id TEXT PRIMARY KEY,
    promotion_id TEXT NOT NULL UNIQUE,
    learning_id TEXT NOT NULL,
    reverted_at INTEGER NOT NULL,
    reverted_by TEXT NOT NULL,
    reverted_from_scope TEXT NOT NULL,
    reverted_to_scope TEXT NOT NULL,
    notes TEXT,
    FOREIGN KEY (promotion_id) REFERENCES promotions(promotion_id) ON DELETE CASCADE,
    FOREIGN KEY (learning_id) REFERENCES learnings(learning_id) ON DELETE CASCADE
);

-- Performance indexes for revert queries
CREATE INDEX IF NOT EXISTS idx_revert_promotions_promotion_id ON revert_promotions(promotion_id);
CREATE INDEX IF NOT EXISTS idx_revert_promotions_learning_id ON revert_promotions(learning_id);
CREATE INDEX IF NOT EXISTS idx_revert_promotions_reverted_at ON revert_promotions(reverted_at DESC);
CREATE INDEX IF NOT EXISTS idx_revert_promotions_reverted_by ON revert_promotions(reverted_by);

-- Promotion Metrics: Instrumentation for transparency (KBP-NFR-001)
CREATE TABLE IF NOT EXISTS promotion_metrics (
    metric_id TEXT PRIMARY KEY,
    metric_name TEXT NOT NULL,
    metric_value REAL NOT NULL,
    recorded_at INTEGER NOT NULL,
    period_start INTEGER,
    period_end INTEGER,
    context TEXT
);

-- Performance indexes for metric queries
CREATE INDEX IF NOT EXISTS idx_promotion_metrics_name ON promotion_metrics(metric_name);
CREATE INDEX IF NOT EXISTS idx_promotion_metrics_recorded_at ON promotion_metrics(recorded_at DESC);
CREATE INDEX IF NOT EXISTS idx_promotion_metrics_name_recorded_at ON promotion_metrics(metric_name, recorded_at DESC);
";

    /// <summary>
    /// Migration 008: Web fetch metadata tracking
    /// Adds web_fetches table for storing metadata about fetched web content.
    /// Implements WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.
    /// </summary>
    public const string Migration008_WebFetchMetadata = @"
-- Web Fetches: Track fetched web content metadata for change detection and source tracking
CREATE TABLE IF NOT EXISTS web_fetches (
    web_fetch_id TEXT PRIMARY KEY,
    doc_id TEXT NOT NULL,
    source_url TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    fetch_date INTEGER NOT NULL,
    title TEXT,
    description TEXT,
    status TEXT NOT NULL CHECK(status IN ('active', 'stale', 'error', 'deleted')) DEFAULT 'active',
    error_message TEXT,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (doc_id) REFERENCES documents(doc_id) ON DELETE CASCADE
);

-- Performance indexes for web fetch queries
CREATE INDEX IF NOT EXISTS idx_web_fetches_doc_id ON web_fetches(doc_id);
CREATE INDEX IF NOT EXISTS idx_web_fetches_source_url ON web_fetches(source_url);
CREATE INDEX IF NOT EXISTS idx_web_fetches_content_hash ON web_fetches(content_hash);
CREATE INDEX IF NOT EXISTS idx_web_fetches_fetch_date ON web_fetches(fetch_date DESC);
CREATE INDEX IF NOT EXISTS idx_web_fetches_status ON web_fetches(status);

-- Composite index for querying active fetches by date (most common pattern)
CREATE INDEX IF NOT EXISTS idx_web_fetches_status_fetch_date ON web_fetches(status, fetch_date DESC);
";

    /// <summary>
    /// Migration 009: Application settings with versioning support
    /// Adds app_settings table for storing versioned user configuration (CT-DATA-001).
    /// Implements:
    /// - Settings schema versioning to support upgrades
    /// - Key-value pairs with JSON values
    /// - Change tracking for audit trails
    /// - Version tracking for migration support
    /// </summary>
    public const string Migration009_ApplicationSettings = @"
-- App Settings: Versioned application settings with support for upgrades
CREATE TABLE IF NOT EXISTS app_settings (
    setting_id TEXT PRIMARY KEY,
    setting_key TEXT NOT NULL UNIQUE,
    setting_value TEXT NOT NULL,
    value_type TEXT NOT NULL DEFAULT 'json' CHECK(value_type IN ('string', 'json', 'integer', 'boolean', 'real')),
    category TEXT NOT NULL DEFAULT 'general' CHECK(category IN ('general', 'paths', 'models', 'providers', 'hardware', 'ui', 'knowledge')),
    schema_version INTEGER NOT NULL DEFAULT 1,
    description TEXT,
    is_sensitive BOOLEAN NOT NULL DEFAULT 0,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    updated_by TEXT DEFAULT 'system'
);

-- Performance indexes for settings queries
CREATE INDEX IF NOT EXISTS idx_app_settings_key ON app_settings(setting_key);
CREATE INDEX IF NOT EXISTS idx_app_settings_category ON app_settings(category);
CREATE INDEX IF NOT EXISTS idx_app_settings_schema_version ON app_settings(schema_version);
CREATE INDEX IF NOT EXISTS idx_app_settings_updated_at ON app_settings(updated_at DESC);

-- Composite index for category-based setting queries (most common pattern)
CREATE INDEX IF NOT EXISTS idx_app_settings_category_key ON app_settings(category, setting_key);

-- Settings Version History: Audit trail for setting changes with version tracking
CREATE TABLE IF NOT EXISTS settings_version_history (
    history_id TEXT PRIMARY KEY,
    setting_key TEXT NOT NULL,
    old_value TEXT,
    new_value TEXT NOT NULL,
    schema_version INTEGER NOT NULL,
    changed_at INTEGER NOT NULL,
    changed_by TEXT DEFAULT 'system',
    reason TEXT,
    FOREIGN KEY (setting_key) REFERENCES app_settings(setting_key) ON DELETE CASCADE
);

-- Performance indexes for version history queries
CREATE INDEX IF NOT EXISTS idx_settings_version_history_setting_key ON settings_version_history(setting_key);
CREATE INDEX IF NOT EXISTS idx_settings_version_history_changed_at ON settings_version_history(changed_at DESC);
CREATE INDEX IF NOT EXISTS idx_settings_version_history_schema_version ON settings_version_history(schema_version);

-- Composite index for setting audit trail (most common pattern)
CREATE INDEX IF NOT EXISTS idx_settings_version_history_key_changed_at ON settings_version_history(setting_key, changed_at DESC);

-- Settings Metadata: Tracks schema version and migration status
CREATE TABLE IF NOT EXISTS settings_metadata (
    metadata_key TEXT PRIMARY KEY,
    metadata_value TEXT NOT NULL,
    updated_at INTEGER NOT NULL
);
";
}
