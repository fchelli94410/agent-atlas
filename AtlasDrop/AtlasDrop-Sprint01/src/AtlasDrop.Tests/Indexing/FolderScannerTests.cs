using Xunit;
using AtlasDrop.Infrastructure.Indexing;

namespace AtlasDrop.Tests.Indexing;

public sealed class FolderScannerTests
{
    [Fact]
    public async Task Scan_returns_root_and_children()
    {
        var root = CreateTree();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "A"));
            Directory.CreateDirectory(Path.Combine(root, "A", "B"));

            var scanner = new FolderScanner();
            var result = await scanner.ScanAsync(root);

            Assert.Equal(3, result.Count);
            Assert.Contains(result, x => x.FullPath == root && x.Depth == 0);
            Assert.Contains(result, x => x.Name == "A" && x.Depth == 1);
            Assert.Contains(result, x => x.Name == "B" && x.Depth == 2);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Scan_counts_files_and_extensions()
    {
        var root = CreateTree();

        try
        {
            File.WriteAllText(Path.Combine(root, "a.pdf"), "x");
            File.WriteAllText(Path.Combine(root, "b.PDF"), "x");
            File.WriteAllText(Path.Combine(root, "c.txt"), "x");

            var scanner = new FolderScanner();
            var result = await scanner.ScanAsync(root);

            var rootItem = Assert.Single(result);
            Assert.Equal(3, rootItem.FileCount);
            Assert.Contains(".pdf", rootItem.FileExtensions);
            Assert.Contains(".txt", rootItem.FileExtensions);
            Assert.Equal(2, rootItem.FileExtensions.Count);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Scan_reports_progress()
    {
        var root = CreateTree();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "A"));
            Directory.CreateDirectory(Path.Combine(root, "B"));

            var seen = new List<string>();
            var progress = new InlineProgress<AtlasDrop.Core.Indexing.FolderScanProgress>(
                x => seen.Add(x.CurrentPath));

            var scanner = new FolderScanner();
            await scanner.ScanAsync(root, progress);

            Assert.Equal(3, seen.Count);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Scan_supports_cancellation()
    {
        var root = CreateTree();

        try
        {
            for (var i = 0; i < 30; i++)
                Directory.CreateDirectory(Path.Combine(root, $"D{i:00}"));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var scanner = new FolderScanner();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => scanner.ScanAsync(root, cancellationToken: cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Scan_does_not_modify_files()
    {
        var root = CreateTree();

        try
        {
            var file = Path.Combine(root, "important.txt");
            File.WriteAllText(file, "original");
            var before = File.GetLastWriteTimeUtc(file);

            var scanner = new FolderScanner();
            await scanner.ScanAsync(root);

            Assert.True(File.Exists(file));
            Assert.Equal("original", File.ReadAllText(file));
            Assert.Equal(before, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static string CreateTree()
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

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, recursive: true);
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public InlineProgress(Action<T> callback) => _callback = callback;

        public void Report(T value) => _callback(value);
    }
}
