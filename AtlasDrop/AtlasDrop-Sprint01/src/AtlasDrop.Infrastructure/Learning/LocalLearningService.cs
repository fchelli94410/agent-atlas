using AtlasDrop.Core.Learning;

namespace AtlasDrop.Infrastructure.Learning;

public sealed class LocalLearningService : ILearningService
{
    private readonly ILearningRepository _repository;

    public LocalLearningService(ILearningRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task RecordAsync(
        LearningEventType type,
        string? folderId = null,
        string? contextKey = null,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var learningEvent = new LearningEvent(
            Guid.NewGuid().ToString("N"),
            type,
            DateTime.UtcNow,
            folderId,
            contextKey,
            SanitizeValue(value));

        return _repository.AddAsync(
            learningEvent,
            cancellationToken);
    }

    public Task<IReadOnlyList<LearningEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.ClearAsync(cancellationToken);
    }

    private static string? SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        // Learning stores compact signals only, never document contents.
        if (trimmed.Length > 200)
            trimmed = trimmed[..200];

        return trimmed;
    }
}
