using System.Linq;
using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Knowledge.Tests.DocProc;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_Whitespace_ReturnsEmpty()
    {
        var options = Options.Create(new TokenizationOptions
        {
            EncodingName = "r50k_base",
            MaxTokensPerChunk = 10,
            OverlapTokens = 2
        });

        var provider = new TokenizerProvider(NullLogger<TokenizerProvider>.Instance, options);
        var chunker = new TextChunker(NullLogger<TextChunker>.Instance, provider, options);

        var chunks = chunker.Chunk("  ");

        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_SplitsTextByTokens()
    {
        var options = Options.Create(new TokenizationOptions
        {
            EncodingName = "r50k_base",
            MaxTokensPerChunk = 3,
            OverlapTokens = 1,
            ConsiderNormalization = false,
            ConsiderPreTokenization = true
        });

        var provider = new TokenizerProvider(NullLogger<TokenizerProvider>.Instance, options);
        var chunker = new TextChunker(NullLogger<TextChunker>.Instance, provider, options);

        var text = string.Join(" ", Enumerable.Repeat("This is a tokenization chunking test", 10));

        var chunks = chunker.Chunk(text);

        var tokenizer = provider.GetTokenizer();
        var tokens = tokenizer.EncodeToTokens(
            text,
            out _,
            options.Value.ConsiderPreTokenization,
            options.Value.ConsiderNormalization);

        if (tokens.Count <= options.Value.MaxTokensPerChunk)
        {
            Assert.Single(chunks);
        }
        else
        {
            Assert.True(chunks.Count > 1);
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var tokenCount = tokenizer.CountTokens(
                chunk.Text,
                options.Value.ConsiderPreTokenization,
                options.Value.ConsiderNormalization);

            Assert.InRange(chunk.TokenCount, 1, options.Value.MaxTokensPerChunk);
            Assert.Equal(chunk.Text.Length, chunk.Length);
            Assert.Equal(tokenCount, chunk.TokenCount);
            Assert.True(chunk.StartOffset >= 0);
            Assert.True(chunk.StartOffset + chunk.Length <= text.Length);
        }
    }
}
