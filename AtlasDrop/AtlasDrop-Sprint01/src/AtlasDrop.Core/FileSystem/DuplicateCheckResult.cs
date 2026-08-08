namespace AtlasDrop.Core.FileSystem;

public enum DuplicateMatchKind
{
    None = 0,
    NameCollision = 1,
    SameSize = 2,
    ExactDuplicate = 3
}

public sealed record DuplicateCheckResult(
    DuplicateMatchKind MatchKind,
    string DestinationPath,
    string SuggestedAvailablePath,
    bool DestinationExists,
    string Message);
