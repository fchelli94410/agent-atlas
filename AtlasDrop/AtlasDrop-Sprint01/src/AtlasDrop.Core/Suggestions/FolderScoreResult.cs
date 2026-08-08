namespace AtlasDrop.Core.Suggestions;

public sealed record FolderScoreResult(
    FolderCandidate Folder,
    double Score,
    IReadOnlyList<string> Reasons,
    SuggestionConfidence? Confidence = null);
