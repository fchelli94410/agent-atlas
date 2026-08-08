namespace AtlasDrop.Core.Analysis;

public sealed record DetectedDate(
    DateTime? Value,
    DetectedDatePrecision Precision,
    double Confidence,
    string Source,
    string RawText);
