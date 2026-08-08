namespace AtlasDrop.Core.FileSystem;

public sealed record SafeFileNameResult(
    bool IsValid,
    string SafeFileName,
    bool Changed,
    IReadOnlyList<string> Reasons);
