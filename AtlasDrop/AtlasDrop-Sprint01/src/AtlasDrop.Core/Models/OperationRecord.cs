namespace AtlasDrop.Core.Models;

public sealed record OperationRecord(
    long Id,
    string SourcePath,
    string? OldName,
    string? NewName,
    string? OldDestination,
    string? NewDestination,
    DateTime CreatedUtc,
    string Result,
    string? Sha256,
    bool IsUndone);
