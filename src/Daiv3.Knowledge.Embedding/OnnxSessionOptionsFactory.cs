using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace Daiv3.Knowledge.Embedding;

public sealed class OnnxSessionOptionsFactory : IOnnxSessionOptionsFactory
{
    private readonly ILogger<OnnxSessionOptionsFactory> _logger;
    private readonly EmbeddingOnnxOptions _options;
    private readonly IHardwareDetectionProvider _hardwareDetection;

    public OnnxSessionOptionsFactory(
        ILogger<OnnxSessionOptionsFactory> logger,
        IOptions<EmbeddingOnnxOptions> options,
        IHardwareDetectionProvider hardwareDetection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _hardwareDetection = hardwareDetection ?? throw new ArgumentNullException(nameof(hardwareDetection));
    }

    public SessionOptions Create(out OnnxExecutionProvider selectedProvider)
    {
        _options.Validate();

        var preference = ResolvePreference(_options.ExecutionProviderPreference);

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        if (preference == OnnxExecutionProviderPreference.Auto ||
            preference == OnnxExecutionProviderPreference.DirectML)
        {
            try
            {
                // When using Microsoft.ML.OnnxRuntime.DirectML package, 
                // creating a new SessionOptions() automatically uses DirectML
                var directMlOptions = new SessionOptions();
                directMlOptions.AppendExecutionProvider_DML(0);
                ApplyCommonOptions(directMlOptions);
                selectedProvider = OnnxExecutionProvider.DirectML;
                return directMlOptions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DirectML provider initialization failed; falling back to CPU.");
            }
        }
#else
        if (preference == OnnxExecutionProviderPreference.DirectML)
        {
            _logger.LogWarning("DirectML provider requested on non-Windows target; falling back to CPU.");
        }
#endif

        var cpuOptions = new SessionOptions();
        ApplyCommonOptions(cpuOptions);
        selectedProvider = OnnxExecutionProvider.Cpu;
        return cpuOptions;
    }

    private OnnxExecutionProviderPreference ResolvePreference(OnnxExecutionProviderPreference preference)
    {
        if (preference != OnnxExecutionProviderPreference.Auto)
        {
            return preference;
        }

        var tiers = _hardwareDetection.GetAvailableTiers();
        var bestTier = tiers.Count > 0 ? tiers[0] : HardwareAccelerationTier.Cpu;
        bool hasNpu = tiers.Contains(HardwareAccelerationTier.Npu);

        switch (bestTier)
        {
            case HardwareAccelerationTier.Npu:
                _logger.LogInformation("NPU detected; preferring DirectML for embedding inference.");
                return OnnxExecutionProviderPreference.DirectML;
            case HardwareAccelerationTier.Gpu:
                if (!hasNpu)
                {
                    _logger.LogInformation("NPU unavailable or insufficient; falling back to GPU for embedding inference.");
                }
                else
                {
                    _logger.LogInformation("GPU detected; preferring DirectML for embedding inference.");
                }
                return OnnxExecutionProviderPreference.DirectML;
            default:
                _logger.LogDebug("No hardware accelerator detected; using CPU for embedding inference.");
                return OnnxExecutionProviderPreference.Cpu;
        }
    }

    private void ApplyCommonOptions(SessionOptions options)
    {
        if (_options.IntraOpNumThreads.HasValue)
        {
            options.IntraOpNumThreads = _options.IntraOpNumThreads.Value;
        }

        if (_options.InterOpNumThreads.HasValue)
        {
            options.InterOpNumThreads = _options.InterOpNumThreads.Value;
        }

        options.EnableMemoryPattern = _options.EnableMemoryPattern;
        options.EnableCpuMemArena = _options.EnableCpuMemArena;
    }
}
