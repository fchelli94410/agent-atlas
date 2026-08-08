using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Search;
using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Search.Suggestions;

public sealed class FolderScoringService : IFolderScoringService
{
    private readonly ITextNormalizer _normalizer;
    private readonly IFileSimilarityService _fileSimilarity;
    private readonly IUserHistoryScoringService _historyScoring;
    private readonly ISuggestionConfidenceService _confidenceService;
    private readonly int _maxSuggestedDepth;

    public FolderScoringService(
        ITextNormalizer normalizer,
        int maxSuggestedDepth = 4,
        IFileSimilarityService? fileSimilarity = null,
        IUserHistoryScoringService? historyScoring = null,
        ISuggestionConfidenceService? confidenceService = null)
    {
        _normalizer = normalizer
            ?? throw new ArgumentNullException(nameof(normalizer));

        _fileSimilarity = fileSimilarity
            ?? new FileSimilarityService(_normalizer);

        _historyScoring = historyScoring
            ?? new UserHistoryScoringService();

        _confidenceService = confidenceService
            ?? new SuggestionConfidenceService();

        if (maxSuggestedDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSuggestedDepth));

        _maxSuggestedDepth = maxSuggestedDepth;
    }

    public FolderScoreResult Score(
        FolderCandidate folder,
        FileSuggestionContext context)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(context);

        if (folder.IsExcluded)
        {
            return new FolderScoreResult(
                folder,
                0d,
                new[] { "dossier exclu" },
                _confidenceService.Evaluate(0d));
        }

        if (folder.Depth > _maxSuggestedDepth)
        {
            return new FolderScoreResult(
                folder,
                0d,
                new[] { $"profondeur {folder.Depth} supérieure à {_maxSuggestedDepth}" },
                _confidenceService.Evaluate(0d));
        }

        var score = 0d;
        var reasons = new List<string>();

        var folderName = _normalizer.Normalize(folder.Name);
        var fileName = _normalizer.Normalize(
            Path.GetFileNameWithoutExtension(context.FileName));
        var text = _normalizer.Normalize(context.NormalizedText);

        if (ContainsUsefulMatch(fileName, folderName))
        {
            score += 0.24;
            reasons.Add("nom du fichier proche du dossier");
        }

        if (ContainsUsefulMatch(text, folderName))
        {
            score += 0.14;
            reasons.Add("contenu proche du nom du dossier");
        }

        var keywordMatches = CountMatches(
            context.Keywords,
            folder.Keywords);

        if (keywordMatches > 0)
        {
            score += Math.Min(0.18, keywordMatches * 0.06);
            reasons.Add($"{keywordMatches} mot(s)-clé(s) commun(s)");
        }

        var placeMatches = CountMatches(
            context.Places,
            folder.Places);

        if (placeMatches > 0)
        {
            score += Math.Min(0.16, placeMatches * 0.08);
            reasons.Add($"{placeMatches} lieu(x) commun(s)");
        }

        var companyMatches = CountMatches(
            context.Companies,
            folder.Companies);

        if (companyMatches > 0)
        {
            score += Math.Min(0.18, companyMatches * 0.09);
            reasons.Add($"{companyMatches} entreprise(s) commune(s)");
        }

        if (context.Years.Count > 0 &&
            folder.Years.Intersect(context.Years).Any())
        {
            score += 0.08;
            reasons.Add("année correspondante");
        }

        if (context.DocumentType != DocumentType.Unknown &&
            folder.DocumentTypeCounts.TryGetValue(
                context.DocumentType,
                out var typeCount) &&
            typeCount > 0)
        {
            score += Math.Min(
                0.16,
                0.08 + Math.Log10(typeCount + 1) * 0.04);

            reasons.Add(
                $"{typeCount} fichier(s) du même type");
        }

        if (folder.ExistingFiles is { Count: > 0 })
        {
            var similarities = folder.ExistingFiles
                .Select(x =>
                {
                    var similarity = _fileSimilarity.Score(
                        context,
                        x,
                        out var similarityReasons);

                    return new
                    {
                        Score = similarity,
                        Reasons = similarityReasons
                    };
                })
                .Where(x => x.Score > 0d)
                .OrderByDescending(x => x.Score)
                .Take(3)
                .ToArray();

            if (similarities.Length > 0)
            {
                var best = similarities[0].Score;
                var averageTop = similarities.Average(x => x.Score);

                score += Math.Min(
                    0.22,
                    best * 0.14 + averageTop * 0.08);

                reasons.Add("fichiers similaires déjà présents");
            }
        }

        if (folder.PreviousChoiceCount > 0)
        {
            score += Math.Min(
                0.14,
                0.04 + Math.Log10(folder.PreviousChoiceCount + 1) * 0.05);

            reasons.Add(
                $"{folder.PreviousChoiceCount} choix précédent(s)");
        }

        if (folder.HistorySignals is { Count: > 0 })
        {
            var historyAdjustment = _historyScoring.GetAdjustment(
                folder.Id,
                folder.HistorySignals,
                context.HistoryContextKey,
                out var historyReasons);

            if (Math.Abs(historyAdjustment) > 0.0001)
            {
                score += historyAdjustment;

                reasons.Add(
                    historyAdjustment > 0
                        ? "historique utilisateur favorable"
                        : "historique utilisateur défavorable");

                reasons.AddRange(historyReasons);
            }
        }

        if (folder.LastUsedUtc is not null)
        {
            var age = DateTime.UtcNow - folder.LastUsedUtc.Value;

            if (age <= TimeSpan.FromDays(30))
            {
                score += 0.05;
                reasons.Add("dossier utilisé récemment");
            }
        }

        score += Math.Clamp(folder.QualityScore, 0d, 1d) * 0.08;

        if (folder.QualityScore >= 0.75)
            reasons.Add("bonne qualité de dossier");

        // Pénalités V1 : profondeur, faible qualité et noms trop génériques.
        if (folder.Depth >= _maxSuggestedDepth)
        {
            score -= 0.05;
            reasons.Add("profondeur maximale autorisée");
        }

        if (folder.QualityScore < 0.25)
        {
            score -= 0.10;
            reasons.Add("qualité de dossier faible");
        }

        if (IsGenericName(folderName))
        {
            score -= 0.10;
            reasons.Add("nom de dossier générique");
        }

        score = Math.Clamp(score, 0d, 1d);

        return new FolderScoreResult(
            folder,
            score,
            reasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _confidenceService.Evaluate(score));
    }

    public IReadOnlyList<FolderScoreResult> Rank(
        IEnumerable<FolderCandidate> folders,
        FileSuggestionContext context,
        int maxResults = 3)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(context);

        if (maxResults <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        return folders
            .Select(x => Score(x, context))
            .Where(x =>
                !x.Folder.IsExcluded &&
                x.Folder.Depth <= _maxSuggestedDepth &&
                x.Score > 0d)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Folder.Depth)
            .ThenBy(x => x.Folder.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    private int CountMatches(
        IEnumerable<string> source,
        IEnumerable<string> target)
    {
        var targetSet = target
            .Select(_normalizer.Normalize)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return source
            .Select(_normalizer.Normalize)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count(targetSet.Contains);
    }

    private static bool ContainsUsefulMatch(
        string haystack,
        string needle)
    {
        return needle.Length >= 3 &&
               haystack.Contains(
                   needle,
                   StringComparison.Ordinal);
    }

    private static bool IsGenericName(string normalizedName)
    {
        return normalizedName is
            "divers" or
            "documents" or
            "fichiers" or
            "temp" or
            "tmp" or
            "nouveau dossier" or
            "autres";
    }
}
