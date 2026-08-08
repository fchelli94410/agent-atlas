using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Indexing;

public sealed record FolderFileProfile(
    int FileCount,
    IReadOnlyDictionary<DocumentType, int> DocumentTypeCounts,
    IReadOnlyDictionary<string, int> ExtensionCounts,
    IReadOnlyList<string> FrequentKeywords);
