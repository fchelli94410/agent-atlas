namespace AtlasDrop.Core.History;

public interface IOperationUndoService
{
    Task<UndoOperationResult> UndoAsync(
        OperationHistoryEntry entry,
        CancellationToken cancellationToken = default);
}
