namespace AtlasDrop.Core.Indexing;

public sealed record ScanExecutionStatus(
    ScanExecutionState State,
    int FoldersScanned,
    string? CurrentPath,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    string? ErrorMessage);
