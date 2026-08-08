using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Indexing;

public sealed record IndexedFileSignal(
    string FileName,
    string Extension,
    DocumentType DocumentType,
    string NormalizedText,
    IReadOnlyList<string> Keywords);
