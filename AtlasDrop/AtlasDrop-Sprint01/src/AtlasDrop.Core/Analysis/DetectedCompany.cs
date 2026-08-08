namespace AtlasDrop.Core.Analysis;

public sealed record DetectedCompany(
    string Name,
    string NormalizedName,
    double Confidence,
    string Source,
    string RawText);
