using Xunit;
using AtlasDrop.Analysis.Ocr;

namespace AtlasDrop.Tests.Analysis;

public sealed class TesseractImageOcrServiceTests
{
    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.JPEG", true)]
    [InlineData("scan.png", true)]
    [InlineData("scan.tif", true)]
    [InlineData("scan.tiff", true)]
    [InlineData("document.pdf", false)]
    public void CanHandle_supported_images(string fileName, bool expected)
    {
        var root = CreateTempRoot();

        try
        {
            var service = new TesseractImageOcrService(root, backend: new FakeBackend());
            Assert.Equal(expected, service.CanHandle(fileName));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Recognize_returns_text_and_confidence()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.png");
            await File.WriteAllBytesAsync(image, new byte[] { 1, 2, 3 });

            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");

            var service = new TesseractImageOcrService(
                root,
                "fra",
                backend: new FakeBackend("Facture EDF Courbevoie", 0.91f));

            var result = await service.RecognizeAsync(image);

            Assert.Equal("Facture EDF Courbevoie", result.Text);
            Assert.Equal(0.91f, result.Confidence);
            Assert.Equal("fra", result.Languages);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task French_and_english_are_supported()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.jpg");
            await File.WriteAllBytesAsync(image, new byte[] { 1 });

            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");
            File.WriteAllText(Path.Combine(root, "eng.traineddata"), "fake");

            var service = new TesseractImageOcrService(
                root,
                "fra+eng",
                backend: new FakeBackend("Invoice Facture", 0.8f));

            var result = await service.RecognizeAsync(image);

            Assert.Equal("fra+eng", result.Languages);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_language_data_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.png");
            await File.WriteAllBytesAsync(image, new byte[] { 1 });

            var service = new TesseractImageOcrService(
                root,
                "fra",
                backend: new FakeBackend());

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.RecognizeAsync(image));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "document.txt");
            await File.WriteAllTextAsync(file, "fake");
            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");

            var service = new TesseractImageOcrService(
                root,
                backend: new FakeBackend());

            await Assert.ThrowsAsync<NotSupportedException>(
                () => service.RecognizeAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Long_ocr_text_is_truncated()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.png");
            await File.WriteAllBytesAsync(image, new byte[] { 1 });
            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");

            var service = new TesseractImageOcrService(
                root,
                maxCharacters: 10,
                backend: new FakeBackend(new string('X', 100), 0.5f));

            var result = await service.RecognizeAsync(image);

            Assert.True(result.WasTruncated);
            Assert.Equal(10, result.Text.Length);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_is_respected_before_ocr()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.png");
            await File.WriteAllBytesAsync(image, new byte[] { 1 });
            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var service = new TesseractImageOcrService(
                root,
                backend: new FakeBackend());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.RecognizeAsync(image, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Source_image_is_never_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var image = Path.Combine(root, "scan.png");
            await File.WriteAllBytesAsync(image, new byte[] { 1, 2, 3, 4 });
            File.WriteAllText(Path.Combine(root, "fra.traineddata"), "fake");

            var beforeLength = new FileInfo(image).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(image);

            var service = new TesseractImageOcrService(
                root,
                backend: new FakeBackend("Texte", 0.7f));

            _ = await service.RecognizeAsync(image);

            Assert.Equal(beforeLength, new FileInfo(image).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(image));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private sealed class FakeBackend : ITesseractBackend
    {
        private readonly string _text;
        private readonly float _confidence;

        public FakeBackend(string text = "Texte OCR", float confidence = 0.75f)
        {
            _text = text;
            _confidence = confidence;
        }

        public OcrResultData Recognize(
            string filePath,
            string tessDataPath,
            string languages,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new OcrResultData(_text, _confidence);
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
