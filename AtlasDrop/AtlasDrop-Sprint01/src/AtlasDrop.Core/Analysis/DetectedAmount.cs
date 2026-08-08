namespace AtlasDrop.Core.Analysis;

public sealed record DetectedAmount(
    decimal Value,
    string Currency,
    double Confidence,
    string Source,
    string RawText);
