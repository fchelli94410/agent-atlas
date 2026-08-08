namespace AtlasDrop.Core.FileSystem;

public interface ISafeFileMoveService
{
    Task<SafeMoveResult> MoveAsync(
        SafeMoveRequest request,
        CancellationToken cancellationToken = default);
}
