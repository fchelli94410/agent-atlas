namespace AtlasDrop.Core.FileSystem;

public sealed record MovePreflightRequest(
    string OneDriveRoot,
    string SourceFilePath,
    string DestinationDirectory,
    string ProposedFileName);
