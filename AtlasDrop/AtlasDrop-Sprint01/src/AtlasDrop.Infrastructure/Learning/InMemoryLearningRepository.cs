using AtlasDrop.Core.Learning;

namespace AtlasDrop.Infrastructure.Learning;

public sealed class InMemoryLearningRepository : ILearningRepository
{
    private readonly object _gate = new();
    private readonly List<LearningEvent> _events = new();

    public Task AddAsync(
        LearningEvent learningEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(learningEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _events.Add(learningEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LearningEvent>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<LearningEvent> result = _events
                .OrderByDescending(x => x.TimestampUtc)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _events.Clear();
        }

        return Task.CompletedTask;
    }
}
