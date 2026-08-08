using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Entities;

public sealed class PersonAndReferenceDetectionService
    : IPersonAndReferenceDetectionService
{
    private static readonly Regex PersonLabelPattern = new(
        @"(?i)(?:contact|monsieur|m\.|madame|mme|mr|mrs|client|signataire|responsable)\s*[:\-]?\s*(?<name>[A-ZÀ-ÖØ-Þ][A-Za-zÀ-ÖØ-öø-ÿ'’\-]+(?:\s+[A-ZÀ-ÖØ-Þ][A-Za-zÀ-ÖØ-öø-ÿ'’\-]+){1,3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UppercasePersonPattern = new(
        @"(?<!\p{L})(?<name>[A-ZÀ-ÖØ-Þ][A-ZÀ-ÖØ-Þ'’\-]{1,30}\s+[A-ZÀ-ÖØ-Þ][A-ZÀ-ÖØ-Þ'’\-]{1,30})(?!\p{L})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReferencePattern = new(
        @"(?i)\b(?<kind>dossier|contrat|commande|devis|projet|client|réf(?:érence)?|ref(?:erence)?|ticket|sinistre|police|facture)\s*(?:n[°o]\s*)?[:#\-]?\s*(?<value>[A-Z0-9][A-Z0-9._/\-]{2,40})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GenericReferencePattern = new(
        @"(?<![A-Z0-9])(?<value>[A-Z]{2,8}[-_/][A-Z0-9]{2,20}(?:[-_/][A-Z0-9]{2,20})*)(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<DetectedPerson> DetectPersons(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectedPerson>();

        var candidates = new List<DetectedPerson>();

        foreach (Match match in PersonLabelPattern.Matches(text))
        {
            AddPerson(
                candidates,
                match.Groups["name"].Value,
                0.94,
                "explicit-label",
                match.Value);
        }

        foreach (Match match in UppercasePersonPattern.Matches(text))
        {
            AddPerson(
                candidates,
                match.Groups["name"].Value,
                0.72,
                "uppercase-name",
                match.Value);
        }

        return candidates
            .GroupBy(x => x.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Confidence).First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<DetectedReference> DetectReferences(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectedReference>();

        var candidates = new List<DetectedReference>();

        foreach (Match match in ReferencePattern.Matches(text))
        {
            var kind = NormalizeKind(match.Groups["kind"].Value);
            var value = match.Groups["value"].Value.Trim();

            AddReference(
                candidates,
                kind,
                value,
                0.96,
                match.Value);
        }

        foreach (Match match in GenericReferencePattern.Matches(text))
        {
            var value = match.Groups["value"].Value.Trim();

            AddReference(
                candidates,
                "reference",
                value,
                0.75,
                match.Value);
        }

        return candidates
            .GroupBy(
                x => x.NormalizedValue,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.Kind.Equals("reference", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddPerson(
        ICollection<DetectedPerson> candidates,
        string name,
        double confidence,
        string source,
        string raw)
    {
        var cleaned = Regex.Replace(name.Trim(), @"\s+", " ");
        var normalized = NormalizeWords(cleaned);

        if (normalized.Length < 3)
            return;

        candidates.Add(new DetectedPerson(
            cleaned,
            normalized,
            confidence,
            source,
            raw.Trim()));
    }

    private static void AddReference(
        ICollection<DetectedReference> candidates,
        string kind,
        string value,
        double confidence,
        string raw)
    {
        var normalized = NormalizeReference(value);

        if (normalized.Length < 3)
            return;

        candidates.Add(new DetectedReference(
            kind,
            value,
            normalized,
            confidence,
            raw.Trim()));
    }

    private static string NormalizeKind(string kind)
    {
        var normalized = NormalizeWords(kind);

        return normalized switch
        {
            "ref" or "reference" => "reference",
            "facture" => "facture",
            "dossier" => "dossier",
            "contrat" => "contrat",
            "commande" => "commande",
            "devis" => "devis",
            "projet" => "projet",
            "client" => "client",
            "ticket" => "ticket",
            "sinistre" => "sinistre",
            "police" => "police",
            _ => normalized
        };
    }

    private static string NormalizeReference(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, @"[\s._/\\\-]+", "-").Trim('-');
    }

    private static string NormalizeWords(string value)
    {
        var text = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else
                builder.Append(' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
