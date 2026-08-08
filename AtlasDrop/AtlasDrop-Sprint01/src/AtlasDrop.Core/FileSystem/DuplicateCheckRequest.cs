namespace AtlasDrop.Core.FileSystem;

public sealed record DuplicateCheckRequest(
    string SourceFilePath,
    string DestinationDirectory,
    string ProposedFileName);
