using Daiv3.Infrastructure.Shared.Hardware;
using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class HardwareAwareVectorSimilarityServiceTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var hardware = new Mock<IHardwareDetectionProvider>();
        var cpuService = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);

        Assert.Throws<ArgumentNullException>(() =>
            new HardwareAwareVectorSimilarityService(null!, hardware.Object, cpuService));
    }

    [Fact]
    public void CosineSimilarity_DelegatesToCpuService()
    {
        var hardware = new Mock<IHardwareDetectionProvider>();
        hardware.Setup(x => x.GetBestAvailableTier()).Returns(HardwareAccelerationTier.Cpu);
        hardware.Setup(x => x.GetAvailableTiers())
            .Returns(new[] { HardwareAccelerationTier.Cpu });

        var cpuService = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var service = new HardwareAwareVectorSimilarityService(
            NullLogger<HardwareAwareVectorSimilarityService>.Instance,
            hardware.Object,
            cpuService);

        float[] vector1 = [1.0f, 2.0f, 3.0f];
        float[] vector2 = [4.0f, 5.0f, 6.0f];

        float expected = cpuService.CosineSimilarity(vector1, vector2);
        float actual = service.CosineSimilarity(vector1, vector2);

        Assert.Equal(expected, actual, precision: 6);
    }

    [Fact]
    public void Constructor_UsesHardwareDetectionToDeterminePreference()
    {
        var hardware = new Mock<IHardwareDetectionProvider>();
        hardware.Setup(x => x.GetAvailableTiers())
            .Returns(new[] { HardwareAccelerationTier.Gpu, HardwareAccelerationTier.Cpu });

        var cpuService = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);

        _ = new HardwareAwareVectorSimilarityService(
            NullLogger<HardwareAwareVectorSimilarityService>.Instance,
            hardware.Object,
            cpuService);

        hardware.Verify(x => x.GetAvailableTiers(), Times.Once);
    }
}
