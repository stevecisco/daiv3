using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Knowledge.Embedding.IntegrationTests;

public class DirectMlSessionOptionsFactoryTests
{
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
            Options.Create(options));

        var sessionOptions = factory.Create(out var provider);

        Assert.NotNull(sessionOptions);
        Assert.True(provider == OnnxExecutionProvider.DirectML || provider == OnnxExecutionProvider.Cpu);
    }
}
