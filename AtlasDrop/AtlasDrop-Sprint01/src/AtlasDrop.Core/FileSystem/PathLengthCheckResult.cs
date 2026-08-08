namespace AtlasDrop.Core.FileSystem;

public sealed record PathLengthCheckResult(
    bool IsSafe,
    string NormalizedPath,
    int Length,
    int Limit,
    string Message);
