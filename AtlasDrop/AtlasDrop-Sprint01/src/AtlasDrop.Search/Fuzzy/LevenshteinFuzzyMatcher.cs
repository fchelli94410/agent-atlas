using AtlasDrop.Core.Search;

namespace AtlasDrop.Search.Fuzzy;

public sealed class LevenshteinFuzzyMatcher : IFuzzyMatcher
{
    private readonly ITextNormalizer _normalizer;

    public LevenshteinFuzzyMatcher(ITextNormalizer normalizer)
    {
        _normalizer = normalizer
            ?? throw new ArgumentNullException(nameof(normalizer));
    }

    public bool IsMatch(
        string candidate,
        string query,
        out double similarity)
    {
        similarity = 0d;

        var left = _normalizer.Normalize(candidate);
        var right = _normalizer.Normalize(query);

        if (left.Length == 0 || right.Length == 0)
            return false;

        if (left.Equals(right, StringComparison.Ordinal))
        {
            similarity = 1d;
            return true;
        }

        var distance = Distance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);

        similarity = 1d - ((double)distance / maxLength);

        var allowedDistance = GetAllowedDistance(right.Length);

        return distance <= allowedDistance &&
               similarity >= GetMinimumSimilarity(right.Length);
    }

    internal static int Distance(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;

        if (right.Length == 0)
            return left.Length;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;

                current[j] = Math.Min(
                    Math.Min(
                        current[j - 1] + 1,
                        previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static int GetAllowedDistance(int queryLength)
    {
        return queryLength switch
        {
            <= 3 => 0,
            <= 6 => 1,
            <= 12 => 2,
            _ => 3
        };
    }

    private static double GetMinimumSimilarity(int queryLength)
    {
        return queryLength switch
        {
            <= 3 => 1.00,
            <= 6 => 0.75,
            <= 12 => 0.72,
            _ => 0.70
        };
    }
}
