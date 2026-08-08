namespace AtlasDrop.Core.Indexing;

public interface IFolderScanner
{
    Task<IReadOnlyList<FolderScanItem>> ScanAsync(
        string rootPath,
        IProgress<FolderScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
