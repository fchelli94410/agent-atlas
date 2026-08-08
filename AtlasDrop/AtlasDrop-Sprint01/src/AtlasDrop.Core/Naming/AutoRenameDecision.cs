using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Core.Naming;

public sealed record AutoRenameDecision(
    bool ShouldApply,
    string ProposedFileName,
    SuggestionConfidenceLevel Confidence,
    string Reason);
