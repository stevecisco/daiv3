# HW-NFR-002: CPU Performance with SIMD Acceleration - Performance Expectations

## Overview

This document specifies the expected performance characteristics of the DAIv3 system when running on CPU-only hardware with SIMD acceleration. It serves as a reference for system operators, performance analysts, and developers.

## Performance Guarantees

The system is engineered to maintain **usable performance** when NPU and GPU hardware are unavailable, relying entirely on CPU SIMD acceleration (System.Numerics.TensorPrimitives).

### Core Metrics

#### 1. Single Vector Pair Comparison
- **Threshold**: < 200µs per pair (2x normal rate for system variance)
- **Real-world range**: 50-150µs on modern CPUs (x64/ARM64)
- **Typical operation**: Computing cosine similarity between two 384-768 dimension vectors

**Why this matters**: Single comparisons are the atomic operation; this threshold ensures streaming operations remain responsive.

#### 2. Batch Tier 1 Search (Topic Index)
- **Threshold**: < 25ms to search 10,000 vectors of 384 dimensions
- **Throughput**: ~400,000 vectors/second on modern CPUs
- **Per-vector cost**: 2.5µs per vector including SIMD overhead
- **Typical use case**: Initial topic filtering across all documents

**Performance Example**:
```
Query vector: 384 dimensions
Target vectors: 10,000 documents (384 dims each)
Expected time: 15-25ms on Intel Core Ultra / Snapdragon X
```

#### 3. Batch Tier 2 Search (Chunk Index)
- **Threshold**: < 100ms to search 1,000 vectors of 768 dimensions
- **Throughput**: ~10,000 vectors/second on modern CPUs
- **Per-vector cost**: 100µs per vector
- **Typical use case**: Fine-grained search within top candidate documents

**Performance Example**:
```
Query vector: 768 dimensions
Target vectors: 1,000 chunks from top topics (768 dims each)
Expected time: 50-100ms on Intel Core Ultra / Snapdragon X
```

#### 4. Embedding Generation (document processing)
- **Single document**: ~2-5 seconds for typical knowledge doc (assuming I/O not bottleneck)
- **Batch of 100 chunks**: ~500ms CPU time for embedding computation
- **Throughput**: ~200 chunks/second embedding generation

## Scaling Characteristics

The service exhibits **linear scaling** with vector count under normal operating conditions:

```
Vector Count    384-Dim Time    768-Dim Time    Linear?
─────────────   ────────────    ────────────    ───────
100             ~0.25ms         ~1.0ms          ✓
1,000           ~2.5ms          ~10ms           ✓
10,000          ~25ms           ~100ms          ✓
50,000          ~125ms          ~500ms          ✓ (some cache effects)
```

**Key insight**: Doubling vector count roughly doubles execution time. This enables predictable performance planning.

## Hardware Platform Specific Expectations

### Windows 11 Copilot+ Devices (NPU/GPU Primary)

CPU-only scenario assumes automatic fallback when primary acceleration unavailable:

| CPU Tier | Single Pair | 10K x 384D | 1K x 768D | Notes |
|----------|------------|-----------|----------|-------|
| **NPU Available** | N/A | < 5ms | < 10ms | Typical case - use HW-REQ-003 |
| **Snapdragon X (CPU fallback)** | 80µs | 20ms | 60ms | High-performance ARM64, ~12 cores |
| **Intel Core Ultra (CPU fallback)** | 100µs | 25ms | 80ms | x64 hybrid architecture |
| **Intel Core i7 12th Gen** | 120µs | 30ms | 100ms | Older x64, still performant |
| **Low-end CPUs** | 200+µs | 40+ms | 150+ms | Edge case, may feel slow |

### Cross-Platform Deployment

If deploying to non-Copilot+ Windows systems:

- **Minimum viable CPU**: Intel Core i5-8th Gen / AMD Ryzen 5 2600
- **Recommended**: Intel Core i7 / AMD Ryzen 7 (multi-core preferred)
- **Expected latency impact**: 1.5-2x longer than Copilot+ equivalents

## User Experience Implications

### Responsive Operations (< 100ms)
- **Tier 1 topic search**: Instant to user (satisfies KM-NFR-001)
- **Small batch operations**: Responsive feedback
- **Search metadata operations**: Unnoticeable latency

### Acceptable Latency (100ms - 1s)
- **Tier 2 chunk search on top candidates**: Perceivable but acceptable
- **Single embedding generation for user input**: Acceptable wait time
- **Knowledge index updates (per-document)**: Background operation

### Slow Operations (> 1s)
- **Batch embedding of large corpus**: Long-running batch operation
- **Full index rebuild**: Multi-document operation, time-proportional to corpus size

## Optimization Opportunities

### Within-Session Optimizations (Implemented)
1. **Pre-compute query vector magnitude**: 1 operation vs. N operations
2. **SIMD batch operations**: TensorPrimitives handles parallelization
3. **Vector reuse**: In-memory cache of embeddings (KM-REQ-018, KM-REQ-019)

### Configuration-Level Tuning
- **Metrics collection**: Disable in production (`EnableMetricsCollection: false`)
- **Batch size tuning**: Larger batches (1K+) amortize setup costs
- **Thread pool sizing**: System defaults usually optimal

### Deployment-Level Tuning
- **CPU affinity**: Pin critical threads to performance cores
- **NUMA awareness**: Respect node-local memory placement
- **Thermal management**: Performance may degrade under thermal throttling

## Degradation Scenarios

### Under What Conditions Does Performance Degrade?

| Scenario | Impact | Duration | Recovery |
|----------|--------|----------|----------|
| **High CPU contention** | 1.5-3x slower | Variable | When load decreases |
| **Thermal throttling** | 0.5-0.8x max speed | Minutes to hours | When CPU cools |
| **Memory pressure (swap)** | 10+ x slower | Minutes | Restart to clear swap |
| **JIT compilation pause** | 10-100ms extra | First execution | Subsequent calls fast |

**Monitoring tip**: If operations consistently exceed 2x expected time, investigate system resource usage.

## Telemetry and Monitoring

The system can optionally collect performance metrics when configured:

```csharp
var metricsOptions = new PerformanceMetricsOptions
{
    EnableMetricsCollection = true,
    SlowOperationThresholdMs = 50.0,  // Alert if > 50ms
    SlowOperationSampleRate = 0.1,     // Log 10% of slow ops
    EnableDetailedTelemetry = false    // Set to true for maximum verbosity
};
```

**Recommended monitoring thresholds**:
- **Green (normal)**: Single pair < 100µs, Tier 1 search < 15ms, Tier 2 search < 70ms
- **Yellow (degraded)**: 1.5x normal thresholds
- **Red (concerning)**: 2x normal thresholds

## Comparison: CPU vs. NPU/GPU

For reference, hardware-accelerated performance:

| Operation | CPU Only | GPU/NPU | Speedup |
|-----------|----------|---------|---------|
| Single pair | 80-150µs | 5-10µs | 10-20x |
| 10K x 384D | 15-25ms | 2-5ms | 5-10x |
| 1K x 768D | 50-100ms | 5-15ms | 5-10x |

**CPU remains viable** for most workloads even with this gap, because:
1. Batch operations approach hardware saturation
2. Most queries are small (100-1000 vectors)
3. I/O and model loading dominate end-to-end time

## Best Practices for Operations

### When CPU-Only is Acceptable
- Tier 1 search on reasonable corpus (< 100K documents)
- Single-document indexing (< 100 chunks)
- Real-time chat with streaming responses
- Small batch operations in background

### When CPU-Only is Problematic
- Bulk re-indexing of very large corpus (100K+ documents)
- Extremely high-concurrency scenarios (100+ simultaneous queries)
- Interactive operations on massive knowledge bases (10M+ chunks)

**Mitigation**: Pre-compute or cache expensive operations; use asynchronous processing for batch operations.

## Benchmarking Your Deployment

To benchmark your specific hardware:

```bash
# Run performance benchmark suite
dotnet test tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests -k VectorSimilarityPerformance

# Examine output for your CPU type and take measurements
# Compare against tables above
```

**Document your baseline**:
- CPU model and core count
- Single pair time
- 10K vector search time
- Date and conditions

## Future Improvements

Potential optimizations for future versions:
1. **HNSW indexing** (KM-NFR-002): Logarithmic search instead of linear
2. **Batch inference**: GPU/NPU batching for embedding generation
3. **Quantization**: 8-bit vectors instead of 32-bit (4x memory reduction)
4. **SIMD optimization**: Hand-tuned kernels for special cases

---

**Version**: 1.0  
**Updated**: February 23, 2026  
**Audience**: System operators, DevOps engineers, performance analysts
