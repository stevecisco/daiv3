using Daiv3.Persistence.Entities;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository interface for managing GlossaryEntry entities.
/// Provides data access for glossary terms, definitions, and related terms.
/// Implements GLO-DATA-001: Glossary entries SHALL include term, definition, and related terms.
/// </summary>
public interface IGlossaryRepository : IRepository<GlossaryEntry>
{
    /// <summary>
    /// Gets a glossary entry by term.
    /// </summary>
    /// <param name="term">The term to search for (case-insensitive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The glossary entry if found; null otherwise.</returns>
    Task<GlossaryEntry?> GetByTermAsync(string term, CancellationToken ct = default);

    /// <summary>
    /// Gets all glossary entries by category.
    /// Useful for organizing glossary entries in the UI by topic.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., 'Architecture', 'Knowledge', 'Models').</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All glossary entries in the specified category.</returns>
    Task<IReadOnlyList<GlossaryEntry>> GetByCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Gets all unique categories in the glossary.
    /// Used for building category navigation in the UI.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all distinct categories.</returns>
    Task<IReadOnlyList<string>> GetAllCategoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Searches glossary entries by term prefix (for autocomplete).
    /// </summary>
    /// <param name="termPrefix">Part of the term to search for (case-insensitive).</param>
    /// <param name="maxResults">Maximum number of results to return (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of glossary entries matching the prefix.</returns>
    Task<IReadOnlyList<GlossaryEntry>> SearchByTermPrefixAsync(string termPrefix, int maxResults = 10, CancellationToken ct = default);

    /// <summary>
    /// Searches glossary entries by definition content.
    /// </summary>
    /// <param name="searchText">Text to search for in definitions (case-insensitive partial match).</param>
    /// <param name="maxResults">Maximum number of results to return (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of glossary entries with definitions matching the search text.</returns>
    Task<IReadOnlyList<GlossaryEntry>> SearchDefinitionAsync(string searchText, int maxResults = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of glossary entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of glossary entries.</returns>
    Task<int> GetCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of glossary entries in a specific category.
    /// </summary>
    /// <param name="category">The category to count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of entries in the category.</returns>
    Task<int> GetCountByCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Gets glossary entries that were created or updated within a date range.
    /// Useful for auditing changes to terminology.
    /// </summary>
    /// <param name="afterDate">Unix timestamp - entries after this date are returned.</param>
    /// <param name="beforeDate">Unix timestamp - entries before this date are returned (can be null for "to present").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All glossary entries modified within the date range.</returns>
    Task<IReadOnlyList<GlossaryEntry>> GetModifiedDateRangeAsync(long afterDate, long? beforeDate = null, CancellationToken ct = default);
}
