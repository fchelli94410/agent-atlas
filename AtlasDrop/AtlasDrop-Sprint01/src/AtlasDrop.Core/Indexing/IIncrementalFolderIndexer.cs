namespace AtlasDrop.Core.Indexing;
public interface IIncrementalFolderIndexer
{
    IncrementalScanResult Compare(IReadOnlyCollection<FolderScanItem> current, IReadOnlyCollection<IndexedFolderSnapshot> previous);
}
