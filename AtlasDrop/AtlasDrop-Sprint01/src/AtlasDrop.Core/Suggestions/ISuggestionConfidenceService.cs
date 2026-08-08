namespace AtlasDrop.Core.Suggestions;

public interface ISuggestionConfidenceService
{
    SuggestionConfidence Evaluate(double score);
}
