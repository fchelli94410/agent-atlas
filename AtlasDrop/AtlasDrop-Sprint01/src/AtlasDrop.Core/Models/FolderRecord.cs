namespace AtlasDrop.Core.Models;

public sealed record FolderRecord(
    string StableId,
    string FullPath,
    string Name,
    string NormalizedName,
    string? ParentStableId,
    int Depth,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    int FileCount,
    string FileTypesJson,
    string FrequentKeywordsJson,
    string FrequentPlacesJson,
    string FrequentCompaniesJson,
    string FrequentYearsJson,
    bool IsExcluded,
    double QualityScore,
    int UsageCount,
    DateTime? LastUsedUtc,
    DateTime LastScanUtc);
