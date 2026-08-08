using AtlasDrop.Core.Search;
using AtlasDrop.Search.Fuzzy;

namespace AtlasDrop.Search.Engine;

public sealed class SimpleSearchEngine : ISimpleSearchEngine
{
    private readonly ITextNormalizer _normalizer;
    private readonly IFuzzyMatcher _fuzzyMatcher;

    public SimpleSearchEngine(ITextNormalizer normalizer)
        : this(
            normalizer,
            new LevenshteinFuzzyMatcher(normalizer))
    {
    }

    public SimpleSearchEngine(
        ITextNormalizer normalizer,
        IFuzzyMatcher fuzzyMatcher)
    {
        _normalizer = normalizer
            ?? throw new ArgumentNullException(nameof(normalizer));

        _fuzzyMatcher = fuzzyMatcher
            ?? throw new ArgumentNullException(nameof(fuzzyMatcher));
    }

    public IReadOnlyList<SearchHit> Search(
        IEnumerable<SearchDocument> documents,
        string query,
        int maxResults = 20)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchHit>();

        if (maxResults <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        var normalizedQuery = _normalizer.Normalize(query);

        if (normalizedQuery.Length == 0)
            return Array.Empty<SearchHit>();

        var terms = normalizedQuery
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (terms.Length == 0)
            return Array.Empty<SearchHit>();

        var canonicalQuery = string.Join(" ", terms);
        var hits = new List<SearchHit>();

        foreach (var document in documents)
        {
            if (document is null)
                continue;

            var normalizedName = _normalizer.Normalize(document.Name);
            var normalizedPath = _normalizer.Normalize(document.FullPath);
            var normalizedText = _normalizer.Normalize(document.SearchText);

            var exactFullNameMatch = normalizedName.Equals(
                canonicalQuery,
                StringComparison.Ordinal);

            var matchedTerms = new HashSet<string>(StringComparer.Ordinal);
            var reasons = new List<string>();
            var accumulatedScore = 0d;

            foreach (var term in terms)
            {
                var termScore = 0d;
                var matched = false;

                if (normalizedName.Equals(term, StringComparison.Ordinal))
                {
                    termScore += 1.00;
                    matched = true;
                    reasons.Add($"nom exact: {term}");
                }
                else if (normalizedName.Contains(term, StringComparison.Ordinal))
                {
                    termScore += 0.70;
                    matched = true;
                    reasons.Add($"nom contient: {term}");
                }
                else if (TryFuzzyMatchAnyToken(normalizedName, term, out var nameSimilarity))
                {
                    termScore += 0.50 * nameSimilarity;
                    matched = true;
                    reasons.Add($"nom proche: {term}");
                }

                if (normalizedPath.Contains(term, StringComparison.Ordinal))
                {
                    termScore += 0.35;
                    matched = true;
                    reasons.Add($"chemin contient: {term}");
                }
                else if (TryFuzzyMatchAnyToken(normalizedPath, term, out var pathSimilarity))
                {
                    termScore += 0.20 * pathSimilarity;
                    matched = true;
                    reasons.Add($"chemin proche: {term}");
                }

                if (normalizedText.Contains(term, StringComparison.Ordinal))
                {
                    termScore += 0.55;
                    matched = true;

                    reasons.Add(
                        terms.Length == 1
                            ? "contenu indexé contient la recherche"
                            : $"contenu contient: {term}");
                }
                else if (TryFuzzyMatchAnyToken(normalizedText, term, out var textSimilarity))
                {
                    termScore += 0.30 * textSimilarity;
                    matched = true;
                    reasons.Add($"contenu proche: {term}");
                }

                if (matched)
                {
                    matchedTerms.Add(term);
                    accumulatedScore += Math.Min(termScore, 1.0);
                }
            }

            if (matchedTerms.Count == 0)
                continue;

            var coverage = (double)matchedTerms.Count / terms.Length;

            if (coverage >= 1.0)
            {
                accumulatedScore += 0.40;
                reasons.Add("tous les termes correspondent");
            }
            else if (coverage >= 0.5)
            {
                accumulatedScore += 0.15;
                reasons.Add("correspondance partielle multi-mots");
            }

            var normalizedScore = Math.Clamp(
                (accumulatedScore / terms.Length) * 0.75 +
                coverage * 0.25,
                0d,
                0.99);

            if (exactFullNameMatch)
            {
                normalizedScore = 1.0;
                reasons.Add("nom exact de la requête complète");
            }

            hits.Add(new SearchHit(
                document,
                normalizedScore,
                reasons
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()));
        }

        return hits
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Document.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    private bool TryFuzzyMatchAnyToken(
        string candidate,
        string term,
        out double bestSimilarity)
    {
        bestSimilarity = 0d;

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var tokens = candidate.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        var matched = false;

        foreach (var token in tokens)
        {
            if (!_fuzzyMatcher.IsMatch(
                token,
                term,
                out var similarity))
            {
                continue;
            }

            matched = true;
            bestSimilarity = Math.Max(
                bestSimilarity,
                similarity);
        }

        return matched;
    }
}
