namespace AtlasDrop.Core.Analysis;

public sealed record ZipInspectionResult(
    IReadOnlyList<ZipEntryInfo> Entries,
    long TotalCompressedBytes,
    long TotalUncompressedBytes,
    bool IsSafe,
    IReadOnlyList<string> Warnings);
