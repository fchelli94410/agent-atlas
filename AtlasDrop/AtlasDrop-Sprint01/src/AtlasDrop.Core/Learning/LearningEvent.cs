namespace AtlasDrop.Core.Learning;

public sealed record LearningEvent(
    string Id,
    LearningEventType Type,
    DateTime TimestampUtc,
    string? FolderId,
    string? ContextKey,
    string? Value);
