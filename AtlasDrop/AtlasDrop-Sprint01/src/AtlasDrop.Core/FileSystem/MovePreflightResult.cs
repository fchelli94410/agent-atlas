namespace AtlasDrop.Core.FileSystem;

public sealed record MovePreflightResult(
    bool CanProceed,
    string SourcePath,
    string DestinationPath,
    DuplicateCheckResult Duplicate,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
