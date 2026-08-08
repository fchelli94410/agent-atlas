namespace AtlasDrop.Core.Indexing;

public sealed record FolderReconciliationResult(
    IReadOnlyList<FolderIdentityMatch> Matches,
    IReadOnlyList<FolderScanItem> UnmatchedCurrent,
    IReadOnlyList<FolderIdentityCandidate> UnmatchedPrevious);
