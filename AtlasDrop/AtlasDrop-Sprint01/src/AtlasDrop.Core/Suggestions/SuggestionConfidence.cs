namespace AtlasDrop.Core.Suggestions;

public sealed record SuggestionConfidence(
    SuggestionConfidenceLevel Level,
    double Score,
    bool CanAutoClassify,
    string Reason);
