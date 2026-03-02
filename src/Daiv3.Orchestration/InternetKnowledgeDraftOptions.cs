namespace Daiv3.Orchestration;

/// <summary>
/// Configuration options for Internet-level knowledge promotion draft artifacts.
/// </summary>
public class InternetKnowledgeDraftOptions
{
    /// <summary>
    /// Output directory for generated draft artifacts.
    /// Defaults to %LOCALAPPDATA%\Daiv3\drafts\knowledge-promotions.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Daiv3",
        "drafts",
        "knowledge-promotions");

    /// <summary>
    /// Maximum description characters written per learning in the artifact.
    /// </summary>
    public int MaxDescriptionLength { get; set; } = 280;
}
