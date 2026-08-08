namespace AtlasDrop.Core.Indexing;

public interface IFolderIdentityResolver
{
    FolderIdentityMatch? TryMatch(
        FolderIdentityCandidate previous,
        FolderScanItem current);
}
