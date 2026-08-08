namespace AtlasDrop.Core.Search;

public sealed record SearchDocument(
    string Id,
    string Name,
    string FullPath,
    string SearchText);
