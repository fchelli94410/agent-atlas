namespace AtlasDrop.Core.Indexing;

public sealed record FolderScanProgress(
    int FoldersScanned,
    string CurrentPath);
