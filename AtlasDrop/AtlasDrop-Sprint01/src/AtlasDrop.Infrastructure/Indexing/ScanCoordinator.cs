using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class ScanCoordinator : IScanCoordinator
{
    private readonly IFolderScanner _scanner;
    private readonly object _gate = new();

    private ScanExecutionStatus _status = new(
        ScanExecutionState.NotStarted,
        0,
        null,
        null,
        null,
        null);

    public ScanCoordinator(IFolderScanner scanner)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
    }

    public event EventHandler<ScanExecutionStatus>? StatusChanged;

    public ScanExecutionStatus Status
    {
        get
        {
            lock (_gate)
                return _status;
        }
    }

    public async Task<IReadOnlyList<FolderScanItem>> RunAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        SetStatus(new ScanExecutionStatus(
            ScanExecutionState.Running,
            0,
            null,
            started,
            null,
            null));

        var progress = new Progress<FolderScanProgress>(p =>
        {
            SetStatus(new ScanExecutionStatus(
                ScanExecutionState.Running,
                p.FoldersScanned,
                p.CurrentPath,
                started,
                null,
                null));
        });

        try
        {
            var result = await _scanner.ScanAsync(
                rootPath,
                progress,
                cancellationToken);

            var completed = DateTime.UtcNow;
            var finalCount = result.Count;

            SetStatus(new ScanExecutionStatus(
                ScanExecutionState.Completed,
                finalCount,
                result.LastOrDefault()?.FullPath,
                started,
                completed,
                null));

            return result;
        }
        catch (OperationCanceledException)
        {
            var current = Status;

            SetStatus(new ScanExecutionStatus(
                ScanExecutionState.Cancelled,
                current.FoldersScanned,
                current.CurrentPath,
                started,
                DateTime.UtcNow,
                null));

            throw;
        }
        catch (Exception ex)
        {
            var current = Status;

            SetStatus(new ScanExecutionStatus(
                ScanExecutionState.Failed,
                current.FoldersScanned,
                current.CurrentPath,
                started,
                DateTime.UtcNow,
                ex.Message));

            throw;
        }
    }

    private void SetStatus(ScanExecutionStatus status)
    {
        lock (_gate)
            _status = status;

        StatusChanged?.Invoke(this, status);
    }
}
