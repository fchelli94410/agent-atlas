using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Suggestions;

public sealed record FileSuggestionContext(
    string FileName,
    string NormalizedText,
    DocumentType DocumentType,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Places,
    IReadOnlyList<string> Companies,
    IReadOnlyList<int> Years,
    string? HistoryContextKey = null);
