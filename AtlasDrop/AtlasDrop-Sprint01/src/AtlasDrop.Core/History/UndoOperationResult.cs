namespace AtlasDrop.Core.History;

public sealed record UndoOperationResult(
    bool Success,
    string Message,
    string? RestoredPath);
