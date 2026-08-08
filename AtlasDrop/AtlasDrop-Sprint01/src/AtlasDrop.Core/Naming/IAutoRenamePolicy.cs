using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Core.Naming;

public interface IAutoRenamePolicy
{
    AutoRenameDecision Decide(
        FileRenameSuggestion suggestion,
        SuggestionConfidence confidence,
        bool userEnabledAutoRename);
}
