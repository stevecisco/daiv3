using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Daiv3.Knowledge.DocProc;

public sealed class DocumentTextExtractor : ITextExtractor
{
    private static readonly Regex ScriptRegex = new(
        "<script[^>]*>.*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        "<style[^>]*>.*?</style>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(ILogger<DocumentTextExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" => await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false),
            ".md" => await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false),
            ".html" => await ExtractHtmlAsync(filePath, cancellationToken).ConfigureAwait(false),
            ".htm" => await ExtractHtmlAsync(filePath, cancellationToken).ConfigureAwait(false),
            ".docx" => ExtractDocx(filePath),
            ".pdf" => ExtractPdf(filePath, cancellationToken),
            _ => throw new NotSupportedException($"Document type not supported: {extension}")
        };
    }

    private static async Task<string> ExtractHtmlAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return ExtractTextFromHtml(content);
    }

    private static string ExtractTextFromHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var withoutScript = ScriptRegex.Replace(content, " ");
        var withoutStyle = StyleRegex.Replace(withoutScript, " ");
        var withoutTags = TagRegex.Replace(withoutStyle, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return NormalizeWhitespace(decoded);
    }

    private static string ExtractDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var paragraphs = body
            .Elements<Paragraph>()
            .Select(p => p.InnerText)
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private string ExtractPdf(string filePath, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                builder.AppendLine(page.Text);
            }
        }

        var text = NormalizeWhitespace(builder.ToString());
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("PDF extraction returned no text for {Path}.", filePath);
        }

        return text;
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(text, " ").Trim();
    }
}
