namespace AtlasDrop.Core.Suggestions;

public interface IFolderScoringService
{
    FolderScoreResult Score(
        FolderCandidate folder,
        FileSuggestionContext context);

    IReadOnlyList<FolderScoreResult> Rank(
        IEnumerable<FolderCandidate> folders,
        FileSuggestionContext context,
        int maxResults = 3);
}
