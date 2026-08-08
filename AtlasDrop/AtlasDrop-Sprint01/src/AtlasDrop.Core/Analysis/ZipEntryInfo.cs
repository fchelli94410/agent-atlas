namespace AtlasDrop.Core.Analysis;

public sealed record ZipEntryInfo(
    string FullName,
    long CompressedLength,
    long UncompressedLength,
    bool IsDirectory,
    bool IsSuspicious);
