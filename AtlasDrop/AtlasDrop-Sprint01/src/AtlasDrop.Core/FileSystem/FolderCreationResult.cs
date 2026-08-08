namespace AtlasDrop.Core.FileSystem;

public sealed record FolderCreationResult(
    bool Success,
    string? FullPath,
    string Message);
