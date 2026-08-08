namespace AtlasDrop.Core.FileSystem;

public sealed record SafeMoveRequest(
    string OneDriveRoot,
    string SourceFilePath,
    string DestinationDirectory,
    string ProposedFileName);
