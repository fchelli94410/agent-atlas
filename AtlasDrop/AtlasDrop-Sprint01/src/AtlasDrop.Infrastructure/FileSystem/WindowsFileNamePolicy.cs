using System.Text.RegularExpressions;
using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class WindowsFileNamePolicy
    : IWindowsFileNamePolicy
{
    private static readonly Regex Spaces =
        new(@"\s+", RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        };

    public SafeFileNameResult Sanitize(
        string fileName,
        int maxFileNameLength = 180)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new SafeFileNameResult(
                false,
                string.Empty,
                false,
                new[] { "nom de fichier vide" });
        }

        if (maxFileNameLength < 16)
            throw new ArgumentOutOfRangeException(nameof(maxFileNameLength));

        var original = fileName.Trim();
        var reasons = new List<string>();

        var precleaned = original
            .Replace("\"", "")
            .Replace("*", "")
            .Replace(":", " -")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("?", "")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("|", "-");

        if (!string.Equals(precleaned, original, StringComparison.Ordinal))
            reasons.Add("caractères Windows interdits supprimés");

        var extension = Path.GetExtension(precleaned);
        var baseName = Path.GetFileNameWithoutExtension(precleaned);

        var cleaned = baseName;

        cleaned = Spaces.Replace(cleaned, " ").Trim();
        cleaned = cleaned.TrimEnd('.', ' ');

        if (ReservedNames.Contains(cleaned))
        {
            cleaned = "_" + cleaned;
            reasons.Add("nom réservé Windows neutralisé");
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Fichier";
            reasons.Add("nom vide remplacé");
        }

        extension = SanitizeExtension(extension, reasons);

        var maxBaseLength = Math.Max(
            1,
            maxFileNameLength - extension.Length);

        if (cleaned.Length > maxBaseLength)
        {
            cleaned = cleaned[..maxBaseLength].TrimEnd('.', ' ');
            reasons.Add("nom raccourci");
        }

        var safe = cleaned + extension;

        if (safe.EndsWith(".", StringComparison.Ordinal) ||
            safe.EndsWith(" ", StringComparison.Ordinal))
        {
            safe = safe.TrimEnd('.', ' ');
            reasons.Add("fin de nom Windows corrigée");
        }

        var valid = IsValid(safe);

        return new SafeFileNameResult(
            valid,
            safe,
            !string.Equals(
                safe,
                original,
                StringComparison.Ordinal),
            reasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public bool IsValid(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var raw = fileName.Trim();

        if (raw.IndexOfAny(
            new[] { '"', '*', ':', '<', '>', '?', '/', '\\', '|' }) >= 0)
        {
            return false;
        }

        if (raw.EndsWith(".", StringComparison.Ordinal) ||
            raw.EndsWith(" ", StringComparison.Ordinal))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(raw);

        return !ReservedNames.Contains(stem);
    }

    private static string SanitizeExtension(
        string extension,
        ICollection<string> reasons)
    {
        if (string.IsNullOrEmpty(extension))
            return string.Empty;

        var cleaned = extension
            .Replace("\"", "")
            .Replace("*", "")
            .Replace(":", "")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("?", "")
            .Replace("/", "")
            .Replace("\\", "")
            .Replace("|", "")
            .Trim();

        if (!cleaned.StartsWith(".", StringComparison.Ordinal))
            cleaned = "." + cleaned.TrimStart('.');

        if (!string.Equals(cleaned, extension, StringComparison.Ordinal))
            reasons.Add("extension nettoyée");

        return cleaned;
    }
}
