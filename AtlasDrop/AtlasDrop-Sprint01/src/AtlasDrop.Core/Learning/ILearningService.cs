namespace AtlasDrop.Core.Learning;

public interface ILearningService
{
    Task RecordAsync(
        LearningEventType type,
        string? folderId = null,
        string? contextKey = null,
        string? value = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}
