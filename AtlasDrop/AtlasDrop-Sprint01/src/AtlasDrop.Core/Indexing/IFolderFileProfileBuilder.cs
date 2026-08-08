namespace AtlasDrop.Core.Indexing;

public interface IFolderFileProfileBuilder
{
    FolderFileProfile Build(
        IEnumerable<IndexedFileSignal> files,
        int maxKeywords = 20);
}
