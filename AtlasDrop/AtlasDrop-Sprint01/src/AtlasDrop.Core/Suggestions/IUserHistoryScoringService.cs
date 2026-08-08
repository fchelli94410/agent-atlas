namespace AtlasDrop.Core.Suggestions;

public interface IUserHistoryScoringService
{
    double GetAdjustment(
        string folderId,
        IEnumerable<UserHistorySignal> signals,
        string? contextKey,
        out IReadOnlyList<string> reasons);
}
