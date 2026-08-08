namespace AtlasDrop.Core.FileSystem;

public sealed record RollbackResult(
    bool Success,
    string Message);
