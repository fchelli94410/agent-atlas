using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Suggestions;

public sealed record FolderCandidate(
    string Id,
    string Name,
    string FullPath,
    int Depth,
    bool IsExcluded,
    double QualityScore,
    DateTime? LastUsedUtc,
    int PreviousChoiceCount,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Places,
    IReadOnlyList<string> Companies,
    IReadOnlyList<int> Years,
    IReadOnlyDictionary<DocumentType, int> DocumentTypeCounts,
    IReadOnlyList<SimilarFileProfile>? ExistingFiles = null,
    IReadOnlyList<UserHistorySignal>? HistorySignals = null);
