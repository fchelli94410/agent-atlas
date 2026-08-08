using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class FolderScoringSimilarityTests
{
    [Fact]
    public void Existing_similar_files_raise_folder_score()
    {
        var service = new FolderScoringService(
            new TextNormalizer(),
            maxSuggestedDepth: 4);

        var context = new FileSuggestionContext(
            "Facture EDF Courbevoie.pdf",
            "",
            DocumentType.Invoice,
            new[] { "facture" },
            new[] { "Courbevoie" },
            new[] { "EDF" },
            new[] { 2026 });

        var similar = new FolderCandidate(
            "1",
            "Clients",
            @"C:\OneDrive\Clients",
            2,
            false,
            0.5,
            null,
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            new Dictionary<DocumentType, int>(),
            new[]
            {
                new SimilarFileProfile(
                    "Facture EDF 2026.pdf",
                    DocumentType.Invoice,
                    new[] { "facture" },
                    new[] { "Courbevoie" },
                    new[] { "EDF" },
                    new[] { 2026 })
            });

        var none = similar with
        {
            Id = "2",
            ExistingFiles = Array.Empty<SimilarFileProfile>()
        };

        var scoreWith = service.Score(similar, context);
        var scoreWithout = service.Score(none, context);

        Assert.True(scoreWith.Score > scoreWithout.Score);
        Assert.Contains(
            "fichiers similaires déjà présents",
            scoreWith.Reasons);
    }

    [Fact]
    public void Similar_files_never_bypass_depth_rule()
    {
        var service = new FolderScoringService(
            new TextNormalizer(),
            maxSuggestedDepth: 4);

        var context = new FileSuggestionContext(
            "Facture EDF.pdf",
            "",
            DocumentType.Invoice,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>());

        var deep = new FolderCandidate(
            "1",
            "EDF",
            @"C:\OneDrive\A\B\C\D\EDF",
            5,
            false,
            1.0,
            DateTime.UtcNow,
            100,
            new[] { "edf" },
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>(),
            new Dictionary<DocumentType, int>
            {
                [DocumentType.Invoice] = 100
            },
            new[]
            {
                new SimilarFileProfile(
                    "Facture EDF.pdf",
                    DocumentType.Invoice,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { "EDF" },
                    Array.Empty<int>())
            });

        var result = service.Rank(
            new[] { deep },
            context);

        Assert.Empty(result);
    }
}
