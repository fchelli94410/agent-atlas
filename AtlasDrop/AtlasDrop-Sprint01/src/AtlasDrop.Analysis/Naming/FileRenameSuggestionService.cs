using System.Globalization;
using System.Text;
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

    private static readonly Regex SourceWordRegex =
        new(@"[\p{L}][\p{L}\p{N}'’]{1,30}", RegexOptions.Compiled);

    private static readonly HashSet<string> SourceStopWords =
        new(
            new[]
            {
                "scan", "document", "image", "invoice", "facture", "file", "fichier",
                "nouveau", "new", "contrat", "devis", "rapport", "relevé", "releve",
                "email", "reçu", "recu", "archive", "copie", "page"
            },
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<DocumentType> StructuredDocumentTypes =
        new(
            new[]
            {
                DocumentType.Invoice,
                DocumentType.Quote,
                DocumentType.Contract,
                DocumentType.Order,
                DocumentType.Statement,
                DocumentType.Receipt
            });

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

        // Règle de sécurité Atlas Drop : si le nom source contient déjà une information
        // humaine exploitable, l'OCR ne doit JAMAIS lui ajouter une date, un lieu, une
        // société ou un type documentaire. On conserve le nom utilisateur, en corrigeant
        // uniquement les espaces/caractères Windows invalides.
        if (!JunkNameRegex.IsMatch(originalBase.Trim()))
        {
            var cleanedOriginal = CleanPart(originalBase);
            var safeOriginal = _fileNamePolicy.Sanitize(cleanedOriginal + extension);
            var reasons = new List<string>
            {
                "nom source informatif conservé : aucune donnée OCR ajoutée"
            };

            if (HasRejectedUncertainDate(context, originalBase))
                reasons.Add("date détectée ignorée car absente du nom source");

            reasons.AddRange(safeOriginal.Reasons);

            return new FileRenameSuggestion(
                safeOriginal.SafeFileName,
                !string.Equals(
                    safeOriginal.SafeFileName,
                    context.OriginalFileName,
                    StringComparison.Ordinal),
                reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        // Un nom réellement générique (scan, document1...) peut encore être amélioré
        // à partir de métadonnées structurées. Cette branche ne s'applique jamais à un
        // nom déjà significatif fourni par l'utilisateur.
        var parts = new List<string>();
        var reasonsForJunk = new List<string>();

        var sourceKeywords = ExtractSourceKeywords(originalBase);
        var datePart = BuildReliableDatePart(context, originalBase);
        var hasExtractedMetadata =
            !string.IsNullOrWhiteSpace(datePart) ||
            context.DocumentType != DocumentType.Unknown ||
            !string.IsNullOrWhiteSpace(context.Place) ||
            !string.IsNullOrWhiteSpace(context.Company) ||
            !string.IsNullOrWhiteSpace(context.Detail) ||
            !string.IsNullOrWhiteSpace(context.Reference);

        if (!string.IsNullOrWhiteSpace(datePart))
        {
            parts.Add(datePart);
            reasonsForJunk.Add("date suffisamment fiable intégrée");
        }

        if (sourceKeywords.Count > 0 && hasExtractedMetadata)
        {
            parts.Add(string.Join(' ', sourceKeywords));
            reasonsForJunk.Add("mots fiables du nom source prioritaires");
        }

        var typePart = GetDocumentTypeLabel(context.DocumentType);

        if (!string.IsNullOrWhiteSpace(typePart) && !ContainsEquivalentSourceWord(sourceKeywords, typePart))
        {
            parts.Add(typePart);
            reasonsForJunk.Add("type documentaire intégré");
        }

        if (!string.IsNullOrWhiteSpace(context.Place))
        {
            parts.Add(CleanPart(context.Place));
            reasonsForJunk.Add("lieu intégré");
        }

        var detail = FirstUseful(
            context.Detail,
            context.Company,
            context.Reference);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            parts.Add(CleanPart(detail));
            reasonsForJunk.Add("détail utile intégré");
        }

        if (parts.Count == 0)
        {
            var cleanedOriginal = CleanPart(originalBase);
            var safeOriginal = _fileNamePolicy.Sanitize(cleanedOriginal + extension);

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

        reasonsForJunk.Add("nom source peu informatif remplacé");

        if (HasRejectedUncertainDate(context, originalBase))
            reasonsForJunk.Add("date détectée ignorée car insuffisamment fiable pour le renommage");

        reasonsForJunk.AddRange(safe.Reasons);

        return new FileRenameSuggestion(
            safe.SafeFileName,
            !string.Equals(
                safe.SafeFileName,
                context.OriginalFileName,
                StringComparison.Ordinal),
            reasonsForJunk.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IReadOnlyList<string> ExtractSourceKeywords(string originalBase)
    {
        if (JunkNameRegex.IsMatch(originalBase.Trim()))
            return Array.Empty<string>();

        return SourceWordRegex.Matches(originalBase)
            .Select(match => match.Value.Trim())
            .Where(word => word.Length >= 3)
            .Where(word => !SourceStopWords.Contains(word))
            .Where(word => word.Any(char.IsLetter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static string? BuildReliableDatePart(FileRenameContext context, string originalBase)
    {
        if (context.ExactDate is not null)
        {
            var exact = context.ExactDate.Value;
            if (StructuredDocumentTypes.Contains(context.DocumentType) || OriginalContainsDate(originalBase, exact))
                return exact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return null;
        }

        if (context.Year is null)
            return null;

        if (StructuredDocumentTypes.Contains(context.DocumentType))
        {
            if (context.Month is >= 1 and <= 12)
                return $"{context.Year:0000}-{context.Month:00}";

            return $"{context.Year:0000}";
        }

        if (!OriginalContainsYear(originalBase, context.Year.Value))
            return null;

        if (context.Month is >= 1 and <= 12)
            return $"{context.Year:0000}-{context.Month:00}";

        return $"{context.Year:0000}";
    }

    private static bool HasRejectedUncertainDate(FileRenameContext context, string originalBase)
    {
        var hasCandidate = context.ExactDate is not null || context.Year is not null;
        if (!hasCandidate)
            return false;

        if (context.ExactDate is DateTime exact && OriginalContainsDate(originalBase, exact))
            return false;

        if (context.Year is int year && OriginalContainsYear(originalBase, year))
            return false;

        return true;
    }

    private static bool OriginalContainsDate(string originalBase, DateTime value)
    {
        var candidates = new[]
        {
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            value.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
            value.ToString("ddMMyyyy", CultureInfo.InvariantCulture),
            value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        };

        return candidates.Any(candidate => originalBase.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool OriginalContainsYear(string originalBase, int year) =>
        Regex.IsMatch(originalBase, $@"(?<!\d){year:0000}(?!\d)", RegexOptions.CultureInvariant);

    private static bool ContainsEquivalentSourceWord(IReadOnlyList<string> sourceKeywords, string typePart)
    {
        var normalizedType = RemoveDiacritics(typePart);
        return sourceKeywords.Any(word =>
            string.Equals(RemoveDiacritics(word), normalizedType, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(NormalizationForm.FormC);
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
