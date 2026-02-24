using System.Text;
using Daiv3.Knowledge.DocProc;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.DocProc;

public sealed class DocumentTextExtractorTests : IDisposable
{
    private readonly string _tempDir;

    public DocumentTextExtractorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"daiv3_doc_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractAsync_Txt_ReturnsContent()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreateTextFile(".txt", "Plain text content");

        var result = await extractor.ExtractAsync(path);

        Assert.Equal("Plain text content", result);
    }

    [Fact]
    public async Task ExtractAsync_Markdown_ReturnsContent()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreateTextFile(".md", "# Title\nSome markdown content.");

        var result = await extractor.ExtractAsync(path);

        Assert.Contains("Title", result);
        Assert.Contains("markdown content", result);
    }

    [Fact]
    public async Task ExtractAsync_Html_StripsTags()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreateTextFile(".html", "<html><body><p>Hello <b>World</b></p></body></html>");

        var result = await extractor.ExtractAsync(path);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public async Task ExtractAsync_Docx_ReturnsContent()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreateDocxFile("Docx content for extraction");

        var result = await extractor.ExtractAsync(path);

        Assert.Contains("Docx content for extraction", result);
    }

    [Fact]
    public async Task ExtractAsync_Pdf_ReturnsContent()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreatePdfFile("Hello PDF");

        var result = await extractor.ExtractAsync(path);

        Assert.Contains("Hello PDF", result);
    }

    [Fact]
    public async Task ExtractAsync_UnsupportedExtension_Throws()
    {
        var extractor = new DocumentTextExtractor(NullLogger<DocumentTextExtractor>.Instance);
        var path = CreateTextFile(".bin", "binary");

        await Assert.ThrowsAsync<NotSupportedException>(() => extractor.ExtractAsync(path));
    }

    private string CreateTextFile(string extension, string content)
    {
        var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string CreateDocxFile(string content)
    {
        var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.docx");

        using (var document = WordprocessingDocument.Create(filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            body.AppendChild(new Paragraph(new Run(new Text(content))));
            mainPart.Document.Append(body);
        }

        return filePath;
    }

    private string CreatePdfFile(string text)
    {
        var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.pdf");
        var pdfBytes = BuildSimplePdf(text);
        File.WriteAllBytes(filePath, pdfBytes);
        return filePath;
    }

    private static byte[] BuildSimplePdf(string text)
    {
        var escapedText = EscapePdfString(text);
        var content = $"BT\n/F1 24 Tf\n72 100 Td\n({escapedText}) Tj\nET\n";

        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
        };

        var parts = new List<string>();
        var offsets = new List<int> { 0 };

        var header = "%PDF-1.4\n";
        parts.Add(header);
        var offset = header.Length;

        foreach (var obj in objects)
        {
            offsets.Add(offset);
            parts.Add(obj);
            offset += obj.Length;
        }

        var xrefStart = offset;
        var xrefBuilder = new StringBuilder();
        xrefBuilder.Append("xref\n");
        xrefBuilder.Append($"0 {objects.Count + 1}\n");
        xrefBuilder.Append("0000000000 65535 f \n");

        for (var i = 1; i <= objects.Count; i++)
        {
            xrefBuilder.Append($"{offsets[i]:0000000000} 00000 n \n");
        }

        xrefBuilder.Append("trailer\n");
        xrefBuilder.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        xrefBuilder.Append("startxref\n");
        xrefBuilder.Append(xrefStart);
        xrefBuilder.Append("\n%%EOF\n");

        parts.Add(xrefBuilder.ToString());

        var combined = string.Concat(parts);
        return Encoding.ASCII.GetBytes(combined);
    }

    private static string EscapePdfString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}
