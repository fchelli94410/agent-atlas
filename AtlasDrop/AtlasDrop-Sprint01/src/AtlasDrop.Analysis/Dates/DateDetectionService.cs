using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Dates;

public sealed class DateDetectionService : IDateDetectionService
{
    private static readonly Regex IsoDate = new(
        @"(?<!\d)(?<y>19\d{2}|20\d{2})[-/.](?<m>0?[1-9]|1[0-2])[-/.](?<d>0?[1-9]|[12]\d|3[01])(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FrenchNumericDate = new(
        @"(?<!\d)(?<d>0?[1-9]|[12]\d|3[01])[-/.](?<m>0?[1-9]|1[0-2])[-/.](?<y>19\d{2}|20\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FrenchLongDate = new(
        @"(?<!\p{L})(?<d>0?[1-9]|[12]\d|3[01])\s+(?<month>janvier|février|fevrier|mars|avril|mai|juin|juillet|août|aout|septembre|octobre|novembre|décembre|decembre)\s+(?<y>19\d{2}|20\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FrenchMonthYear = new(
        @"(?<!\p{L})(?<month>janvier|février|fevrier|mars|avril|mai|juin|juillet|août|aout|septembre|octobre|novembre|décembre|decembre)\s+(?<y>19\d{2}|20\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MonthYear = new(
        @"(?<!\d)(?<m>0?[1-9]|1[0-2])[-/.](?<y>19\d{2}|20\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex YearOnly = new(
        @"(?<!\d)(?<y>19\d{2}|20\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<DetectedDate> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<DetectedDate>();

        var results = new List<DetectedDate>();
        var occupied = new List<(int Start, int Length)>();

        AddMatches(text, IsoDate, "text-iso", 0.98, DetectedDatePrecision.Day, results, occupied,
            m => TryCreateDate(m.Groups["y"].Value, m.Groups["m"].Value, m.Groups["d"].Value));

        AddMatches(text, FrenchNumericDate, "text-fr", 0.97, DetectedDatePrecision.Day, results, occupied,
            m => TryCreateDate(m.Groups["y"].Value, m.Groups["m"].Value, m.Groups["d"].Value));

        AddMatches(text, FrenchLongDate, "text-fr-long", 0.97, DetectedDatePrecision.Day, results, occupied,
            m =>
            {
                if (!int.TryParse(m.Groups["y"].Value, out var year) ||
                    !int.TryParse(m.Groups["d"].Value, out var day))
                    return null;

                return TryCreateDate(year, ParseFrenchMonth(m.Groups["month"].Value), day);
            });

        AddMatches(text, FrenchMonthYear, "text-fr-month", 0.88, DetectedDatePrecision.Month, results, occupied,
            m =>
            {
                if (!int.TryParse(m.Groups["y"].Value, out var year))
                    return null;

                return TryCreateDate(year, ParseFrenchMonth(m.Groups["month"].Value), 1);
            });

        AddMatches(text, MonthYear, "text-month-year", 0.86, DetectedDatePrecision.Month, results, occupied,
            m => TryCreateDate(m.Groups["y"].Value, m.Groups["m"].Value, "1"));

        AddMatches(text, YearOnly, "text-year", 0.72, DetectedDatePrecision.Year, results, occupied,
            m => TryCreateDate(m.Groups["y"].Value, "1", "1"));

        return results
            .OrderByDescending(x => x.Precision)
            .ThenByDescending(x => x.Confidence)
            .ThenBy(x => x.Value)
            .ToArray();
    }

    private static void AddMatches(
        string text,
        Regex regex,
        string source,
        double confidence,
        DetectedDatePrecision precision,
        ICollection<DetectedDate> results,
        ICollection<(int Start, int Length)> occupied,
        Func<Match, DateTime?> parser)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (IsOverlapping(match.Index, match.Length, occupied))
                continue;

            var value = parser(match);
            if (value is null)
                continue;

            results.Add(new DetectedDate(
                value,
                precision,
                confidence,
                source,
                match.Value));

            occupied.Add((match.Index, match.Length));
        }
    }

    private static bool IsOverlapping(
        int start,
        int length,
        IEnumerable<(int Start, int Length)> occupied)
    {
        var end = start + length;

        return occupied.Any(x =>
        {
            var otherEnd = x.Start + x.Length;
            return start < otherEnd && end > x.Start;
        });
    }

    private static DateTime? TryCreateDate(
        string year,
        string month,
        string day)
    {
        if (!int.TryParse(year, out var y) ||
            !int.TryParse(month, out var m) ||
            !int.TryParse(day, out var d))
            return null;

        return TryCreateDate(y, m, d);
    }

    private static DateTime? TryCreateDate(
        int year,
        int month,
        int day)
    {
        try
        {
            return new DateTime(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int ParseFrenchMonth(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "janvier" => 1,
            "février" or "fevrier" => 2,
            "mars" => 3,
            "avril" => 4,
            "mai" => 5,
            "juin" => 6,
            "juillet" => 7,
            "août" or "aout" => 8,
            "septembre" => 9,
            "octobre" => 10,
            "novembre" => 11,
            "décembre" or "decembre" => 12,
            _ => 0
        };
    }
}
