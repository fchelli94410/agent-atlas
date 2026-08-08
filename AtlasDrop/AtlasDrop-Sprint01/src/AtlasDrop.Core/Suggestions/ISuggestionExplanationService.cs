namespace AtlasDrop.Core.Suggestions;

public interface ISuggestionExplanationService
{
    SuggestionExplanation Explain(FolderScoreResult result);
}
