namespace AtlasDrop.Core.History;

public interface IOperationHistoryRepository
{
    Task AddAsync(
        OperationHistoryEntry entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationHistoryEntry>> GetRecentAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task MarkUndoneAsync(
        string operationId,
        CancellationToken cancellationToken = default);
}
