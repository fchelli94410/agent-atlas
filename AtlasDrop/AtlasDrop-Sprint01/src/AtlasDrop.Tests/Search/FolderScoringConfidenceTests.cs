using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class FolderScoringConfidenceTests
{
    [Fact]
    public void Scoring_result_contains_confidence()
    {
        var service = new FolderScoringService(
            new TextNormalizer(),
            maxSuggestedDepth: 4);

        var folder = new FolderCandidate(
            "1",
            "EDF Courbevoie",
            @"C:\OneDrive\EDF Courbevoie",
            2,
            false,
            0.9,
            DateTime.UtcNow,
            10,
            new[] { "facture" },
            new[] { "Courbevoie" },
            new[] { "EDF" },
            new[] { 2026 },
            new Dictionary<DocumentType, int>
            {
                [DocumentType.Invoice] = 20
            });

        var context = new FileSuggestionContext(
            "Facture EDF Courbevoie 2026.pdf",
            "Facture EDF Courbevoie",
            DocumentType.Invoice,
            new[] { "facture" },
            new[] { "Courbevoie" },
            new[] { "EDF" },
            new[] { 2026 });

        var result = service.Score(folder, context);

        Assert.NotNull(result.Confidence);
        Assert.Equal(result.Score, result.Confidence!.Score);
    }

    [Fact]
    public void Excluded_folder_has_low_confidence()
    {
        var service = new FolderScoringService(
            new TextNormalizer(),
            maxSuggestedDepth: 4);

        var folder = new FolderCandidate(
            "1",
            "EDF",
            @"C:\OneDrive\EDF",
            2,
            true,
            1.0,
            DateTime.UtcNow,
            100,
            new[] { "edf" },
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>(),
            new Dictionary<DocumentType, int>());

        var context = new FileSuggestionContext(
            "EDF.pdf",
            "",
            DocumentType.Unknown,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>());

        var result = service.Score(folder, context);

        Assert.Equal(0d, result.Score);
        Assert.Equal(
            SuggestionConfidenceLevel.Low,
            result.Confidence!.Level);
        Assert.False(result.Confidence.CanAutoClassify);
    }

    [Fact]
    public void Deep_folder_can_never_receive_high_confidence()
    {
        var service = new FolderScoringService(
            new TextNormalizer(),
            maxSuggestedDepth: 4);

        var folder = new FolderCandidate(
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
            });

        var context = new FileSuggestionContext(
            "Facture EDF.pdf",
            "",
            DocumentType.Invoice,
            new[] { "edf" },
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>());

        var result = service.Score(folder, context);

        Assert.Equal(
            SuggestionConfidenceLevel.Low,
            result.Confidence!.Level);
        Assert.False(result.Confidence.CanAutoClassify);
    }
}
