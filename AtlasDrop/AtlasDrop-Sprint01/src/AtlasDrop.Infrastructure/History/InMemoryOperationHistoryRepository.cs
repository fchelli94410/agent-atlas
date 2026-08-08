using AtlasDrop.Core.History;

namespace AtlasDrop.Infrastructure.History;

public sealed class InMemoryOperationHistoryRepository
    : IOperationHistoryRepository
{
    private readonly object _gate = new();
    private readonly List<OperationHistoryEntry> _entries = new();

    public Task AddAsync(
        OperationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OperationHistoryEntry>> GetRecentAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<OperationHistoryEntry> result = _entries
                .OrderByDescending(x => x.TimestampUtc)
                .Take(limit)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task MarkUndoneAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException(
                "Identifiant opération obligatoire.",
                nameof(operationId));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _entries.FindIndex(
                x => string.Equals(
                    x.Id,
                    operationId,
                    StringComparison.Ordinal));

            if (index < 0)
                return Task.CompletedTask;

            _entries[index] = _entries[index] with
            {
                Undone = true
            };
        }

        return Task.CompletedTask;
    }
}
