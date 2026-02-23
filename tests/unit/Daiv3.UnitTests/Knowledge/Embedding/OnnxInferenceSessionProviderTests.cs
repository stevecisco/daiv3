using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class OnnxInferenceSessionProviderTests
{
    [Fact]
    public async Task GetSessionAsync_ThrowsWhenModelMissing()
    {
        var options = new EmbeddingOnnxOptions
        {
            ModelPath = Path.Combine(Path.GetTempPath(), "missing-model.onnx")
        };
        var sessionFactory = new StubSessionOptionsFactory();
        var provider = new OnnxInferenceSessionProvider(
            NullLogger<OnnxInferenceSessionProvider>.Instance,
            Options.Create(options),
            sessionFactory);

        await Assert.ThrowsAsync<FileNotFoundException>(() => provider.GetSessionAsync());

        Assert.False(sessionFactory.Invoked);
    }

    private sealed class StubSessionOptionsFactory : IOnnxSessionOptionsFactory
    {
        public bool Invoked { get; private set; }

        public SessionOptions Create(out OnnxExecutionProvider selectedProvider)
        {
            Invoked = true;
            selectedProvider = OnnxExecutionProvider.Cpu;
            return new SessionOptions();
        }
    }
}
