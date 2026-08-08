namespace AtlasDrop.Core.Indexing;

public sealed record FolderScanItem(
    string FullPath,
    string Name,
    string? ParentPath,
    int Depth,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    int FileCount,
    IReadOnlyCollection<string> FileExtensions);
