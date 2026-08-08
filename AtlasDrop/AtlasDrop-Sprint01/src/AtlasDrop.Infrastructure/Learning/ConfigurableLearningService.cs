using AtlasDrop.Core.Learning;

namespace AtlasDrop.Infrastructure.Learning;

public sealed class ConfigurableLearningService : ILearningService
{
    private readonly ILearningService _inner;
    private readonly ILearningSettingsService _settings;

    public ConfigurableLearningService(
        ILearningService inner,
        ILearningSettingsService settings)
    {
        _inner = inner
            ?? throw new ArgumentNullException(nameof(inner));

        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
    }

    public Task RecordAsync(
        LearningEventType type,
        string? folderId = null,
        string? contextKey = null,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsEnabled)
            return Task.CompletedTask;

        return _inner.RecordAsync(
            type,
            folderId,
            contextKey,
            value,
            cancellationToken);
    }

    public Task<IReadOnlyList<LearningEvent>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        return _inner.GetEventsAsync(cancellationToken);
    }

    public Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        return _inner.ClearAsync(cancellationToken);
    }
}
