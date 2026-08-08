using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class FolderScanner : IFolderScanner
{
    public Task<IReadOnlyList<FolderScanItem>> ScanAsync(
        string rootPath,
        IProgress<FolderScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("La racine de scan est obligatoire.", nameof(rootPath));

        var fullRoot = Path.GetFullPath(rootPath);

        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Racine introuvable : {fullRoot}");

        return Task.Run<IReadOnlyList<FolderScanItem>>(
            () => ScanInternal(fullRoot, progress, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<FolderScanItem> ScanInternal(
        string rootPath,
        IProgress<FolderScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<FolderScanItem>();
        var root = new DirectoryInfo(rootPath);
        var stack = new Stack<(DirectoryInfo Directory, int Depth, string? ParentPath)>();
        stack.Push((root, 0, null));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (directory, depth, parentPath) = stack.Pop();

            var item = BuildItem(directory, depth, parentPath);
            results.Add(item);

            progress?.Report(new FolderScanProgress(results.Count, directory.FullName));

            DirectoryInfo[] children;
            try
            {
                children = directory.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            Array.Sort(children, (a, b) =>
                string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));

            for (var i = children.Length - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var child = children[i];

                if (ShouldSkipTechnicalDirectory(child))
                    continue;

                stack.Push((child, depth + 1, directory.FullName));
            }
        }

        return results;
    }

    private static FolderScanItem BuildItem(
        DirectoryInfo directory,
        int depth,
        string? parentPath)
    {
        var extensionCounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileCount = 0;

        try
        {
            foreach (var file in directory.EnumerateFiles())
            {
                fileCount++;

                if (!string.IsNullOrWhiteSpace(file.Extension))
                    extensionCounts.Add(file.Extension.ToLowerInvariant());
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return new FolderScanItem(
            directory.FullName,
            directory.Name,
            parentPath,
            depth,
            directory.CreationTimeUtc,
            directory.LastWriteTimeUtc,
            fileCount,
            extensionCounts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool ShouldSkipTechnicalDirectory(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            return true;

        if ((directory.Attributes & FileAttributes.System) != 0)
            return true;

        return false;
    }
}
