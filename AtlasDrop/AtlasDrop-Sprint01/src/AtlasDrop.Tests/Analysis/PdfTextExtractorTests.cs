using Xunit;
using AtlasDrop.Analysis.Pdf;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace AtlasDrop.Tests.Analysis;

public sealed class PdfTextExtractorTests
{
    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("DOCUMENT.PDF", true)]
    [InlineData("document.docx", false)]
    [InlineData("document.txt", false)]
    public void CanHandle_only_pdf(string fileName, bool expected)
    {
        var extractor = new PdfTextExtractor();
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_text_from_pdf()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "test.pdf");
            CreatePdf(file, "Facture EDF - Courbevoie 2026");

            var result = await new PdfTextExtractor().ExtractAsync(file);

            Assert.Contains("[Page 1]", result.Text);
            Assert.Contains("Facture EDF", result.Text);
            Assert.Contains("Courbevoie 2026", result.Text);
            Assert.Equal("pdf-text", result.DetectedEncoding);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Character_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "large.pdf");
            CreatePdf(file, new string('X', 1000));

            var result = await new PdfTextExtractor(maxCharacters: 100).ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.True(result.Text.Length <= 100);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_pdf_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.pdf");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new PdfTextExtractor().ExtractAsync(missing));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "fake.txt");
            await File.WriteAllTextAsync(file, "fake");

            await Assert.ThrowsAsync<NotSupportedException>(
                () => new PdfTextExtractor().ExtractAsync(file));
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
            var file = Path.Combine(root, "cancel.pdf");
            CreatePdf(file, "Atlas Drop");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new PdfTextExtractor().ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Original_pdf_is_not_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "readonly.pdf");
            CreatePdf(file, "Original");

            var beforeLength = new FileInfo(file).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(file);

            _ = await new PdfTextExtractor().ExtractAsync(file);

            Assert.Equal(beforeLength, new FileInfo(file).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static void CreatePdf(string path, string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);

        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);

        File.WriteAllBytes(path, builder.Build());
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
