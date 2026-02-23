# ARCH-NFR-002

Source Spec: 3. System Architecture Overview - Requirements

## Requirement
The architecture SHOULD allow the Knowledge Graph to be added later without changing external interfaces.

## Implementation Design

### Overview
This requirement ensures architectural extensibility for a future Knowledge Graph (KG) subsystem without requiring changes to external service interfaces (ITwoTierIndexService, IVectorStoreService, IKnowledgeDocumentProcessor). The Knowledge Graph will provide semantic relationship mapping and graph-based reasoning over the knowledge base.

### Owning Component
**Project:** `Daiv3.Knowledge`  
**Namespace:** `Daiv3.Knowledge` (new interfaces and services)

### Architecture

#### 1. **Extension Points**

The Knowledge Layer is designed with clear extensibility boundaries:

- **ITwoTierIndexService** - Current interface remains unchanged
  - KG enhancement will be provided via optional decorator pattern (ISearchEnhancer)
  - Existing callers continue to call SearchAsync() with no changes
  - Optional KG decorators enriched results with graph metadata

- **IKnowledgeDocumentProcessor** - Current interface remains unchanged
  - KG indexing triggered via optional post-processing hook (IIndexEnhancer)
  - Existing callers continue to call ProcessDocumentAsync() with no changes
  - Optional KG services receive notifications after indexing

- **IVectorStoreService** - Current interface remains unchanged
  - KG graph relationships stored in dedicated schema extension
  - No changes to existing vector storage methods
  - KG uses separate repositories for relationship metadata

#### 2. **Optional Extension Interfaces** (TBD when KG is implemented)

```csharp
/// <summary>
/// Optional service for enriching search results with knowledge graph information.
/// Implements decorator pattern - can be registered or omitted.
/// </summary>
public interface ISearchEnhancer
{
    /// <summary>
    /// Optionally enhance search results with graph-based relevance or relationships.
    /// This method is called AFTER primary search returns results.
    /// If not implemented, search behavior is unchanged from non-KG baseline.
    /// </summary>
    Task<TwoTierSearchResults> EnhanceSearchResultsAsync(
        TwoTierSearchResults baselineResults,
        float[] queryEmbedding,
        CancellationToken ct = default);

    /// <summary>
    /// Whether this enhancer should be applied (allows conditional activation).
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Optional service for indexing knowledge graph relationships during document processing.
/// Implements post-processing hook pattern - can be registered or omitted.
/// </summary>
public interface IIndexEnhancer
{
    /// <summary>
    /// Called AFTER a document has been indexed in Tier 1 and Tier 2.
    /// KG service can extract relationships and index them.
    /// If not implemented, indexing proceeds with no graph data.
    /// </summary>
    Task EnhanceIndexAsync(
        string docId,
        string summaryText,
        IReadOnlyList<string> chunks,
        CancellationToken ct = default);

    /// <summary>
    /// Whether this enhancer should be applied.
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Marker interface for KG relationship queries (future extension).
/// Allows graph-specific filtering without changing ITwoTierIndexService.
/// </summary>
public interface IKnowledgeGraphQuery
{
    // Reserved for future graph-specific query parameters
}
```

#### 3. **Metrics and Instrumentation**

Define baseline metrics for the Knowledge Layer (without KG) to enable performance comparison when KG is added:

```csharp
/// <summary>
/// Metrics for Knowledge Layer performance monitoring.
/// Establishes baseline performance thresholds for extensibility verification.
/// </summary>
public class KnowledgeLayerMetrics
{
    // Search Performance
    public double Tier1SearchLatencyMs { get; set; }      // Target: <10ms for 10K docs, 384 dims
    public double Tier2SearchLatencyMs { get; set; }      // Target: <50ms per document's chunks
    public double SearchTotalLatencyMs { get; set; }      // Target: <100ms end-to-end
    
    // Indexing Performance
    public double DocumentIndexLatencyMs { get; set; }    // Target: <5s per document
    public double EmbeddingGenerationMs { get; set; }     // Tracked separately for analysis
    public double ChunkGenerationMs { get; set; }         // Tracked separately for analysis
    
    // Memory Usage
    public long Tier1MemoryBytesUsed { get; set; }        // Topic index in-memory cache
    public long Tier1MemoryBytesPerVector { get; set; }   // Baseline: 4 bytes/dim * 384 dims = 1536 bytes
    public int Tier1VectorsLoaded { get; set; }
    
    // Index Integrity
    public int TotalDocumentsIndexed { get; set; }
    public int TotalChunksIndexed { get; set; }
    public int EmbeddingDimensionsTier1 { get; set; }      // Baseline: 384
    public int EmbeddingDimensionsTier2 { get; set; }      // Baseline: 768
    
    // Guardrail Indicators
    public bool Tier1SearchExceededThreshold { get; set; }
    public bool Tier2SearchExceededThreshold { get; set; }
    public bool MemoryUsageExceededThreshold { get; set; }
}

/// <summary>
/// Configuration for Knowledge Layer performance guardrails.
/// Allows tuning thresholds to ensure KG integration doesn't degrade performance.
/// </summary>
public class KnowledgeLayerGuardrails
{
    /// <summary>Maximum acceptable latency for Tier 1 search (milliseconds).</summary>
    public double MaxTier1SearchLatencyMs { get; set; } = 50;
    
    /// <summary>Maximum acceptable latency for complete two-tier search.</summary>
    public double MaxSearchTotalLatencyMs { get; set; } = 200;
    
    /// <summary>Maximum acceptable latency for single document indexing.</summary>
    public double MaxIndexLatencyMs { get; set; } = 10000;
    
    /// <summary>Maximum memory allowed for Tier 1 in-memory cache (bytes).</summary>
    public long MaxTier1MemoryBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
    
    /// <summary>Enable guardrail enforcement (when false, logs warnings only).</summary>
    public bool EnforceGuardrails { get; set; } = false;
    
    /// <summary>Enable detailed metrics recording for analysis.</summary>
    public bool RecordDetailedMetrics { get; set; } = true;
}
```

#### 4. **Instrumentation Service**

```csharp
/// <summary>
/// Records and validates Knowledge Layer metrics against guardrails.
/// Provides baseline data for KG integration verification.
/// </summary>
public interface IKnowledgeLayerTelemetry
{
    /// <summary>Record a search operation's metrics.</summary>
    void RecordSearchMetrics(SearchMetricsContext context);
    
    /// <summary>Record an indexing operation's metrics.</summary>
    void RecordIndexingMetrics(IndexingMetricsContext context);
    
    /// <summary>Get current metrics snapshot.</summary>
    KnowledgeLayerMetrics GetCurrentMetrics();
    
    /// <summary>Verify metrics against guardrails.</summary>
    KnowledgeLayerMetricsValidationResult ValidateAgainstGuardrails();
    
    /// <summary>Export metrics for analysis (JSON format).</summary>
    string ExportMetricsAsJson();
}
```

#### 5. **Service Registration Pattern**

The extensibility is enabled through optional DI registration:

```csharp
// In KnowledgeServiceExtensions.cs
public static void AddKnowledgeLayer(this IServiceCollection services, Action<DocumentProcessingOptions>? configureOptions = null)
{
    // Existing registrations
    services.AddScoped<IVectorStoreService, VectorStoreService>();
    services.AddScoped<ITwoTierIndexService, TwoTierIndexService>();
    services.AddScoped<IKnowledgeDocumentProcessor, KnowledgeDocumentProcessor>();
    services.AddScoped<IKnowledgeLayerTelemetry, KnowledgeLayerTelemetry>();
    
    // Optional: When KG is implemented, these can be added without changing above registrations
    // services.AddScoped<ISearchEnhancer, KnowledgeGraphSearchEnhancer>();
    // services.AddScoped<IIndexEnhancer, KnowledgeGraphIndexEnhancer>();
    
    // Configure guardrails
    services.Configure<KnowledgeLayerGuardrails>(options =>
    {
        options.EnforceGuardrails = false; // Default: log-only mode
        options.RecordDetailedMetrics = true;
    });
}

// When KG is ready, only add_optional_ services:
// public static void AddKnowledgeGraph(this IServiceCollection services) { ... }
// This keeps existing code unchanged
```

### External Interface Preservation

**Key Principle:** No method signatures or return types in existing interfaces change.

- `ITwoTierIndexService.SearchAsync()` - Same signature, results enriched via data model only
- `IVectorStoreService.Store*/Get*()` - All methods unchanged
- `IKnowledgeDocumentProcessor.Process*()` - All methods unchanged

When KG is implemented, clients will receive enriched data models (e.g., more fields in TwoTierSearchResults) but no interface changes.

### Dependencies on Architecture
- **ARCH-REQ-005** - Knowledge Layer implementation provides the base interfaces
- **ARCH-CON-001** - Unidirectional dependencies maintained; KG services depend only on Knowledge layer, not vice versa
- **HW-NFR-002** - Performance guardrails align with CPU baseline expectations

## Implementation Plan
1. **Phase 1: Instrumentation** (Current requirement)
   - Create KnowledgeLayerMetrics, KnowledgeLayerGuardrails classes
   - Implement IKnowledgeLayerTelemetry service
   - Integrate telemetry into TwoTierIndexService, VectorStoreService, KnowledgeDocumentProcessor
   - Establish baseline metrics against current implementation

2. **Phase 2: Extension Interfaces** (Current requirement)
   - Define ISearchEnhancer, IIndexEnhancer, IKnowledgeGraphQuery interfaces
   - Update service registration for optional components
   - Document extension points in architecture guide

3. **Phase 3: KG Implementation** (Future - separate requirement)
   - Implement ISearchEnhancer: KnowledgeGraphSearchEnhancer
   - Implement IIndexEnhancer: KnowledgeGraphIndexEnhancer
   - Verify no changes to existing interface signatures needed
   - Measure KG overhead via instrumentation

## Testing Plan
- **Baseline Measurement Tests** - Capture baseline metrics for Tier 1/Tier 2 search without KG
- **Instrumentation Tests** - Verify metrics are recorded correctly
- **Guardrail Tests** - Verify guardrail thresholds work and alert appropriately
- **Extensibility Tests** - Verify mock ISearchEnhancer/IIndexEnhancer can be registered without breaking existing code
- **Interface Immutability Tests** - Ensure no existing method signatures change after adding KG stubs
- **Performance Regression Tests** - Ensure adding instrumentation doesn't degrade baseline performance >5%

## Usage and Operational Notes

### Configuration
Via KnowledgeLayerGuardrails configuration:
```csharp
services.Configure<KnowledgeLayerGuardrails>(options =>
{
    options.MaxTier1SearchLatencyMs = 50;
    options.MaxSearchTotalLatencyMs = 200;
    options.EnforceGuardrails = true; // Fail fast on threshold violation
    options.RecordDetailedMetrics = true;
});
```

### Metrics Export
```csharp
// Get current metrics
var metrics = telemetry.GetCurrentMetrics();
var validation = telemetry.ValidateAgainstGuardrails();
var json = telemetry.ExportMetricsAsJson();

// Log results
if (validation.HasViolations)
{
    logger.LogWarning("Knowledge Layer guardrails violated: {Violations}", 
        string.Join(", ", validation.ViolationDetails));
}
```

### Operational Constraints
- **Metrics Recording Overhead** - <5% latency impact when RecordDetailedMetrics=true
- **Memory Overhead** - Metrics storage requires <10 MB for tracking
- **Offline Mode** - Metrics recorded locally; no external telemetry required

## Dependencies
- KLC-REQ-004 (SQLite persistence for schema extensions when KG is added)
- KLC-REQ-011 (UI surfaces for metrics/guardrail configuration)
- ARCH-REQ-005 (Knowledge Layer foundation)

## Related Requirements
- ARCH-REQ-005 (Knowledge Layer)
- HW-NFR-002 (Performance baseline for CPU fallback)
