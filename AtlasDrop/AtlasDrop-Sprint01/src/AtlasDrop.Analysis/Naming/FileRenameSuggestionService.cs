using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Core.Naming;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Analysis.Naming;

public sealed class FileRenameSuggestionService
    : IFileRenameSuggestionService
{
    private readonly IWindowsFileNamePolicy _fileNamePolicy;

    public FileRenameSuggestionService(
        IWindowsFileNamePolicy? fileNamePolicy = null)
    {
        _fileNamePolicy = fileNamePolicy
            ?? new WindowsFileNamePolicy();
    }

    private static readonly Regex SpacesRegex =
        new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex JunkNameRegex =
        new(
            @"^(scan|document|document\d+|image|img|invoice|facture|file|fichier|new document|nouveau document)(\s*\(\d+\))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FileRenameSuggestion Suggest(FileRenameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.OriginalFileName))
            throw new ArgumentException(
                "Nom de fichier source obligatoire.",
                nameof(context));

        var extension = Path.GetExtension(context.OriginalFileName);
        var originalBase = Path.GetFileNameWithoutExtension(
            context.OriginalFileName);

        var parts = new List<string>();
        var reasons = new List<string>();

        var datePart = BuildDatePart(context);

        if (!string.IsNullOrWhiteSpace(datePart))
        {
            parts.Add(datePart);
            reasons.Add("date fiable intégrée");
        }

        var typePart = GetDocumentTypeLabel(context.DocumentType);

        if (!string.IsNullOrWhiteSpace(typePart))
        {
            parts.Add(typePart);
            reasons.Add("type documentaire intégré");
        }

        if (!string.IsNullOrWhiteSpace(context.Place))
        {
            parts.Add(CleanPart(context.Place));
            reasons.Add("lieu intégré");
        }

        var detail = FirstUseful(
            context.Detail,
            context.Company,
            context.Reference);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            parts.Add(CleanPart(detail));
            reasons.Add("détail utile intégré");
        }

        if (parts.Count == 0)
        {
            var cleanedOriginal = CleanPart(originalBase);

            var safeOriginal = _fileNamePolicy.Sanitize(
                cleanedOriginal + extension);

            return new FileRenameSuggestion(
                safeOriginal.SafeFileName,
                safeOriginal.Changed,
                new[] { "nom existant nettoyé sans invention" }
                    .Concat(safeOriginal.Reasons)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        var proposedBase = string.Join(
            " - ",
            parts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(RemoveDuplicateDateFragments)
                .Distinct(StringComparer.OrdinalIgnoreCase));

        proposedBase = CleanPart(proposedBase);

        if (proposedBase.Length > 150)
            proposedBase = proposedBase[..150].Trim();

        var proposed = proposedBase + extension;
        var safe = _fileNamePolicy.Sanitize(proposed);

        if (JunkNameRegex.IsMatch(originalBase.Trim()))
            reasons.Add("nom source peu informatif remplacé");

        reasons.AddRange(safe.Reasons);

        return new FileRenameSuggestion(
            safe.SafeFileName,
            !string.Equals(
                safe.SafeFileName,
                context.OriginalFileName,
                StringComparison.Ordinal),
            reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? BuildDatePart(FileRenameContext context)
    {
        if (context.ExactDate is not null)
            return context.ExactDate.Value.ToString("yyyy-MM-dd");

        if (context.Year is null)
            return null;

        if (context.Month is >= 1 and <= 12)
            return $"{context.Year:0000}-{context.Month:00}";

        return $"{context.Year:0000}";
    }

    private static string? GetDocumentTypeLabel(DocumentType type) =>
        type switch
        {
            DocumentType.Invoice => "Facture",
            DocumentType.Quote => "Devis",
            DocumentType.Contract => "Contrat",
            DocumentType.Order => "Commande",
            DocumentType.Report => "Rapport",
            DocumentType.Spreadsheet => "Tableau",
            DocumentType.Presentation => "Présentation",
            DocumentType.Email => "Email",
            DocumentType.Image => "Image",
            DocumentType.Archive => "Archive",
            DocumentType.Letter => "Courrier",
            DocumentType.Statement => "Relevé",
            DocumentType.Receipt => "Reçu",
            _ => null
        };

    private static string? FirstUseful(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string CleanPart(string value)
    {
        var cleaned = value
            .Replace("\"", "")
            .Replace("*", "")
            .Replace(":", " -")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("?", "")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("|", "-");

        cleaned = SpacesRegex.Replace(cleaned, " ").Trim();
        cleaned = cleaned.Trim('.', ' ');

        return cleaned;
    }

    private static string RemoveDuplicateDateFragments(string value)
    {
        return Regex.Replace(
            value,
            @"\b(\d{4}-\d{2}-\d{2}|\d{4}-\d{2}|\d{4})\b(?:\s*-\s*\1\b)+",
            "$1",
            RegexOptions.IgnoreCase);
    }
}
