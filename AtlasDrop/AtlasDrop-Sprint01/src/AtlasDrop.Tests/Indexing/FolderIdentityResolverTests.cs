using Xunit;
using AtlasDrop.Core.Indexing;
using AtlasDrop.Infrastructure.Indexing;

namespace AtlasDrop.Tests.Indexing;

public sealed class FolderIdentityResolverTests
{
    private static readonly DateTime T =
        new(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rename_preserves_stable_id()
    {
        var previous = Old(
            "stable-001",
            @"C:\Root\Immobilier\AncienNom",
            "AncienNom",
            @"C:\Root\Immobilier",
            12,
            new[] { ".pdf", ".xlsx" });

        var current = Current(
            @"C:\Root\Immobilier\NouveauNom",
            "NouveauNom",
            @"C:\Root\Immobilier",
            12,
            new[] { ".xlsx", ".pdf" });

        var match = new FolderIdentityResolver().TryMatch(previous, current);

        Assert.NotNull(match);
        Assert.Equal("stable-001", match!.StableId);
        Assert.Equal(previous.FullPath, match.OldPath);
        Assert.Equal(current.FullPath, match.NewPath);
        Assert.True(match.Confidence >= 0.70);
    }

    [Fact]
    public void Move_preserves_stable_id()
    {
        var previous = Old(
            "stable-002",
            @"C:\Root\A\Factures",
            "Factures",
            @"C:\Root\A",
            8,
            new[] { ".pdf" });

        var current = Current(
            @"C:\Root\B\Factures",
            "Factures",
            @"C:\Root\B",
            8,
            new[] { ".pdf" });

        var match = new FolderIdentityResolver().TryMatch(previous, current);

        Assert.NotNull(match);
        Assert.Equal("stable-002", match!.StableId);
    }

    [Fact]
    public void Unrelated_folder_is_not_matched()
    {
        var previous = Old(
            "stable-003",
            @"C:\Root\A\Factures",
            "Factures",
            @"C:\Root\A",
            8,
            new[] { ".pdf" });

        var current = Current(
            @"C:\Root\B\Photos",
            "Photos",
            @"C:\Root\B",
            70,
            new[] { ".jpg" });

        var match = new FolderIdentityResolver().TryMatch(previous, current);

        Assert.Null(match);
    }

    [Fact]
    public void Same_path_is_certain_match()
    {
        var previous = Old(
            "stable-004",
            @"C:\Root\A",
            "A",
            @"C:\Root",
            1,
            new[] { ".pdf" });

        var current = Current(
            @"C:\ROOT\A",
            "A",
            @"C:\ROOT",
            2,
            new[] { ".txt" });

        var match = new FolderIdentityResolver().TryMatch(previous, current);

        Assert.NotNull(match);
        Assert.Equal(1.0, match!.Confidence);
    }

    [Fact]
    public void Reconciler_does_not_force_ambiguous_match()
    {
        var old = Old(
            "stable-005",
            @"C:\Root\Old\Factures",
            "Factures",
            @"C:\Root\Old",
            5,
            new[] { ".pdf" });

        var current1 = Current(
            @"C:\Root\A\Factures",
            "Factures",
            @"C:\Root\A",
            5,
            new[] { ".pdf" });

        var current2 = Current(
            @"C:\Root\B\Factures",
            "Factures",
            @"C:\Root\B",
            5,
            new[] { ".pdf" });

        var result = new FolderIdentityReconciler().Reconcile(
            new[] { old },
            new[] { current1, current2 });

        Assert.Empty(result.Matches);
        Assert.Equal(2, result.UnmatchedCurrent.Count);
        Assert.Single(result.UnmatchedPrevious);
    }

    [Fact]
    public void Reconciler_matches_unique_best_candidate()
    {
        var old = Old(
            "stable-006",
            @"C:\Root\A\Factures",
            "Factures",
            @"C:\Root\A",
            5,
            new[] { ".pdf" });

        var renamed = Current(
            @"C:\Root\A\Factures 2026",
            "Factures 2026",
            @"C:\Root\A",
            5,
            new[] { ".pdf" });

        var unrelated = Current(
            @"C:\Root\B\Photos",
            "Photos",
            @"C:\Root\B",
            50,
            new[] { ".jpg" });

        var result = new FolderIdentityReconciler().Reconcile(
            new[] { old },
            new[] { renamed, unrelated });

        Assert.Single(result.Matches);
        Assert.Equal("stable-006", result.Matches[0].StableId);
        Assert.Single(result.UnmatchedCurrent);
        Assert.Empty(result.UnmatchedPrevious);
    }

    private static FolderIdentityCandidate Old(
        string id,
        string path,
        string name,
        string parent,
        int count,
        IReadOnlyCollection<string> extensions) =>
        new(id, path, name, parent, T, T, count, extensions);

    private static FolderScanItem Current(
        string path,
        string name,
        string parent,
        int count,
        IReadOnlyCollection<string> extensions) =>
        new(path, name, parent, 2, T, T, count, extensions);
}
