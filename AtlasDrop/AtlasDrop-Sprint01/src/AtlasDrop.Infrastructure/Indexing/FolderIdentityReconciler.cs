using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class FolderIdentityReconciler
{
    private readonly IFolderIdentityResolver _resolver;

    public FolderIdentityReconciler(IFolderIdentityResolver? resolver = null)
    {
        _resolver = resolver ?? new FolderIdentityResolver();
    }

    public FolderReconciliationResult Reconcile(
        IReadOnlyCollection<FolderIdentityCandidate> previous,
        IReadOnlyCollection<FolderScanItem> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var matches = new List<FolderIdentityMatch>();
        var usedCurrent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedPrevious = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var oldFolder in previous)
        {
            var candidates = current
                .Where(x => !usedCurrent.Contains(Normalize(x.FullPath)))
                .Select(x => new
                {
                    Current = x,
                    Match = _resolver.TryMatch(oldFolder, x)
                })
                .Where(x => x.Match is not null)
                .OrderByDescending(x => x.Match!.Confidence)
                .ToArray();

            if (candidates.Length == 0)
                continue;

            // Évite une association ambiguë : deux meilleurs scores identiques.
            if (candidates.Length > 1 &&
                Math.Abs(candidates[0].Match!.Confidence -
                         candidates[1].Match!.Confidence) < 0.001)
                continue;

            var best = candidates[0];
            matches.Add(best.Match!);

            usedCurrent.Add(Normalize(best.Current.FullPath));
            usedPrevious.Add(oldFolder.StableId);
        }

        var unmatchedCurrent = current
            .Where(x => !usedCurrent.Contains(Normalize(x.FullPath)))
            .ToArray();

        var unmatchedPrevious = previous
            .Where(x => !usedPrevious.Contains(x.StableId))
            .ToArray();

        return new FolderReconciliationResult(
            matches,
            unmatchedCurrent,
            unmatchedPrevious);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
