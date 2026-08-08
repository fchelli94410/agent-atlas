using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Places;

public sealed class PlaceDetectionService : IPlaceDetectionService
{
    private static readonly Regex PostalCityPattern = new(
        @"(?<!\d)(?<postal>\d{5})[ \t]+(?<city>[A-ZÀ-ÖØ-Þ][A-Za-zÀ-ÖØ-öø-ÿ'’\- \t]{1,60})(?!\p{L})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AddressPattern = new(
        @"(?<!\d)(?<number>\d{1,4})\s+(?<street>(?:rue|avenue|av\.?|boulevard|bd\.?|chemin|route|all[ée]e|place|quai|impasse)\s+[A-Za-zÀ-ÖØ-öø-ÿ0-9'’\-\s]{2,80})(?:,\s*)?(?<postal>\d{5})?\s*(?<city>[A-ZÀ-ÖØ-Þ][A-Za-zÀ-ÖØ-öø-ÿ'’\-\s]{1,60})?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitPlacePattern = new(
        @"(?i)(?:ville|lieu|commune|site|agence)[ \t]*[:\-][ \t]*(?<city>[A-Za-zÀ-ÖØ-öø-ÿ'’\- \t]{2,80})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] KnownPlaces =
    {
        "Courbevoie",
        "Paris",
        "Saint-Maurice",
        "Saint Maurice",
        "Montévrain",
        "Montevrain",
        "Nanterre",
        "Puteaux",
        "La Défense",
        "Levallois-Perret",
        "Boulogne-Billancourt",
        "Neuilly-sur-Seine",
        "Créteil",
        "Versailles"
    };

    public IReadOnlyList<DetectedPlace> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectedPlace>();

        var candidates = new List<DetectedPlace>();

        foreach (Match match in PostalCityPattern.Matches(text))
        {
            var city = Clean(match.Groups["city"].Value);
            var postal = match.Groups["postal"].Value;

            AddCandidate(
                candidates,
                city,
                postal,
                0.97,
                "postal-city",
                match.Value);
        }

        foreach (Match match in AddressPattern.Matches(text))
        {
            var city = Clean(match.Groups["city"].Value);
            var postal = match.Groups["postal"].Success
                ? match.Groups["postal"].Value
                : null;

            if (!string.IsNullOrWhiteSpace(city))
            {
                AddCandidate(
                    candidates,
                    city,
                    postal,
                    postal is null ? 0.88 : 0.95,
                    "address",
                    match.Value);
            }
        }

        foreach (Match match in ExplicitPlacePattern.Matches(text))
        {
            var city = Clean(match.Groups["city"].Value);

            if (city.Length >= 2)
            {
                AddCandidate(
                    candidates,
                    city,
                    null,
                    0.90,
                    "explicit-label",
                    match.Value);
            }
        }

        foreach (var place in KnownPlaces)
        {
            var pattern = $@"(?<!\p{{L}}){Regex.Escape(place)}(?!\p{{L}})";

            foreach (Match match in Regex.Matches(
                text,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                AddCandidate(
                    candidates,
                    place,
                    null,
                    0.82,
                    "known-place",
                    match.Value);
            }
        }

        return candidates
            .GroupBy(x => x.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.Confidence)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.PostalCode))
                .First())
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCandidate(
        ICollection<DetectedPlace> candidates,
        string name,
        string? postalCode,
        double confidence,
        string source,
        string raw)
    {
        var cleanName = Clean(name);
        if (cleanName.Length < 2)
            return;

        var normalized = Normalize(cleanName);

        candidates.Add(new DetectedPlace(
            cleanName,
            normalized,
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode,
            confidence,
            source,
            raw.Trim()));
    }

    private static string Clean(string value)
    {
        var result = value ?? string.Empty;

        result = Regex.Replace(
            result,
            @"[ \t]+[A-ZÀ-ÖØ-Þ]{3,}(?:[ \t]+[A-ZÀ-ÖØ-Þ]{3,})+[ \t]*$",
            string.Empty,
            RegexOptions.CultureInvariant);

        result = Regex.Replace(result, @"\s+", " ").Trim();
        return result.Trim(' ', ',', ';', ':', '.', '-', '–', '—');
    }

    private static string Normalize(string value)
    {
        var text = value
            .ToLowerInvariant()
            .Replace("st-", "saint ")
            .Replace("st ", "saint ")
            .Normalize(NormalizationForm.FormD);

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
