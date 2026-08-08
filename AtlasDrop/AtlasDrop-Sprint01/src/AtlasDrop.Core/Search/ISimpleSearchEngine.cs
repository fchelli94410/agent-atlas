namespace AtlasDrop.Core.Search;

public interface ISimpleSearchEngine
{
    IReadOnlyList<SearchHit> Search(
        IEnumerable<SearchDocument> documents,
        string query,
        int maxResults = 20);
}
