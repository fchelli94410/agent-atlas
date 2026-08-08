namespace AtlasDrop.Core.Indexing;

public sealed record FolderIdentityMatch(
    string StableId,
    string OldPath,
    string NewPath,
    double Confidence,
    IReadOnlyList<string> Reasons);
