using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class FolderIdentityResolver : IFolderIdentityResolver
{
    public FolderIdentityMatch? TryMatch(
        FolderIdentityCandidate previous,
        FolderScanItem current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var reasons = new List<string>();
        var score = 0d;

        if (SamePath(previous.FullPath, current.FullPath))
        {
            return new FolderIdentityMatch(
                previous.StableId,
                previous.FullPath,
                current.FullPath,
                1.0,
                new[] { "Chemin identique." });
        }

        if (SameName(previous.Name, current.Name))
        {
            score += 0.30;
            reasons.Add("Nom de dossier identique.");
        }

        if (SameParent(previous.ParentPath, current.ParentPath))
        {
            score += 0.20;
            reasons.Add("Parent identique.");
        }

        if (previous.FileCount == current.FileCount)
        {
            score += 0.20;
            reasons.Add("Même nombre de fichiers.");
        }

        if (SameExtensions(previous.FileExtensions, current.FileExtensions))
        {
            score += 0.20;
            reasons.Add("Types de fichiers identiques.");
        }

        if (CreationDatesClose(previous.CreatedUtc, current.CreatedUtc))
        {
            score += 0.10;
            reasons.Add("Date de création cohérente.");
        }

        // Renommage : parent identique + contenu cohérent.
        // Déplacement : nom identique + contenu cohérent.
        var strongRename =
            SameParent(previous.ParentPath, current.ParentPath) &&
            previous.FileCount == current.FileCount &&
            SameExtensions(previous.FileExtensions, current.FileExtensions);

        var strongMove =
            SameName(previous.Name, current.Name) &&
            previous.FileCount == current.FileCount &&
            SameExtensions(previous.FileExtensions, current.FileExtensions);

        if (!strongRename && !strongMove)
            return null;

        if (score < 0.70)
            return null;

        return new FolderIdentityMatch(
            previous.StableId,
            previous.FullPath,
            current.FullPath,
            Math.Min(score, 1.0),
            reasons);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Normalize(left),
            Normalize(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool SameName(string left, string right) =>
        string.Equals(
            left.Trim(),
            right.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static bool SameParent(string? left, string? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return SamePath(left, right);
    }

    private static bool SameExtensions(
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        var a = left.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var b = right.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        return a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
    }

    private static bool CreationDatesClose(DateTime left, DateTime right) =>
        Math.Abs((left - right).TotalSeconds) <= 2;

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
