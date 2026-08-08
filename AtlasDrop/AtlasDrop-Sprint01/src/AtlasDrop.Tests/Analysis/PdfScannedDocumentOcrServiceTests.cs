using Xunit;
using AtlasDrop.Analysis.Ocr;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Tests.Analysis;

public sealed class PdfScannedDocumentOcrServiceTests
{
    [Fact]
    public async Task Ocr_combines_rendered_pages()
    {
        var root = CreateTempRoot();

        try
        {
            var pdf = Path.Combine(root, "scan.pdf");
            await File.WriteAllBytesAsync(pdf, new byte[] { 1, 2, 3 });

            var renderer = new FakeRenderer(2);
            var ocr = new FakeImageOcrService();

            var service = new PdfScannedDocumentOcrService(
                renderer,
                ocr,
                maxPages: 5);

            var result = await service.ExtractAsync(pdf);

            Assert.Contains("[Page OCR 1]", result.Text);
            Assert.Contains("[Page OCR 2]", result.Text);
            Assert.Contains("Texte page 1", result.Text);
            Assert.Contains("Texte page 2", result.Text);
            Assert.Equal("pdf-ocr", result.DetectedEncoding);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Max_pages_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var pdf = Path.Combine(root, "scan.pdf");
            await File.WriteAllBytesAsync(pdf, new byte[] { 1 });

            var renderer = new FakeRenderer(10);
            var ocr = new FakeImageOcrService();

            var service = new PdfScannedDocumentOcrService(
                renderer,
                ocr,
                maxPages: 2);

            var result = await service.ExtractAsync(pdf);

            Assert.Contains("[Page OCR 2]", result.Text);
            Assert.DoesNotContain("[Page OCR 3]", result.Text);
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
            var pdf = Path.Combine(root, "scan.pdf");
            await File.WriteAllBytesAsync(pdf, new byte[] { 1 });

            var renderer = new FakeRenderer(1);
            var ocr = new FakeImageOcrService(new string('X', 1000));

            var service = new PdfScannedDocumentOcrService(
                renderer,
                ocr,
                maxCharacters: 100);

            var result = await service.ExtractAsync(pdf);

            Assert.True(result.WasTruncated);
            Assert.True(result.Text.Length <= 100);
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
            var pdf = Path.Combine(root, "scan.pdf");
            await File.WriteAllBytesAsync(pdf, new byte[] { 1 });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var service = new PdfScannedDocumentOcrService(
                new FakeRenderer(1),
                new FakeImageOcrService());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.ExtractAsync(pdf, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Pdf_source_is_not_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var pdf = Path.Combine(root, "scan.pdf");
            await File.WriteAllBytesAsync(pdf, new byte[] { 1, 2, 3, 4 });

            var beforeLength = new FileInfo(pdf).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(pdf);

            var service = new PdfScannedDocumentOcrService(
                new FakeRenderer(1),
                new FakeImageOcrService());

            _ = await service.ExtractAsync(pdf);

            Assert.Equal(beforeLength, new FileInfo(pdf).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(pdf));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private sealed class FakeRenderer : IPdfPageImageRenderer
    {
        private readonly int _pageCount;

        public FakeRenderer(int pageCount) => _pageCount = pageCount;

        public async Task<IReadOnlyList<string>> RenderPagesAsync(
            string pdfPath,
            string outputDirectory,
            int maxPages,
            CancellationToken cancellationToken)
        {
            var files = new List<string>();

            for (var i = 1; i <= Math.Min(_pageCount, maxPages); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = Path.Combine(outputDirectory, $"page-{i}.png");
                await File.WriteAllBytesAsync(file, new byte[] { 1 }, cancellationToken);
                files.Add(file);
            }

            return files;
        }
    }

    private sealed class FakeImageOcrService : IImageOcrService
    {
        private readonly string? _fixedText;
        private int _counter;

        public FakeImageOcrService(string? fixedText = null)
        {
            _fixedText = fixedText;
        }

        public bool CanHandle(string filePath) => true;

        public Task<OcrResult> RecognizeAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _counter++;
            return Task.FromResult(
                new OcrResult(
                    _fixedText ?? $"Texte page {_counter}",
                    0.9f,
                    "fra",
                    false));
        }
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
