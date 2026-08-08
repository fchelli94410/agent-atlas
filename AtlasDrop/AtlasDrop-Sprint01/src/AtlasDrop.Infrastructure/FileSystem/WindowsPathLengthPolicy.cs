using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class WindowsPathLengthPolicy
    : IWindowsPathLengthPolicy
{
    public PathLengthCheckResult Check(
        string path,
        int safeLimit = 240)
    {
        if (safeLimit < 64)
            throw new ArgumentOutOfRangeException(nameof(safeLimit));

        if (string.IsNullOrWhiteSpace(path))
        {
            return new PathLengthCheckResult(
                false,
                string.Empty,
                0,
                safeLimit,
                "Chemin vide.");
        }

        string normalized;

        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return new PathLengthCheckResult(
                false,
                path,
                path.Length,
                safeLimit,
                "Chemin invalide.");
        }

        var length = normalized.Length;

        if (length >= safeLimit)
        {
            return new PathLengthCheckResult(
                false,
                normalized,
                length,
                safeLimit,
                $"Chemin trop long ({length} caractères, limite de sécurité {safeLimit - 1}).");
        }

        return new PathLengthCheckResult(
            true,
            normalized,
            length,
            safeLimit,
            "Longueur de chemin sûre.");
    }

    public PathLengthCheckResult CheckDestination(
        string destinationDirectory,
        string fileName,
        int safeLimit = 240)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            return new PathLengthCheckResult(
                false,
                string.Empty,
                0,
                safeLimit,
                "Dossier destination vide.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PathLengthCheckResult(
                false,
                string.Empty,
                0,
                safeLimit,
                "Nom de fichier vide.");
        }

        string combined;

        try
        {
            combined = Path.Combine(
                destinationDirectory,
                Path.GetFileName(fileName));
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return new PathLengthCheckResult(
                false,
                string.Empty,
                0,
                safeLimit,
                "Destination invalide.");
        }

        return Check(combined, safeLimit);
    }
}
