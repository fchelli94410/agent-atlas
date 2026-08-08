namespace AtlasDrop.Core.Indexing;

public sealed record DirectoryChange(
    DirectoryChangeKind Kind,
    string FullPath,
    string? OldFullPath,
    DateTime ObservedUtc);
