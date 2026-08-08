using System.IO.Compression;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Archive;

public sealed class ZipInspector : IZipInspector
{
    private readonly int _maxEntries;
    private readonly long _maxTotalUncompressedBytes;
    private readonly double _maxCompressionRatio;

    public ZipInspector(
        int maxEntries = 10_000,
        long maxTotalUncompressedBytes = 512L * 1024L * 1024L,
        double maxCompressionRatio = 200d)
    {
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        if (maxTotalUncompressedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalUncompressedBytes));

        if (maxCompressionRatio <= 1)
            throw new ArgumentOutOfRangeException(nameof(maxCompressionRatio));

        _maxEntries = maxEntries;
        _maxTotalUncompressedBytes = maxTotalUncompressedBytes;
        _maxCompressionRatio = maxCompressionRatio;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".zip",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<ZipInspectionResult> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin ZIP est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier ZIP est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension non prise en charge : {Path.GetExtension(fullPath)}");

        return Task.Run(
            () => InspectInternal(fullPath, cancellationToken),
            cancellationToken);
    }

    private ZipInspectionResult InspectInternal(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);

        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: false);

        var entries = new List<ZipEntryInfo>();
        var warnings = new List<string>();

        long totalCompressed = 0;
        long totalUncompressed = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entries.Count >= _maxEntries)
            {
                warnings.Add($"Nombre maximal d'entrées dépassé : {_maxEntries}.");
                break;
            }

            var isDirectory =
                entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                entry.FullName.EndsWith("\\", StringComparison.Ordinal);

            var suspicious = IsSuspiciousPath(entry.FullName);

            if (suspicious)
                warnings.Add($"Chemin ZIP suspect : {entry.FullName}");

            totalCompressed = checked(totalCompressed + Math.Max(0, entry.CompressedLength));
            totalUncompressed = checked(totalUncompressed + Math.Max(0, entry.Length));

            if (totalUncompressed > _maxTotalUncompressedBytes)
            {
                warnings.Add(
                    $"Taille décompressée maximale dépassée : {_maxTotalUncompressedBytes} octets.");
            }

            var ratio = entry.CompressedLength <= 0
                ? (entry.Length > 0 ? double.PositiveInfinity : 1d)
                : (double)entry.Length / entry.CompressedLength;

            if (ratio > _maxCompressionRatio)
            {
                warnings.Add(
                    $"Ratio de compression suspect pour {entry.FullName} : {ratio:F1}x.");
                suspicious = true;
            }

            entries.Add(new ZipEntryInfo(
                entry.FullName,
                entry.CompressedLength,
                entry.Length,
                isDirectory,
                suspicious));
        }

        var isSafe =
            warnings.Count == 0 &&
            entries.Count <= _maxEntries &&
            totalUncompressed <= _maxTotalUncompressedBytes;

        return new ZipInspectionResult(
            entries,
            totalCompressed,
            totalUncompressed,
            isSafe,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool IsSuspiciousPath(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return true;

        var normalized = entryName.Replace('\\', '/');

        if (normalized.StartsWith("/", StringComparison.Ordinal))
            return true;

        if (Path.IsPathRooted(entryName))
            return true;

        var segments = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(x =>
            x == ".." ||
            x == "." ||
            x.StartsWith("$", StringComparison.Ordinal));
    }
}
