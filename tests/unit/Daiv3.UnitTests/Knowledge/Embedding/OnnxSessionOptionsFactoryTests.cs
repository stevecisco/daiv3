using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class OnnxSessionOptionsFactoryTests
{
    [Fact]
    public void Create_UsesCpuWhenPreferenceIsCpu()
    {
        var options = new EmbeddingOnnxOptions
        {
            ModelPath = "C:\\models\\embed.onnx",
            ExecutionProviderPreference = OnnxExecutionProviderPreference.Cpu
        };
        var factory = new OnnxSessionOptionsFactory(
            NullLogger<OnnxSessionOptionsFactory>.Instance,
            Options.Create(options));

        var sessionOptions = factory.Create(out var provider);

        Assert.NotNull(sessionOptions);
        Assert.Equal(OnnxExecutionProvider.Cpu, provider);
    }
}
