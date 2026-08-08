using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Search;
using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Search.Suggestions;

public sealed class FileSimilarityService : IFileSimilarityService
{
    private readonly ITextNormalizer _normalizer;

    public FileSimilarityService(ITextNormalizer normalizer)
    {
        _normalizer = normalizer
            ?? throw new ArgumentNullException(nameof(normalizer));
    }

    public double Score(
        FileSuggestionContext context,
        SimilarFileProfile candidate,
        out IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);

        var score = 0d;
        var details = new List<string>();

        if (context.DocumentType != DocumentType.Unknown &&
            candidate.DocumentType == context.DocumentType)
        {
            score += 0.30;
            details.Add("même type documentaire");
        }

        var keywordMatches = CountMatches(
            context.Keywords,
            candidate.Keywords);

        if (keywordMatches > 0)
        {
            score += Math.Min(0.25, keywordMatches * 0.08);
            details.Add($"{keywordMatches} mot(s)-clé(s) commun(s)");
        }

        var placeMatches = CountMatches(
            context.Places,
            candidate.Places);

        if (placeMatches > 0)
        {
            score += Math.Min(0.15, placeMatches * 0.08);
            details.Add($"{placeMatches} lieu(x) commun(s)");
        }

        var companyMatches = CountMatches(
            context.Companies,
            candidate.Companies);

        if (companyMatches > 0)
        {
            score += Math.Min(0.20, companyMatches * 0.10);
            details.Add($"{companyMatches} entreprise(s) commune(s)");
        }

        if (context.Years.Count > 0 &&
            candidate.Years.Intersect(context.Years).Any())
        {
            score += 0.10;
            details.Add("année commune");
        }

        var fileName = _normalizer.Normalize(
            Path.GetFileNameWithoutExtension(context.FileName));
        var candidateName = _normalizer.Normalize(
            Path.GetFileNameWithoutExtension(candidate.FileName));

        if (fileName.Length >= 4 &&
            candidateName.Length >= 4 &&
            (fileName.Contains(candidateName, StringComparison.Ordinal) ||
             candidateName.Contains(fileName, StringComparison.Ordinal)))
        {
            score += 0.15;
            details.Add("nom de fichier proche");
        }

        reasons = details
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Math.Clamp(score, 0d, 1d);
    }

    private int CountMatches(
        IEnumerable<string> left,
        IEnumerable<string> right)
    {
        var rightSet = right
            .Select(_normalizer.Normalize)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return left
            .Select(_normalizer.Normalize)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count(rightSet.Contains);
    }
}
