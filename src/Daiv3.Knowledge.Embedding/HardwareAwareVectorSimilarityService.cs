using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Hardware-aware router for vector similarity operations.
/// Prefers NPU/GPU when available, with CPU fallback.
/// </summary>
public sealed class HardwareAwareVectorSimilarityService : IVectorSimilarityService
{
    private readonly ILogger<HardwareAwareVectorSimilarityService> _logger;
    private readonly IHardwareDetectionProvider _hardwareDetection;
    private readonly CpuVectorSimilarityService _cpuService;

    public HardwareAwareVectorSimilarityService(
        ILogger<HardwareAwareVectorSimilarityService> logger,
        IHardwareDetectionProvider hardwareDetection,
        CpuVectorSimilarityService cpuService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hardwareDetection = hardwareDetection ?? throw new ArgumentNullException(nameof(hardwareDetection));
        _cpuService = cpuService ?? throw new ArgumentNullException(nameof(cpuService));

        LogPreference();
    }

    public float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        return _cpuService.CosineSimilarity(vector1, vector2);
    }

    public void BatchCosineSimilarity(
        ReadOnlySpan<float> queryVector,
        ReadOnlySpan<float> targetVectors,
        int vectorCount,
        int dimensions,
        Span<float> results)
    {
        _cpuService.BatchCosineSimilarity(queryVector, targetVectors, vectorCount, dimensions, results);
    }

    public void Normalize(ReadOnlySpan<float> vector, Span<float> normalized)
    {
        _cpuService.Normalize(vector, normalized);
    }

    private void LogPreference()
    {
        var tiers = _hardwareDetection.GetAvailableTiers();
        var bestTier = tiers.Count > 0 ? tiers[0] : HardwareAccelerationTier.Cpu;
        bool hasNpu = tiers.Contains(HardwareAccelerationTier.Npu);
        switch (bestTier)
        {
            case HardwareAccelerationTier.Npu:
                _logger.LogInformation(
                    "NPU available; vector operations prefer NPU when an accelerated implementation is configured. Using CPU fallback.");
                break;
            case HardwareAccelerationTier.Gpu:
                if (!hasNpu)
                {
                    _logger.LogInformation(
                        "NPU unavailable or insufficient; falling back to GPU for vector operations. Using CPU fallback.");
                }
                else
                {
                    _logger.LogInformation(
                        "GPU available; vector operations prefer GPU when an accelerated implementation is configured. Using CPU fallback.");
                }
                break;
            default:
                _logger.LogDebug("CPU vector operations selected.");
                break;
        }
    }
}
