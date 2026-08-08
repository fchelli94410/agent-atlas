namespace AtlasDrop.Core.Analysis;

public sealed record DetectedReference(
    string Kind,
    string Value,
    string NormalizedValue,
    double Confidence,
    string RawText);
