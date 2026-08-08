using System.Text;
using Xunit;
using AtlasDrop.Analysis.Text;

namespace AtlasDrop.Tests.Analysis;

public sealed class TextFileExtractorTests
{
    static TextFileExtractorTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Theory]
    [InlineData("document.txt", true)]
    [InlineData("data.csv", true)]
    [InlineData("DATA.CSV", true)]
    [InlineData("document.pdf", false)]
    [InlineData("", false)]
    public void CanHandle_is_extension_based(string fileName, bool expected)
    {
        var extractor = new TextFileExtractor();
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_utf8_text()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "test.txt");
            await File.WriteAllTextAsync(
                file,
                "Facture EDF - Courbevoie - août 2026",
                new UTF8Encoding(false));

            var extractor = new TextFileExtractor();
            var result = await extractor.ExtractAsync(file);

            Assert.Contains("Courbevoie", result.Text);
            Assert.Contains("août", result.Text);
            Assert.Equal("utf-8", result.DetectedEncoding);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Extracts_utf8_bom_text_without_bom_character()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "bom.txt");
            await File.WriteAllTextAsync(
                file,
                "Bonjour Atlas",
                new UTF8Encoding(true));

            var extractor = new TextFileExtractor();
            var result = await extractor.ExtractAsync(file);

            Assert.Equal("Bonjour Atlas", result.Text);
            Assert.DoesNotContain('\uFEFF', result.Text);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Extracts_windows_1252_csv()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "factures.csv");
            var encoding = Encoding.GetEncoding(1252);
            var bytes = encoding.GetBytes("Ville;Montant\r\nMontévrain;125,50");
            await File.WriteAllBytesAsync(file, bytes);

            var extractor = new TextFileExtractor();
            var result = await extractor.ExtractAsync(file);

            Assert.Contains("Montévrain", result.Text);
            Assert.Equal("windows-1252", result.DetectedEncoding);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Extracts_utf16_text()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "unicode.txt");
            await File.WriteAllTextAsync(
                file,
                "Le Havre",
                Encoding.Unicode);

            var extractor = new TextFileExtractor();
            var result = await extractor.ExtractAsync(file);

            Assert.Equal("Le Havre", result.Text);
            Assert.Equal("utf-16", result.DetectedEncoding);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Large_file_is_truncated_safely()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "large.txt");
            await File.WriteAllTextAsync(file, new string('A', 5000));

            var extractor = new TextFileExtractor(maxBytes: 100);
            var result = await extractor.ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.Equal(100, result.BytesRead);
            Assert.Equal(100, result.Text.Length);
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
            var file = Path.Combine(root, "test.pdf");
            await File.WriteAllTextAsync(file, "fake");

            var extractor = new TextFileExtractor();

            await Assert.ThrowsAsync<NotSupportedException>(
                () => extractor.ExtractAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_file_is_rejected()
    {
        var extractor = new TextFileExtractor();
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractAsync(missing));
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "cancel.txt");
            await File.WriteAllTextAsync(file, new string('A', 10000));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var extractor = new TextFileExtractor();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => extractor.ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
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
