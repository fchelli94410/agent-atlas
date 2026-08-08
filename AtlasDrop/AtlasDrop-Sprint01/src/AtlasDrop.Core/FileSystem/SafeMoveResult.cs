namespace AtlasDrop.Core.FileSystem;

public sealed record SafeMoveResult(
    bool Success,
    string SourcePath,
    string? DestinationPath,
    bool RenamedForConflict,
    bool ExactDuplicateDetected,
    string Message);
