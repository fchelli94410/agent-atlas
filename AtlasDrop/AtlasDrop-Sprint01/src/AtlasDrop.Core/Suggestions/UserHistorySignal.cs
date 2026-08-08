namespace AtlasDrop.Core.Suggestions;

public enum UserHistorySignalType
{
    Chosen = 0,
    Rejected = 1,
    RenameCorrected = 2,
    Undo = 3,
    CreatedFolder = 4
}

public sealed record UserHistorySignal(
    string FolderId,
    UserHistorySignalType Type,
    DateTime TimestampUtc,
    string? ContextKey = null);
