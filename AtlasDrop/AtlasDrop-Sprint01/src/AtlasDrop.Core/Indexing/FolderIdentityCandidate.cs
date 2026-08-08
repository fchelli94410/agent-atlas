namespace AtlasDrop.Core.Indexing;

public sealed record FolderIdentityCandidate(
    string StableId,
    string FullPath,
    string Name,
    string? ParentPath,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    int FileCount,
    IReadOnlyCollection<string> FileExtensions);
