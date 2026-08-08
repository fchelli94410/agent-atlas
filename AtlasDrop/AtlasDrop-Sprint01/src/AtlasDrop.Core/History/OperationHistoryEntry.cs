namespace AtlasDrop.Core.History;

public sealed record OperationHistoryEntry(
    string Id,
    string SourcePath,
    string OldFileName,
    string NewFileName,
    string? OldDestination,
    string? NewDestination,
    DateTime TimestampUtc,
    bool Success,
    string Result,
    string? Sha256,
    bool Undone);
