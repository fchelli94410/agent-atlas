namespace AtlasDrop.Core.Search;

public interface IManualFolderSearchService
{
    IReadOnlyList<ManualSearchResult> Search(
        IEnumerable<SearchDocument> documents,
        string query,
        int maxResults = 20);
}
