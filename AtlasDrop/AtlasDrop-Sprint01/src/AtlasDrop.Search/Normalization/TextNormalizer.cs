using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Search;

namespace AtlasDrop.Search.Normalization;

public sealed class TextNormalizer : ITextNormalizer
{
    private readonly IReadOnlyDictionary<string, string> _aliases;

    public TextNormalizer(NormalizationOptions? options = null)
    {
        _aliases = options?.Aliases
                   ?? new NormalizationOptions().Aliases;
    }

    public string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToLowerInvariant();

        text = ReplaceAliases(text);
        text = RemoveDiacritics(text);
        text = NormalizeSeparators(text);
        text = RemoveNoise(text);
        text = CollapseSpaces(text);
        text = ReplaceAliases(text);
        text = CollapseSpaces(text);

        return text.Trim();
    }

    private string ReplaceAliases(string value)
    {
        var result = value;

        foreach (var alias in _aliases
                     .OrderByDescending(x => x.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(alias.Key))
                continue;

            var pattern = $@"(?<!\p{{L}}){Regex.Escape(alias.Key.ToLowerInvariant())}(?!\p{{L}})";
            result = Regex.Replace(
                result,
                pattern,
                alias.Value.ToLowerInvariant(),
                RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);

            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeSeparators(string value)
    {
        return Regex.Replace(
            value,
            @"[-_/\\.,;:()\[\]{}]+",
            " ");
    }

    private static string RemoveNoise(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                builder.Append(ch);
            else
                builder.Append(' ');
        }

        return builder.ToString();
    }

    private static string CollapseSpaces(string value)
    {
        return Regex.Replace(
            value,
            @"\s+",
            " ").Trim();
    }
}
