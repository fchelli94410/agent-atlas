namespace AtlasDrop.Core.FileSystem;

public interface IMoveRollbackService
{
    RollbackResult TryRollback(
        string originalSourcePath,
        string destinationPath);
}
