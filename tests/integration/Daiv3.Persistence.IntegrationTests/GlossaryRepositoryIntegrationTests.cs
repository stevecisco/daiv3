using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

#pragma warning disable IDISP006 // Test classes don't need to implement IDisposable

namespace Daiv3.Persistence.Tests.Integration;

/// <summary>
/// Integration tests for GlossaryRepository against SQLite database.
/// Validates persistence, migration, and complex query scenarios.
/// Implements GLO-DATA-001: Glossary entries SHALL include term, definition, and related terms.
/// </summary>
public class GlossaryRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly IGlossaryRepository _repository;
    private readonly ILogger<GlossaryRepositoryIntegrationTests> _logger;
    private readonly string _testDbPath;

    public GlossaryRepositoryIntegrationTests()
    {
        // Use a temporary database file for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"glossary-integration-test-{Guid.NewGuid()}.db");

        var services = new ServiceCollection();
        services.AddLogging();

        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabasePath = _testDbPath;
        });

        services.AddPersistence();

        _serviceProvider = services.BuildServiceProvider();
        _databaseContext = _serviceProvider.GetRequiredService<IDatabaseContext>();
        _repository = _serviceProvider.GetRequiredService<IGlossaryRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<GlossaryRepositoryIntegrationTests>>();
    }

    public async Task InitializeAsync()
    {
        await _databaseContext.InitializeAsync();
        _logger.LogInformation("Glossary integration test database initialized");
    }

    public async Task DisposeAsync()
    {
        await _databaseContext.DisposeAsync();

        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static GlossaryEntry CreateTestEntry(
        string term, 
        string definition, 
        string? category = null,
        string? relatedTermsJson = null,
        string? notes = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new GlossaryEntry
        {
            GlossaryId = Guid.NewGuid().ToString(),
            Term = term,
            Definition = definition,
            Category = category,
            RelatedTermsJson = relatedTermsJson,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "integration-test",
            UpdatedBy = "integration-test"
        };
    }

    [Fact]
    public async Task AddAsync_RoundTrip_PersistsAllFields()
    {
        // Arrange
        var entry = CreateTestEntry(
            term: "Embedding Model",
            definition: "ML model that converts text to vector embeddings.",
            category: "AI",
            relatedTermsJson: "[\"Vector\", \"Encoding\"]",
            notes: "Used for semantic search in knowledge management."
        );

        // Act
        var id = await _repository.AddAsync(entry, CancellationToken.None);
        var retrieved = await _repository.GetByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Embedding Model", retrieved.Term);
        Assert.Equal("ML model that converts text to vector embeddings.", retrieved.Definition);
        Assert.Equal("AI", retrieved.Category);
        Assert.Equal("[\"Vector\", \"Encoding\"]", retrieved.RelatedTermsJson);
        Assert.Equal("Used for semantic search in knowledge management.", retrieved.Notes);
        Assert.NotEqual(0, retrieved.CreatedAt);
        Assert.NotEqual(0, retrieved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesStoredData_QueryReturnsUpdatedVersion()
    {
        // Arrange
        var entry = CreateTestEntry("Term A", "Original definition.");
        var id = await _repository.AddAsync(entry, CancellationToken.None);

        entry.Definition = "Updated definition with more details.";
        entry.Category = "Knowledge";
        entry.RelatedTermsJson = "[\"Related\"]";
        entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        entry.UpdatedBy = "integration-test-updater";

        // Act
        await _repository.UpdateAsync(entry, CancellationToken.None);
        var retrieved = await _repository.GetByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Updated definition with more details.", retrieved.Definition);
        Assert.Equal("Knowledge", retrieved.Category);
        Assert.Equal("integration-test-updater", retrieved.UpdatedBy);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromDatabase_SubsequentQueryReturnsNull()
    {
        // Arrange
        var entry = CreateTestEntry("Temporary Term", "This will be deleted.");
        var id = await _repository.AddAsync(entry, CancellationToken.None);
        var beforeDelete = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(beforeDelete);

        // Act
        await _repository.DeleteAsync(id, CancellationToken.None);
        var afterDelete = await _repository.GetByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task GetByTermAsync_UniquenessConstraint_OnlyRetrievesCorrectEntry()
    {
        // Arrange
        var term1 = CreateTestEntry("UNIQUE_TERM_1", "First definition.");
        var term2 = CreateTestEntry("UNIQUE_TERM_2", "Second definition.");
        await _repository.AddAsync(term1, CancellationToken.None);
        await _repository.AddAsync(term2, CancellationToken.None);

        // Act
        var retrieved = await _repository.GetByTermAsync("UNIQUE_TERM_1", CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("UNIQUE_TERM_1", retrieved.Term);
        Assert.Equal("First definition.", retrieved.Definition);
    }

    [Fact]
    public async Task MultipleEntries_CategoryIndexing_EfficientRetrieval()
    {
        // Arrange - Create entries across multiple categories
        var categories = new[] { "AI", "Knowledge", "Architecture", "Persistence" };
        var entriesPerCategory = 5;
        
        for (int c = 0; c < categories.Length; c++)
        {
            for (int i = 0; i < entriesPerCategory; i++)
            {
                var entry = CreateTestEntry(
                    $"{categories[c]}_{i:D2}",
                    $"Definition for {categories[c]} term {i}.",
                    categories[c]
                );
                await _repository.AddAsync(entry, CancellationToken.None);
            }
        }

        // Act
        var aiEntries = await _repository.GetByCategoryAsync("AI", CancellationToken.None);
        var knowledgeEntries = await _repository.GetByCategoryAsync("Knowledge", CancellationToken.None);

        // Assert
        Assert.Equal(entriesPerCategory, aiEntries.Count);
        Assert.Equal(entriesPerCategory, knowledgeEntries.Count);
        Assert.All(aiEntries, e => Assert.Equal("AI", e.Category));
        Assert.All(knowledgeEntries, e => Assert.Equal("Knowledge", e.Category));
    }

    [Fact]
    public async Task GetAllCategoriesAsync_LargeDataset_ReturnsDistinctCategoriesOnly()
    {
        // Arrange
        var categories = new[] { "Cloud", "Local", "Hybrid" };
        for (int i = 0; i < 10; i++)
        {
            foreach (var cat in categories)
            {
                var entry = CreateTestEntry($"Term_{cat}_{i}", $"Def {i}", cat);
                await _repository.AddAsync(entry, CancellationToken.None);
            }
        }

        // Act
        var allCategories = await _repository.GetAllCategoriesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, allCategories.Count);
        Assert.Contains("Cloud", allCategories);
        Assert.Contains("Local", allCategories);
        Assert.Contains("Hybrid", allCategories);
    }

    [Fact]
    public async Task SearchByTermPrefixAsync_LargeDataset_PerformsEfficiently()
    {
        // Arrange - Create terms with common prefixes
        var prefixes = new[] { "Chunk", "Vector", "Embedding", "Knowledge", "Index" };
        for (int i = 0; i < 20; i++)
        {
            foreach (var prefix in prefixes)
            {
                var entry = CreateTestEntry($"{prefix}_{i:D2}", $"Definition for {prefix} variant {i}.");
                await _repository.AddAsync(entry, CancellationToken.None);
            }
        }

        // Act
        var vectorResults = await _repository.SearchByTermPrefixAsync("Vector", 100, CancellationToken.None);
        var chunkResults = await _repository.SearchByTermPrefixAsync("Chunk", 100, CancellationToken.None);

        // Assert
        Assert.Equal(20, vectorResults.Count);
        Assert.Equal(20, chunkResults.Count);
        Assert.All(vectorResults, r => Assert.StartsWith("Vector", r.Term));
        Assert.All(chunkResults, r => Assert.StartsWith("Chunk", r.Term));
    }

    [Fact]
    public async Task SearchDefinitionAsync_FullTextPattern_MatchesAcrossEntries()
    {
        // Arrange
        await _repository.AddAsync(
            CreateTestEntry("T1", "This document contains information about vector operations."),
            CancellationToken.None
        );
        await _repository.AddAsync(
            CreateTestEntry("T2", "Vectors are used in machine learning."),
            CancellationToken.None
        );
        await _repository.AddAsync(
            CreateTestEntry("T3", "Completely unrelated topic about databases."),
            CancellationToken.None
        );

        // Act
        var results = await _repository.SearchDefinitionAsync("vector", 20, CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("vector", r.Definition, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCountAsync_EmptyDatabase_ReturnsZero()
    {
        // Act
        var count = await _repository.GetCountAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetCountAsync_PopulatedDatabase_ReturnsAccurateCount()
    {
        // Arrange
        const int expectedCount = 25;
        for (int i = 0; i < expectedCount; i++)
        {
            var entry = CreateTestEntry($"Term_{i:D3}", $"Definition {i}.");
            await _repository.AddAsync(entry, CancellationToken.None);
        }

        // Act
        var count = await _repository.GetCountAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public async Task GetCountByCategoryAsync_WithMultipleCategories_ReturnsAccurateCategoryCount()
    {
        // Arrange
        await _repository.AddAsync(CreateTestEntry("A1", "D1", "Cat1"), CancellationToken.None);
        await _repository.AddAsync(CreateTestEntry("A2", "D2", "Cat1"), CancellationToken.None);
        await _repository.AddAsync(CreateTestEntry("B1", "D3", "Cat2"), CancellationToken.None);
        await _repository.AddAsync(CreateTestEntry("B2", "D4", "Cat2"), CancellationToken.None);
        await _repository.AddAsync(CreateTestEntry("B3", "D5", "Cat2"), CancellationToken.None);

        // Act
        var cat1Count = await _repository.GetCountByCategoryAsync("Cat1", CancellationToken.None);
        var cat2Count = await _repository.GetCountByCategoryAsync("Cat2", CancellationToken.None);

        // Assert
        Assert.Equal(2, cat1Count);
        Assert.Equal(3, cat2Count);
    }

    [Fact]
    public async Task GetModifiedDateRangeAsync_DateRangeQuery_ReturnsEntriesInRange()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var entry1 = CreateTestEntry("Old Entry", "Created before range.");
        entry1.UpdatedAt = baseTime - 2000;
        var id1 = await _repository.AddAsync(entry1, CancellationToken.None);

        var entry2 = CreateTestEntry("In Range Entry", "Created within range.");
        entry2.UpdatedAt = baseTime - 500;
        var id2 = await _repository.AddAsync(entry2, CancellationToken.None);

        var entry3 = CreateTestEntry("New Entry", "Created after range.");
        entry3.UpdatedAt = baseTime + 500;
        var id3 = await _repository.AddAsync(entry3, CancellationToken.None);

        // Act
        var rangeResults = await _repository.GetModifiedDateRangeAsync(
            baseTime - 1000,
            baseTime,
            CancellationToken.None
        );

        // Assert
        Assert.Single(rangeResults);
        Assert.Equal("In Range Entry", rangeResults[0].Term);
    }

    [Fact]
    public async Task GetModifiedDateRangeAsync_OpenEnded_ReturnsAllSinceDate()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var oldEntry = CreateTestEntry("Old", "Before cutoff.");
        oldEntry.UpdatedAt = baseTime - 1000;
        await _repository.AddAsync(oldEntry, CancellationToken.None);

        var newEntry1 = CreateTestEntry("New1", "After cutoff.");
        newEntry1.UpdatedAt = baseTime - 100;
        await _repository.AddAsync(newEntry1, CancellationToken.None);

        var newEntry2 = CreateTestEntry("New2", "After cutoff.");
        newEntry2.UpdatedAt = baseTime + 100;
        await _repository.AddAsync(newEntry2, CancellationToken.None);

        // Act
        var results = await _repository.GetModifiedDateRangeAsync(
            baseTime - 500,
            beforeDate: null,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(new[] { "New1", "New2" }.Contains(r.Term)));
    }

    [Fact]
    public async Task RelatedTermsJson_PersistComplexArray_RoundTrip()
    {
        // Arrange
        var relatedTermsJson = "[\"Related Term 1\", \"Related Term 2\", \"Related Term 3\"]";
        var entry = CreateTestEntry("Main Term", "Definition.", relatedTermsJson: relatedTermsJson);

        // Act
        await _repository.AddAsync(entry, CancellationToken.None);
        var retrieved = await _repository.GetByIdAsync(entry.GlossaryId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(relatedTermsJson, retrieved.RelatedTermsJson);
    }

    [Fact]
    public async Task NotesField_PersistMultilineText_RoundTrip()
    {
        // Arrange
        var notes = "Line 1\nLine 2\nLine 3";
        var entry = CreateTestEntry("Documented Term", "Definition.", notes: notes);

        // Act
        await _repository.AddAsync(entry, CancellationToken.None);
        var retrieved = await _repository.GetByIdAsync(entry.GlossaryId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(notes, retrieved.Notes);
    }

    [Fact]
    public async Task GetAllAsync_OrderedByTerm_StableSort()
    {
        // Arrange
        var terms = new[] { "Zebra", "Alpha", "Delta", "Bravo", "Charlie" };
        foreach (var term in terms)
        {
            await _repository.AddAsync(CreateTestEntry(term, $"Def for {term}."), CancellationToken.None);
        }

        // Act
        var all = await _repository.GetAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(5, all.Count);
        var retrievedTerms = all.Select(e => e.Term).ToList();
        var expectedTerms = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Zebra" };
        for (int i = 0; i < expectedTerms.Length; i++)
        {
            Assert.Equal(expectedTerms[i], retrievedTerms[i]);
        }
    }

    [Fact]
    public async Task ConcurrentOperations_AddAndRead_MaintainsDataIntegrity()
    {
        // Arrange
        var tasks = new List<Task>();
        
        // Add 10 entries concurrently
        for (int i = 0; i < 10; i++)
        {
            int index = i; // Capture for closure
            tasks.Add(_repository.AddAsync(
                CreateTestEntry($"Concurrent_{index:D2}", $"Definition {index}."),
                CancellationToken.None
            ));
        }

        // Act
        await Task.WhenAll(tasks);
        var allEntries = await _repository.GetAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(10, allEntries.Count);
        Assert.Equal(10, await _repository.GetCountAsync(CancellationToken.None));
    }
}
