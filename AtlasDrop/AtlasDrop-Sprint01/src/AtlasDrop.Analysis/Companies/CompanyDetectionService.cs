using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Companies;

public sealed class CompanyDetectionService : ICompanyDetectionService
{
    private static readonly Regex LegalFormPattern = new(
        @"(?<!\p{L})(?<name>[A-ZÀ-ÖØ-Þ0-9][A-Za-zÀ-ÖØ-öø-ÿ0-9&'’\- ]{1,80}?)[ \t]+(?<form>SASU|SAS|SARL|EURL|SA|SCI|SNC|SELARL|SCP|S\.A\.S\.|S\.A\.R\.L\.|S\.A\.)(?!\p{L})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailPattern = new(
        @"(?<![\w.+-])(?<local>[\w.+-]+)@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})(?![\w.-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitCompanyPattern = new(
        @"(?i)(?:soci[eé]t[eé]|entreprise|company|fournisseur|client)\s*[:\-]\s*(?<name>[A-Za-zÀ-ÖØ-öø-ÿ0-9&'’.\- ]{2,100})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> GenericMailDomains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com",
            "googlemail.com",
            "outlook.com",
            "hotmail.com",
            "live.com",
            "icloud.com",
            "yahoo.com",
            "yahoo.fr",
            "orange.fr",
            "free.fr",
            "sfr.fr",
            "laposte.net"
        };

    public IReadOnlyList<DetectedCompany> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectedCompany>();

        var candidates = new List<DetectedCompany>();

        foreach (Match match in LegalFormPattern.Matches(text))
        {
            var name = CleanCompanyName(
                $"{match.Groups["name"].Value} {match.Groups["form"].Value}");

            AddCandidate(
                candidates,
                name,
                0.96,
                "legal-form",
                match.Value);
        }

        foreach (Match match in ExplicitCompanyPattern.Matches(text))
        {
            var name = CleanCompanyName(match.Groups["name"].Value);

            if (name.Length >= 2)
            {
                AddCandidate(
                    candidates,
                    name,
                    0.90,
                    "explicit-label",
                    match.Value);
            }
        }

        foreach (Match match in EmailPattern.Matches(text))
        {
            var domain = match.Groups["domain"].Value.Trim().ToLowerInvariant();

            if (GenericMailDomains.Contains(domain))
                continue;

            var company = CompanyFromDomain(domain);

            if (!string.IsNullOrWhiteSpace(company))
            {
                AddCandidate(
                    candidates,
                    company,
                    0.78,
                    "email-domain",
                    match.Value);
            }
        }

        return candidates
            .GroupBy(x => x.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.Confidence)
                .ThenByDescending(x => x.Name.Length)
                .First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCandidate(
        ICollection<DetectedCompany> candidates,
        string name,
        double confidence,
        string source,
        string raw)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (Regex.IsMatch(
            name,
            @"(?i)\.(pdf|docx?|xlsx?|pptx?|txt|csv|msg|zip)\b"))
        {
            return;
        }

        var normalized = NormalizeCompanyName(name);

        if (normalized.Length < 2)
            return;

        candidates.Add(new DetectedCompany(
            name.Trim(),
            normalized,
            confidence,
            source,
            raw.Trim()));
    }

    private static string CleanCompanyName(string value)
    {
        var cleaned = Regex.Replace(value, @"\s+", " ").Trim();

        cleaned = cleaned.Trim(
            ' ', '\t', '\r', '\n',
            '.', ',', ';', ':', '-', '–', '—');

        return cleaned;
    }

    private static string CompanyFromDomain(string domain)
    {
        var host = domain.ToLowerInvariant();

        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        var parts = host.Split(
            '.',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return string.Empty;

        var label = parts[^2];

        if (label.Equals("co", StringComparison.OrdinalIgnoreCase) &&
            parts.Length >= 3)
        {
            label = parts[^3];
        }

        label = label.Replace("-", " ").Replace("_", " ");
        label = Regex.Replace(label, @"\s+", " ").Trim();

        if (label.Length == 0)
            return string.Empty;

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(label);
    }

    private static string NormalizeCompanyName(string value)
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

        var normalized = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();

        normalized = Regex.Replace(
            normalized,
            @"\b(sasu|sas|sarl|eurl|sa|sci|snc|selarl|scp)\b",
            "",
            RegexOptions.IgnoreCase);

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }
}
