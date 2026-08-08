using AtlasDrop.Core.Search;

namespace AtlasDrop.Search.Engine;

public sealed class ManualFolderSearchService
    : IManualFolderSearchService
{
    private readonly ISimpleSearchEngine _engine;

    public ManualFolderSearchService(ISimpleSearchEngine engine)
    {
        _engine = engine
            ?? throw new ArgumentNullException(nameof(engine));
    }

    public IReadOnlyList<ManualSearchResult> Search(
        IEnumerable<SearchDocument> documents,
        string query,
        int maxResults = 20)
    {
        var hits = _engine.Search(
            documents,
            query,
            maxResults);

        return hits
            .Select(x => new ManualSearchResult(
                x.Document.Id,
                x.Document.Name,
                x.Document.FullPath,
                x.Score,
                x.Reasons))
            .ToArray();
    }
}
