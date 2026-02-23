using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Knowledge.Embedding.IntegrationTests;

public class DirectMlSessionOptionsFactoryTests
{
    private sealed class StubHardwareDetectionProvider : IHardwareDetectionProvider
    {
        public HardwareAccelerationTier GetBestAvailableTier() => HardwareAccelerationTier.Gpu;

        public bool IsTierAvailable(HardwareAccelerationTier tier)
        {
            return tier == HardwareAccelerationTier.Gpu || tier == HardwareAccelerationTier.Cpu;
        }

        public IReadOnlyList<HardwareAccelerationTier> GetAvailableTiers()
        {
            return new[] { HardwareAccelerationTier.Gpu, HardwareAccelerationTier.Cpu };
        }

        public string GetDiagnosticInfo() => "BestTier: Gpu";
    }

    [Fact]
    public void Create_AllowsDirectMlPreference()
    {
        var options = new EmbeddingOnnxOptions
        {
            ModelPath = "C:\\models\\embed.onnx",
            ExecutionProviderPreference = OnnxExecutionProviderPreference.DirectML
        };
        var factory = new OnnxSessionOptionsFactory(
            NullLogger<OnnxSessionOptionsFactory>.Instance,
            Options.Create(options),
            new StubHardwareDetectionProvider());

        var sessionOptions = factory.Create(out var provider);

        Assert.NotNull(sessionOptions);
        Assert.True(provider == OnnxExecutionProvider.DirectML || provider == OnnxExecutionProvider.Cpu);
    }
}
