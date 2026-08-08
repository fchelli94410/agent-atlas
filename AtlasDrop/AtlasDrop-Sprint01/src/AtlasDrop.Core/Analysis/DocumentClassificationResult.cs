namespace AtlasDrop.Core.Analysis;

public sealed record DocumentClassificationResult(
    DocumentType Type,
    double Confidence,
    IReadOnlyList<string> Reasons);
