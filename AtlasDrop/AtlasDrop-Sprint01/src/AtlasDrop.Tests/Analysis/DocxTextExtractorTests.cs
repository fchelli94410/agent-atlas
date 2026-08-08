using Xunit;
using AtlasDrop.Analysis.Office;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AtlasDrop.Tests.Analysis;

public sealed class DocxTextExtractorTests
{
    [Theory]
    [InlineData("document.docx", true)]
    [InlineData("DOCUMENT.DOCX", true)]
    [InlineData("document.doc", false)]
    [InlineData("document.pdf", false)]
    public void CanHandle_only_docx(string fileName, bool expected)
    {
        var extractor = new DocxTextExtractor();
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_paragraph_text()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "test.docx");
            CreateDocx(file,
                new Paragraph(new Run(new Text("Facture EDF"))),
                new Paragraph(new Run(new Text("Courbevoie 2026"))));

            var extractor = new DocxTextExtractor();
            var result = await extractor.ExtractAsync(file);

            Assert.Contains("Facture EDF", result.Text);
            Assert.Contains("Courbevoie 2026", result.Text);
            Assert.Equal("openxml-docx", result.DetectedEncoding);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Preserves_tabs_and_breaks()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "format.docx");

            var run = new Run();
            run.Append(new Text("A"));
            run.Append(new TabChar());
            run.Append(new Text("B"));
            run.Append(new Break());
            run.Append(new Text("C"));

            CreateDocx(file, new Paragraph(run));

            var result = await new DocxTextExtractor().ExtractAsync(file);

            Assert.Contains("A\tB", result.Text);
            Assert.Contains("C", result.Text);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Large_document_is_truncated()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "large.docx");
            CreateDocx(
                file,
                new Paragraph(new Run(new Text(new string('X', 5000)))));

            var extractor = new DocxTextExtractor(maxCharacters: 100);
            var result = await extractor.ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.True(result.Text.Length <= 100);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_docx_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.docx");

        var extractor = new DocxTextExtractor();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractAsync(missing));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "fake.txt");
            await File.WriteAllTextAsync(file, "fake");

            var extractor = new DocxTextExtractor();

            await Assert.ThrowsAsync<NotSupportedException>(
                () => extractor.ExtractAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "cancel.docx");
            CreateDocx(
                file,
                new Paragraph(new Run(new Text(new string('A', 1000)))));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var extractor = new DocxTextExtractor();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => extractor.ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Original_docx_is_not_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "readonly.docx");
            CreateDocx(file, new Paragraph(new Run(new Text("Original"))));

            var beforeLength = new FileInfo(file).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(file);

            var extractor = new DocxTextExtractor();
            _ = await extractor.ExtractAsync(file);

            Assert.True(File.Exists(file));
            Assert.Equal(beforeLength, new FileInfo(file).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static void CreateDocx(string path, params Paragraph[] paragraphs)
    {
        using var document = WordprocessingDocument.Create(
            path,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        foreach (var paragraph in paragraphs)
            body.Append(paragraph);

        mainPart.Document.Append(body);
        mainPart.Document.Save();
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
