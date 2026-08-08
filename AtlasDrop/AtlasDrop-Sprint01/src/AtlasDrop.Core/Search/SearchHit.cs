namespace AtlasDrop.Core.Search;

public sealed record SearchHit(
    SearchDocument Document,
    double Score,
    IReadOnlyList<string> Reasons);
