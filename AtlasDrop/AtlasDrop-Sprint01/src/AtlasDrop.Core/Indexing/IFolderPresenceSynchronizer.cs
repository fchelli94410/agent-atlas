namespace AtlasDrop.Core.Indexing;

public interface IFolderPresenceSynchronizer
{
    Task<FolderPresenceSyncResult> SynchronizeAsync(
        IReadOnlyCollection<string> existingFolderPaths,
        CancellationToken cancellationToken = default);
}
