using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Suggestions;

public sealed record SimilarFileProfile(
    string FileName,
    DocumentType DocumentType,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Places,
    IReadOnlyList<string> Companies,
    IReadOnlyList<int> Years);
