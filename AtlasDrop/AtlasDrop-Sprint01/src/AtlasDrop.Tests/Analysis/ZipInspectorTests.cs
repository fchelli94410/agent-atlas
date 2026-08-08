using System.IO.Compression;
using Xunit;
using AtlasDrop.Analysis.Archive;

namespace AtlasDrop.Tests.Analysis;

public sealed class ZipInspectorTests
{
    [Theory]
    [InlineData("archive.zip", true)]
    [InlineData("ARCHIVE.ZIP", true)]
    [InlineData("archive.rar", false)]
    [InlineData("archive.7z", false)]
    public void CanHandle_only_zip(string fileName, bool expected)
    {
        var inspector = new ZipInspector();
        Assert.Equal(expected, inspector.CanHandle(fileName));
    }

    [Fact]
    public async Task Normal_zip_is_safe()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "normal.zip");
            CreateZip(zip, ("docs/facture.txt", "EDF Courbevoie"));

            var result = await new ZipInspector().InspectAsync(zip);

            Assert.True(result.IsSafe);
            Assert.Single(result.Entries);
            Assert.Empty(result.Warnings);
            Assert.Equal("docs/facture.txt", result.Entries[0].FullName);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Parent_traversal_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "traversal.zip");

            using (var fs = File.Create(zip))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../evil.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("evil");
            }

            var result = await new ZipInspector().InspectAsync(zip);

            Assert.False(result.IsSafe);
            Assert.Contains(result.Warnings, x => x.Contains("suspect", StringComparison.OrdinalIgnoreCase));
            Assert.True(result.Entries[0].IsSuspicious);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Absolute_path_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "absolute.zip");

            using (var fs = File.Create(zip))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("/etc/evil.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("evil");
            }

            var result = await new ZipInspector().InspectAsync(zip);

            Assert.False(result.IsSafe);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Entry_limit_is_enforced()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "many.zip");

            using (var fs = File.Create(zip))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                for (var i = 0; i < 5; i++)
                {
                    var entry = archive.CreateEntry($"file-{i}.txt");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("x");
                }
            }

            var result = await new ZipInspector(maxEntries: 3).InspectAsync(zip);

            Assert.False(result.IsSafe);
            Assert.Equal(3, result.Entries.Count);
            Assert.Contains(result.Warnings, x => x.Contains("Nombre maximal", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Uncompressed_size_limit_is_enforced()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "large.zip");
            CreateZip(zip, ("large.txt", new string('A', 5000)));

            var result = await new ZipInspector(
                maxTotalUncompressedBytes: 1000,
                maxCompressionRatio: 10_000).InspectAsync(zip);

            Assert.False(result.IsSafe);
            Assert.Contains(result.Warnings, x => x.Contains("Taille décompressée", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Suspicious_compression_ratio_is_detected()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "bomb.zip");
            CreateZip(zip, ("bomb.txt", new string('A', 100_000)));

            var result = await new ZipInspector(
                maxCompressionRatio: 5).InspectAsync(zip);

            Assert.False(result.IsSafe);
            Assert.Contains(result.Warnings, x => x.Contains("Ratio de compression", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_zip_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.zip");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new ZipInspector().InspectAsync(missing));
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "cancel.zip");
            CreateZip(zip, ("a.txt", "A"));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new ZipInspector().InspectAsync(zip, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Inspection_does_not_extract_files()
    {
        var root = CreateTempRoot();

        try
        {
            var zip = Path.Combine(root, "inspect.zip");
            CreateZip(zip, ("nested/file.txt", "secret"));

            var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories);

            _ = await new ZipInspector().InspectAsync(zip);

            var after = Directory.GetFiles(root, "*", SearchOption.AllDirectories);

            Assert.Equal(before.OrderBy(x => x), after.OrderBy(x => x));
            Assert.DoesNotContain(after, x => x.EndsWith("file.txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static void CreateZip(string path, params (string Name, string Content)[] files)
    {
        using var fs = File.Create(path);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        foreach (var file in files)
        {
            var entry = archive.CreateEntry(
                file.Name,
                CompressionLevel.Optimal);

            using var writer = new StreamWriter(entry.Open());
            writer.Write(file.Content);
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
