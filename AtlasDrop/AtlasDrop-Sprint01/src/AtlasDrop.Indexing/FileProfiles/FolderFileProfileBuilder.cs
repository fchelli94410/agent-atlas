using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Indexing.FileProfiles;

public sealed class FolderFileProfileBuilder : IFolderFileProfileBuilder
{
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "le", "la", "les", "de", "des", "du", "un", "une",
            "et", "ou", "a", "à", "au", "aux", "en", "pour",
            "sur", "avec", "dans", "par", "the", "of", "and", "for"
        };

    public FolderFileProfile Build(
        IEnumerable<IndexedFileSignal> files,
        int maxKeywords = 20)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (maxKeywords <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxKeywords));

        var list = files.ToArray();

        var documentTypes = list
            .Where(x => x.DocumentType != DocumentType.Unknown)
            .GroupBy(x => x.DocumentType)
            .ToDictionary(
                g => g.Key,
                g => g.Count());

        var extensions = list
            .Select(x => NormalizeExtension(x.Extension))
            .Where(x => x.Length > 0)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Count(),
                StringComparer.OrdinalIgnoreCase);

        var keywordCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in list)
        {
            var perFile = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var keyword in file.Keywords ?? Array.Empty<string>())
            {
                var clean = NormalizeKeyword(keyword);

                if (IsUsefulKeyword(clean))
                    perFile.Add(clean);
            }

            foreach (var token in Tokenize(file.NormalizedText))
            {
                if (IsUsefulKeyword(token))
                    perFile.Add(token);
            }

            foreach (var keyword in perFile)
            {
                keywordCounts.TryGetValue(keyword, out var count);
                keywordCounts[keyword] = count + 1;
            }
        }

        var frequentKeywords = keywordCounts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxKeywords)
            .Select(x => x.Key)
            .ToArray();

        return new FolderFileProfile(
            list.Length,
            documentTypes,
            extensions,
            frequentKeywords);
    }

    private static IEnumerable<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var token in text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var clean = NormalizeKeyword(token);

            if (clean.Length > 0)
                yield return clean;
        }
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var value = extension.Trim().ToLowerInvariant();

        return value.StartsWith(".", StringComparison.Ordinal)
            ? value
            : "." + value;
    }

    private static string NormalizeKeyword(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return string.Empty;

        var chars = keyword
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private static bool IsUsefulKeyword(string keyword)
    {
        return keyword.Length >= 3 &&
               !StopWords.Contains(keyword) &&
               !keyword.All(char.IsDigit);
    }
}
