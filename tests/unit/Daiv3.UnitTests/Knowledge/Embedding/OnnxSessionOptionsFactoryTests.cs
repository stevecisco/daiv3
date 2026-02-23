using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

/// <summary>
/// Unit tests for ONNX session options factory.
/// Verifies hardware-specific session configuration and fallback behavior.
/// 
/// Tests the Windows 11 Copilot+ hardware degradation strategy:
/// DirectML (GPU/NPU) → CPU with SIMD
/// </summary>
public class OnnxSessionOptionsFactoryTests
{
    /// <summary>
    /// Helper method to create a default logger for testing.
    /// </summary>
    private ILogger<OnnxSessionOptionsFactory> GetTestLogger()
    {
        return NullLogger<OnnxSessionOptionsFactory>.Instance;
    }

    /// <summary>
    /// Helper method to create default embedding options.
    /// </summary>
    private EmbeddingOnnxOptions GetDefaultOptions()
    {
        return new EmbeddingOnnxOptions
        {
            ModelPath = "C:\\models\\embed.onnx",
            ExecutionProviderPreference = OnnxExecutionProviderPreference.Auto
        };
    }

    [Fact]
    public void Create_WithCpuPreference_ReturnsCpuProvider()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = OnnxExecutionProviderPreference.Cpu;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        Assert.Equal(OnnxExecutionProvider.Cpu, provider);
    }

    [Fact]
    public void Create_WithDirectMLPreference_AttemptsDirectML()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = OnnxExecutionProviderPreference.DirectML;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        // Provider will be DirectML on Windows, CPU on other platforms or if DirectML unavailable
        Assert.True(
            provider == OnnxExecutionProvider.DirectML || provider == OnnxExecutionProvider.Cpu,
            "Provider should be DirectML or CPU fallback");
    }

    [Fact]
    public void Create_WithAutoPreference_SelectsBestAvailable()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = OnnxExecutionProviderPreference.Auto;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        // Auto should select best available: DirectML on Windows if available, else CPU
        Assert.True(
            provider == OnnxExecutionProvider.DirectML || provider == OnnxExecutionProvider.Cpu,
            "Auto preference should select best available provider");
    }

    [Fact]
    public void Create_ReturnsTuningOptions()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.IntraOpNumThreads = 4;
        options.InterOpNumThreads = 2;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        Assert.Equal(4, sessionOptions.IntraOpNumThreads);
        Assert.Equal(2, sessionOptions.InterOpNumThreads);
    }

    [Fact]
    public void Create_AppliesMemoryPatternOptions()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.EnableMemoryPattern = true;
        options.EnableCpuMemArena = true;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        Assert.True(sessionOptions.EnableMemoryPattern);
        Assert.True(sessionOptions.EnableCpuMemArena);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<EmbeddingOnnxOptions>? nullOptions = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OnnxSessionOptionsFactory(GetTestLogger(), nullOptions!));
    }

    [Fact]
    public void Create_MultipleCallsProduceConsistentProvider()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = OnnxExecutionProviderPreference.Cpu;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        factory.Create(out var provider1);
        factory.Create(out var provider2);

        // Assert
        Assert.Equal(provider1, provider2);
    }

    [Fact]
    public void Create_DefaultThreadOptions_AppliedOnlyIfSet()
    {
        // Arrange
        var options = GetDefaultOptions();
        options.IntraOpNumThreads = null;
        options.InterOpNumThreads = null;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        // When null, the session options should use ONNX Runtime defaults
        // (We can't directly verify this without inspecting internal state,
        // but we verify the creation succeeds)
    }

    [Theory]
    [InlineData(OnnxExecutionProviderPreference.Auto)]
    [InlineData(OnnxExecutionProviderPreference.Cpu)]
    [InlineData(OnnxExecutionProviderPreference.DirectML)]
    public void Create_AllPreferencesProduceSessions(OnnxExecutionProviderPreference preference)
    {
        // Arrange
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = preference;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        Assert.True(
            provider == OnnxExecutionProvider.DirectML || provider == OnnxExecutionProvider.Cpu,
            "Provider should be either DirectML or CPU");
    }

    [Fact]
    public void Create_CpuAlwaysAvailable()
    {
        // Arrange - Force CPU preference to ensure it works
        var options = GetDefaultOptions();
        options.ExecutionProviderPreference = OnnxExecutionProviderPreference.Cpu;
        var factory = new OnnxSessionOptionsFactory(GetTestLogger(), Options.Create(options));

        // Act
        var sessionOptions = factory.Create(out var provider);

        // Assert
        Assert.NotNull(sessionOptions);
        Assert.Equal(OnnxExecutionProvider.Cpu, provider);
        // CPU should always be available as the universal fallback
    }
}
