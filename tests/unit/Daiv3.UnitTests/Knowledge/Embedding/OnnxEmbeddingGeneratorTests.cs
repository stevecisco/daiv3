using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using Microsoft.ML.OnnxRuntime.Tensors;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class OnnxEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ThrowsWhenTextMissing()
    {
        var generator = CreateGenerator(_ => CreateVectorOutput(1f, 2f));

        await Assert.ThrowsAsync<ArgumentException>(() => generator.GenerateEmbeddingAsync(" "));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_VectorOutput_ReturnsOutput()
    {
        var generator = CreateGenerator(_ => CreateVectorOutput(1f, 2f, 3f), normalize: false);

        var embedding = await generator.GenerateEmbeddingAsync("hello world");

        Assert.Equal(3, embedding.Length);
        Assert.Equal(1f, embedding[0]);
        Assert.Equal(2f, embedding[1]);
        Assert.Equal(3f, embedding[2]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_TokenOutput_UsesMeanPooling()
    {
        var generator = CreateGenerator(CreateTokenOutput, normalize: false);

        var embedding = await generator.GenerateEmbeddingAsync("hello world");

        Assert.Equal(2, embedding.Length);
        Assert.Equal(0.5f, embedding[0], 3);
        Assert.Equal(0.5f, embedding[1], 3);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_NormalizesEmbedding()
    {
        var generator = CreateGenerator(_ => CreateVectorOutput(3f, 4f), normalize: true);

        var embedding = await generator.GenerateEmbeddingAsync("hello world");

        Assert.Equal(2, embedding.Length);
        Assert.Equal(0.6f, embedding[0], 3);
        Assert.Equal(0.8f, embedding[1], 3);
    }

    private static OnnxEmbeddingGenerator CreateGenerator(
        Func<EmbeddingModelInput, DenseTensor<float>> outputFactory,
        bool normalize = false)
    {
        var onnxOptions = new EmbeddingOnnxOptions
        {
            ModelPath = "C:\\models\\embed.onnx",
            NormalizeEmbeddings = normalize,
            PoolingStrategy = EmbeddingPoolingStrategy.MeanPooling
        };

        var tokenizationOptions = new EmbeddingTokenizationOptions
        {
            EncodingName = "r50k_base",
            MaxTokens = 2
        };

        var runner = new StubRunner(outputFactory);
        var tokenizerProvider = new StubTokenizerProvider();

        return new OnnxEmbeddingGenerator(
            NullLogger<OnnxEmbeddingGenerator>.Instance,
            Options.Create(onnxOptions),
            Options.Create(tokenizationOptions),
            tokenizerProvider,
            runner);
    }

    private static DenseTensor<float> CreateVectorOutput(params float[] values)
    {
        return new DenseTensor<float>(values, new[] { 1, values.Length });
    }

    private static DenseTensor<float> CreateTokenOutput(EmbeddingModelInput input)
    {
        var sequenceLength = input.AttentionMask.Dimensions[1];
        var hiddenSize = 2;
        var data = new float[sequenceLength * hiddenSize];

        if (sequenceLength >= 1)
        {
            data[0] = 1f;
            data[1] = 0f;
        }

        if (sequenceLength >= 2)
        {
            data[2] = 0f;
            data[3] = 1f;
        }

        return new DenseTensor<float>(data, new[] { 1, sequenceLength, hiddenSize });
    }

    private sealed class StubRunner : IOnnxEmbeddingModelRunner
    {
        private readonly Func<EmbeddingModelInput, DenseTensor<float>> _outputFactory;

        public StubRunner(Func<EmbeddingModelInput, DenseTensor<float>> outputFactory)
        {
            _outputFactory = outputFactory;
        }

        public Task<DenseTensor<float>> RunAsync(EmbeddingModelInput input, CancellationToken ct = default)
        {
            return Task.FromResult(_outputFactory(input));
        }
    }

    private sealed class StubTokenizerProvider : IEmbeddingTokenizerProvider
    {
        private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("r50k_base");

        public Tokenizer GetTokenizer() => _tokenizer;
    }
}
