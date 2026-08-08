namespace AtlasDrop.Core.Indexing;

public interface IScanCoordinator
{
    event EventHandler<ScanExecutionStatus>? StatusChanged;

    ScanExecutionStatus Status { get; }

    Task<IReadOnlyList<FolderScanItem>> RunAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}
