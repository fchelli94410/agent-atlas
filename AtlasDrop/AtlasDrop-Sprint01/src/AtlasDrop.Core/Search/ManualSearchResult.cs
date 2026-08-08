namespace AtlasDrop.Core.Search;

public sealed record ManualSearchResult(
    string Id,
    string Name,
    string FullPath,
    double Score,
    IReadOnlyList<string> Reasons);
