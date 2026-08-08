namespace AtlasDrop.Core.Indexing;
public sealed record IncrementalScanResult(IReadOnlyList<FolderScanItem> Added, IReadOnlyList<FolderScanItem> Changed, IReadOnlyList<string> Removed, IReadOnlyList<FolderScanItem> Unchanged);
