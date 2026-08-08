namespace AtlasDrop.Core.Analysis;

public sealed record DetectedPerson(
    string Name,
    string NormalizedName,
    double Confidence,
    string Source,
    string RawText);
