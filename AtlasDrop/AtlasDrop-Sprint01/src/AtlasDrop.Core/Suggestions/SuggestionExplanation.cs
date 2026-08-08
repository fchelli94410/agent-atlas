namespace AtlasDrop.Core.Suggestions;

public sealed record SuggestionExplanation(
    string Summary,
    IReadOnlyList<string> PositiveReasons,
    IReadOnlyList<string> NegativeReasons);
