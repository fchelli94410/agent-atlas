namespace AtlasDrop.Core.FileSystem;

public sealed record FolderCreationRequest(
    string OneDriveRoot,
    string ParentPath,
    string FolderName);
