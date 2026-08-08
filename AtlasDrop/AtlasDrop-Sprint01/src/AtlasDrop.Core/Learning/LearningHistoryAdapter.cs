using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Core.Learning;

public static class LearningHistoryAdapter
{
    public static IReadOnlyList<UserHistorySignal> ToUserHistorySignals(
        IEnumerable<LearningEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events
            .Where(x => !string.IsNullOrWhiteSpace(x.FolderId))
            .Select(x => new UserHistorySignal(
                x.FolderId!,
                MapType(x.Type),
                x.TimestampUtc,
                x.ContextKey))
            .ToArray();
    }

    private static UserHistorySignalType MapType(
        LearningEventType type) =>
        type switch
        {
            LearningEventType.DestinationChosen =>
                UserHistorySignalType.Chosen,

            LearningEventType.DestinationRejected =>
                UserHistorySignalType.Rejected,

            LearningEventType.FolderCreated =>
                UserHistorySignalType.CreatedFolder,

            LearningEventType.RenameCorrected =>
                UserHistorySignalType.RenameCorrected,

            LearningEventType.OperationUndone =>
                UserHistorySignalType.Undo,

            _ => UserHistorySignalType.Chosen
        };
}
