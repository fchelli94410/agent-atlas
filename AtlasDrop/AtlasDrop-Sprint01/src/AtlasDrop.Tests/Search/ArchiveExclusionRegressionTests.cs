using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;
using Xunit;

namespace AtlasDrop.Tests.Search;

public sealed class ArchiveExclusionRegressionTests
{
    private static FolderScoringService Service() =>
        new(new TextNormalizer(), maxSuggestedDepth: 4);

    private static FileSuggestionContext Context() =>
        new(
            "attestation.pdf",
            "attestation travail franchise",
            DocumentType.Contract,
            new[] { "attestation", "travail", "franchise" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>());

    [Theory]
    [InlineData(@"C:\OneDrive\02 - Activités Professionnelles\11 - Archives\Franchise")]
    [InlineData(@"C:\OneDrive\02 - Activités Professionnelles\11 - Archive\Franchise")]
    [InlineData(@"C:\OneDrive\02 - Activités Professionnelles\Anciennes Archives 2024\Franchise")]
    [InlineData(@"C:\OneDrive\02 - Activités Professionnelles\ARCHIVES\Franchise")]
    public void Any_path_segment_containing_archive_is_never_ranked(string path)
    {
        var folder = new FolderCandidate(
            path,
            Path.GetFileName(path),
            path,
            3,
            false,
            1d,
            DateTime.UtcNow,
            50,
            new[] { "attestation", "travail", "franchise" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            new Dictionary<DocumentType, int> { [DocumentType.Contract] = 50 });

        var score = Service().Score(folder, Context());
        var ranked = Service().Rank(new[] { folder }, Context());

        Assert.True(score.Score <= 0d);
        Assert.Contains(score.Reasons, reason => reason.Contains("archive", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(ranked);
    }

    [Fact]
    public void Non_archive_folder_with_same_strong_signals_remains_eligible()
    {
        const string path = @"C:\OneDrive\02 - Activités Professionnelles\Franchise";
        var folder = new FolderCandidate(
            path,
            "Franchise",
            path,
            2,
            false,
            1d,
            DateTime.UtcNow,
            50,
            new[] { "attestation", "travail", "franchise" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            new Dictionary<DocumentType, int> { [DocumentType.Contract] = 50 });

        var ranked = Service().Rank(new[] { folder }, Context());

        Assert.Single(ranked);
        Assert.True(ranked[0].Score > 0d);
    }
}
