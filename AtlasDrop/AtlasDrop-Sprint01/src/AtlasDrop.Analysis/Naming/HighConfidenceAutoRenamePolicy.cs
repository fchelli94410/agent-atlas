using AtlasDrop.Core.Naming;
using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Analysis.Naming;

public sealed class HighConfidenceAutoRenamePolicy
    : IAutoRenamePolicy
{
    public AutoRenameDecision Decide(
        FileRenameSuggestion suggestion,
        SuggestionConfidence confidence,
        bool userEnabledAutoRename)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        ArgumentNullException.ThrowIfNull(confidence);

        if (!userEnabledAutoRename)
        {
            return new AutoRenameDecision(
                false,
                suggestion.ProposedFileName,
                confidence.Level,
                "Renommage automatique désactivé.");
        }

        if (!suggestion.Changed)
        {
            return new AutoRenameDecision(
                false,
                suggestion.ProposedFileName,
                confidence.Level,
                "Aucun changement de nom utile.");
        }

        if (confidence.Level != SuggestionConfidenceLevel.High ||
            !confidence.CanAutoClassify)
        {
            return new AutoRenameDecision(
                false,
                suggestion.ProposedFileName,
                confidence.Level,
                "Renommage automatique interdit sans confiance élevée.");
        }

        return new AutoRenameDecision(
            true,
            suggestion.ProposedFileName,
            confidence.Level,
            "Renommage automatique autorisé en confiance élevée.");
    }
}
