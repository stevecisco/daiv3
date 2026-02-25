using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

/// <summary>
/// Tests for model-specific tokenizer implementations (BERT WordPiece and SentencePiece).
/// Verifies that each tokenizer correctly implements the IEmbeddingTokenizer interface.
/// </summary>
public class EmbeddingTokenizerImplementationTests
{
    [Fact]
    public void BertWordPieceTokenizer_HasCorrectProperties()
    {
        // Create a temporary vocab file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var vocabPath = Path.Combine(tempDir, "vocab.txt");
        
        try
        {
            // Create minimal vocab file
            var vocabLines = new[]
            {
                "[PAD]",
                "[UNK]",
                "[CLS]",
                "[SEP]",
                "[MASK]",
                "the",
                "a",
                "and",
                "test"
            };
            File.WriteAllLines(vocabPath, vocabLines);

            var tokenizer = new BertWordPieceTokenizer(NullLogger<BertWordPieceTokenizer>.Instance, "all-MiniLM-L6-v2", vocabPath);

            Assert.Equal("BertWordPieceTokenizer", tokenizer.Name);
            Assert.Equal("all-MiniLM-L6-v2", tokenizer.ModelId);
            Assert.Equal(9, tokenizer.VocabularySize);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BertWordPieceTokenizer_Tokenizes_ValidInput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var vocabPath = Path.Combine(tempDir, "vocab.txt");
        
        try
        {
            var vocabLines = new[]
            {
                "[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]",
                "the", "a", "and", "test", "hello", "world"
            };
            File.WriteAllLines(vocabPath, vocabLines);

            var tokenizer = new BertWordPieceTokenizer(NullLogger<BertWordPieceTokenizer>.Instance, "test-model", vocabPath);
            
            var tokens = tokenizer.Tokenize("hello world");
            
            Assert.NotEmpty(tokens);
            Assert.All(tokens, token => Assert.True(token >= 0 && token < tokenizer.VocabularySize));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BertWordPieceTokenizer_ValidateTokenIds_CorrectBounds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var vocabPath = Path.Combine(tempDir, "vocab.txt");
        
        try
        {
            var vocabLines = Enumerable.Range(0, 100).Select(i => $"token_{i}").ToArray();
            File.WriteAllLines(vocabPath, vocabLines);

            var tokenizer = new BertWordPieceTokenizer(NullLogger<BertWordPieceTokenizer>.Instance, "test-model", vocabPath);
            
            // Valid tokens
            var validTokens = new long[] { 0, 50, 99 };
            Assert.True(tokenizer.ValidateTokenIds(validTokens));
            
            // Invalid: token >= vocabulary size
            var invalidTokens = new long[] { 0, 100 };
            Assert.False(tokenizer.ValidateTokenIds(invalidTokens));
            
            // Invalid: negative token
            var negativeTokens = new long[] { -1, 50 };
            Assert.False(tokenizer.ValidateTokenIds(negativeTokens));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SentencePieceTokenizer_HasCorrectProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var tokenizer = new SentencePieceTokenizer(NullLogger<SentencePieceTokenizer>.Instance, "nomic-embed-text-v1.5", tempDir);

            Assert.Equal("SentencePieceTokenizer", tokenizer.Name);
            Assert.Equal("nomic-embed-text-v1.5", tokenizer.ModelId);
            Assert.True(tokenizer.VocabularySize > 0, "Vocabulary size should be greater than 0");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SentencePieceTokenizer_Tokenizes_ValidInput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var tokenizer = new SentencePieceTokenizer(NullLogger<SentencePieceTokenizer>.Instance, "test-model", tempDir);
            
            var tokens = tokenizer.Tokenize("hello world");
            
            Assert.NotEmpty(tokens);
            Assert.All(tokens, token => Assert.True(token >= 0 && token < tokenizer.VocabularySize));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SentencePieceTokenizer_ValidateTokenIds_CorrectBounds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var tokenizer = new SentencePieceTokenizer(NullLogger<SentencePieceTokenizer>.Instance, "test-model", tempDir);
            var vocabSize = tokenizer.VocabularySize;
            
            // Valid tokens
            var validTokens = new long[] { 0, vocabSize / 2, vocabSize - 1 };
            Assert.True(tokenizer.ValidateTokenIds(validTokens));
            
            // Invalid: token >= vocabulary size
            var invalidTokens = new long[] { 0, vocabSize };
            Assert.False(tokenizer.ValidateTokenIds(invalidTokens));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BertWordPieceTokenizer_ThrowsOn_MissingVocabFile()
    {
        var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "vocab.txt");

        Assert.Throws<FileNotFoundException>(() =>
            new BertWordPieceTokenizer(NullLogger<BertWordPieceTokenizer>.Instance, "test-model", nonexistentPath)
        );
    }

    [Fact]
    public void SentencePieceTokenizer_ThrowsOn_InvalidModelDirectory()
    {
        var nonexistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Assert.Throws<DirectoryNotFoundException>(() =>
            new SentencePieceTokenizer(NullLogger<SentencePieceTokenizer>.Instance, "test-model", nonexistentDir)
        );
    }

    [Fact]
    public void EmbeddingTokenizerRegistry_RegistersAndRetrievesTokenizers()
    {
        var loggerFactory = new NullLoggerFactory();
        var registry = new EmbeddingTokenizerRegistry(loggerFactory);

        var testTokenizer = new StubEmbeddingTokenizer();
        registry.Register("test-model", () => testTokenizer);

        Assert.True(registry.IsRegistered("test-model"));
        var retrieved = registry.GetTokenizer("test-model");
        Assert.NotNull(retrieved);
        Assert.Equal("test-model", retrieved.ModelId);
    }

    [Fact]
    public void EmbeddingTokenizerRegistry_ThrowsOn_UnregisteredModel()
    {
        var loggerFactory = new NullLoggerFactory();
        var registry = new EmbeddingTokenizerRegistry(loggerFactory);

        Assert.Throws<KeyNotFoundException>(() => registry.GetTokenizer("unknown-model"));
    }

    private sealed class StubEmbeddingTokenizer : IEmbeddingTokenizer
    {
        public string Name => "StubTokenizer";
        public string ModelId => "test-model";
        public int VocabularySize => 1000;

        public long[] Tokenize(string text) => new long[] { 1, 2, 3 };
        public bool ValidateTokenIds(long[] tokenIds) => tokenIds.All(id => id >= 0 && id < VocabularySize);
        public IReadOnlyDictionary<string, int> GetSpecialTokens() => new Dictionary<string, int>();
    }
}
