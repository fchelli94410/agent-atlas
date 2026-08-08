namespace AtlasDrop.Core.Suggestions;

public interface IFileSimilarityService
{
    double Score(
        FileSuggestionContext context,
        SimilarFileProfile candidate,
        out IReadOnlyList<string> reasons);
}
