using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class FolderScoringHistoryTests
{
    [Fact]
    public void Favorable_history_raises_folder_score()
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
            Array.Empty<int>(),
            "invoice-edf");

        var baseFolder = Folder("A", history: Array.Empty<UserHistorySignal>());

        var favored = Folder(
            "A",
            history: new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Chosen,
                    DateTime.UtcNow.AddDays(-1),
                    "invoice-edf")
            });

        Assert.True(
            service.Score(favored, context).Score >
            service.Score(baseFolder, context).Score);
    }

    [Fact]
    public void Undo_can_lower_folder_score()
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
            Array.Empty<int>(),
            "invoice-edf");

        var neutral = Folder("A", history: Array.Empty<UserHistorySignal>());

        var undone = Folder(
            "A",
            history: new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Undo,
                    DateTime.UtcNow.AddHours(-1),
                    "invoice-edf")
            });

        Assert.True(
            service.Score(undone, context).Score <
            service.Score(neutral, context).Score);
    }

    [Fact]
    public void History_never_bypasses_exclusion_or_depth()
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
            Array.Empty<int>(),
            "invoice-edf");

        var hugeHistory = Enumerable.Range(0, 50)
            .Select(_ => new UserHistorySignal(
                "A",
                UserHistorySignalType.Chosen,
                DateTime.UtcNow,
                "invoice-edf"))
            .ToArray();

        var excluded = Folder("A", excluded: true, history: hugeHistory);
        var deep = Folder("A", depth: 5, history: hugeHistory);

        Assert.Empty(service.Rank(new[] { excluded }, context));
        Assert.Empty(service.Rank(new[] { deep }, context));
    }

    private static FolderCandidate Folder(
        string id,
        int depth = 2,
        bool excluded = false,
        IReadOnlyList<UserHistorySignal>? history = null)
    {
        return new FolderCandidate(
            id,
            "EDF",
            $@"C:\OneDrive\EDF\{id}",
            depth,
            excluded,
            0.5,
            null,
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "EDF" },
            Array.Empty<int>(),
            new Dictionary<DocumentType, int>
            {
                [DocumentType.Invoice] = 2
            },
            Array.Empty<SimilarFileProfile>(),
            history);
    }
}
