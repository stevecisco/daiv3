namespace Daiv3.ModelExecution.Models;

/// <summary>
/// Known task types for model selection and priority assignment.
/// </summary>
public enum TaskType
{
    /// <summary>Unknown or unclassified task type (default fallback).</summary>
    Unknown = 0,

    /// <summary>Interactive chat conversation.</summary>
    Chat = 1,

    /// <summary>Search for information in knowledge base.</summary>
    Search = 2,

    /// <summary>Summarize document or content.</summary>
    Summarize = 3,

    /// <summary>Code generation, analysis, or refactoring.</summary>
    Code = 4,

    /// <summary>Question answering.</summary>
    QuestionAnswer = 5,

    /// <summary>Content rewriting or editing.</summary>
    Rewrite = 6,

    /// <summary>Translation between languages.</summary>
    Translation = 7,

    /// <summary>Analysis of data or content.</summary>
    Analysis = 8,

    /// <summary>Generate structured data or content.</summary>
    Generation = 9,

    /// <summary>Extract information from content.</summary>
    Extraction = 10
}
