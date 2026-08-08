namespace AtlasDrop.Core.FileSystem;

public sealed record MoveVerificationResult(
    bool Success,
    bool DestinationExists,
    bool SourceAbsent,
    bool SizeMatches,
    bool HashMatches,
    string Message);
