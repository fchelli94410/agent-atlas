using AtlasDrop.Core.FileSystem;
using AtlasDrop.Core.History;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.FileOperations;

public sealed class HistoryTrackingFileMoveService
    : ISafeFileMoveService
{
    private readonly ISafeFileMoveService _inner;
    private readonly IOperationHistoryRepository _history;
    private readonly IFileHashService _hashService;

    public HistoryTrackingFileMoveService(
        ISafeFileMoveService inner,
        IOperationHistoryRepository history,
        IFileHashService? hashService = null)
    {
        _inner = inner
            ?? throw new ArgumentNullException(nameof(inner));

        _history = history
            ?? throw new ArgumentNullException(nameof(history));

        _hashService = hashService
            ?? new FileHashService();
    }

    public async Task<SafeMoveResult> MoveAsync(
        SafeMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourcePath = Path.GetFullPath(request.SourceFilePath);
        var oldFileName = Path.GetFileName(sourcePath);
        var oldDestination = Path.GetDirectoryName(sourcePath);

        string? sourceHash = null;

        if (File.Exists(sourcePath))
        {
            try
            {
                var size = new FileInfo(sourcePath).Length;

                if (size <= 64L * 1024L * 1024L)
                    sourceHash = _hashService.ComputeSha256(sourcePath);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                sourceHash = null;
            }
        }

        var result = await _inner.MoveAsync(
            request,
            cancellationToken);

        var newFileName = result.DestinationPath is null
            ? Path.GetFileName(request.ProposedFileName)
            : Path.GetFileName(result.DestinationPath);

        var newDestination = result.DestinationPath is null
            ? request.DestinationDirectory
            : Path.GetDirectoryName(result.DestinationPath);

        var entry = new OperationHistoryEntry(
            Guid.NewGuid().ToString("N"),
            sourcePath,
            oldFileName,
            newFileName,
            oldDestination,
            newDestination,
            DateTime.UtcNow,
            result.Success,
            result.Message,
            sourceHash,
            false);

        await _history.AddAsync(
            entry,
            cancellationToken);

        return result;
    }
}
