namespace AtlasDrop.Core.Analysis;

public sealed record DetectedPlace(
    string Name,
    string NormalizedName,
    string? PostalCode,
    double Confidence,
    string Source,
    string RawText);
