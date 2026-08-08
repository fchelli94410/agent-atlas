using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class FolderScoringServiceTests
{
    private static FolderScoringService CreateService() =>
        new(new TextNormalizer(), maxSuggestedDepth: 4);

    [Fact]
    public void Excluded_folder_gets_zero_and_is_never_ranked()
    {
        var folder = Folder(
            "1", "Courbevoie", depth: 2, excluded: true);

        var context = Context(
            "Facture Courbevoie.pdf",
            places: new[] { "Courbevoie" });

        var service = CreateService();

        var score = service.Score(folder, context);
        var ranked = service.Rank(new[] { folder }, context);

        Assert.Equal(0d, score.Score);
        Assert.Empty(ranked);
    }

    [Fact]
    public void Folder_beyond_level_four_is_never_ranked()
    {
        var deep = Folder(
            "1", "EDF", depth: 5,
            companies: new[] { "EDF" });

        var context = Context(
            "Facture EDF.pdf",
            companies: new[] { "EDF" });

        var service = CreateService();

        var score = service.Score(deep, context);
        var ranked = service.Rank(new[] { deep }, context);

        Assert.Equal(0d, score.Score);
        Assert.Empty(ranked);
        Assert.Contains(
            score.Reasons,
            x => x.Contains("profondeur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Level_four_remains_eligible()
    {
        var folder = Folder(
            "1", "EDF", depth: 4,
            companies: new[] { "EDF" });

        var result = CreateService().Rank(
            new[] { folder },
            Context("Facture EDF.pdf", companies: new[] { "EDF" }));

        Assert.Single(result);
    }

    [Fact]
    public void Company_place_year_and_type_raise_score()
    {
        var strong = Folder(
            "1",
            "EDF Courbevoie 2026",
            depth: 2,
            quality: 0.9,
            places: new[] { "Courbevoie" },
            companies: new[] { "EDF" },
            years: new[] { 2026 },
            documentTypes: new Dictionary<DocumentType, int>
            {
                [DocumentType.Invoice] = 15
            });

        var weak = Folder(
            "2",
            "Clients",
            depth: 2,
            quality: 0.3);

        var context = Context(
            "Facture EDF Courbevoie 2026.pdf",
            type: DocumentType.Invoice,
            places: new[] { "Courbevoie" },
            companies: new[] { "EDF" },
            years: new[] { 2026 });

        var ranked = CreateService().Rank(
            new[] { weak, strong },
            context);

        Assert.Equal("1", ranked[0].Folder.Id);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void Previous_choices_raise_score()
    {
        var often = Folder(
            "1",
            "Clients",
            depth: 2,
            previousChoices: 12);

        var never = Folder(
            "2",
            "Clients",
            depth: 2,
            previousChoices: 0);

        var context = Context("document.pdf");

        var service = CreateService();

        var a = service.Score(often, context);
        var b = service.Score(never, context);

        Assert.True(a.Score > b.Score);
    }

    [Fact]
    public void Recent_use_raises_score()
    {
        var recent = Folder(
            "1",
            "Clients",
            depth: 2,
            lastUsedUtc: DateTime.UtcNow.AddDays(-2));

        var old = Folder(
            "2",
            "Clients",
            depth: 2,
            lastUsedUtc: DateTime.UtcNow.AddYears(-2));

        var context = Context("document.pdf");

        var service = CreateService();

        Assert.True(
            service.Score(recent, context).Score >
            service.Score(old, context).Score);
    }

    [Fact]
    public void Generic_folder_is_penalized()
    {
        var generic = Folder(
            "1",
            "Divers",
            depth: 2,
            quality: 0.8);

        var specific = Folder(
            "2",
            "Clients",
            depth: 2,
            quality: 0.8);

        var context = Context("document.pdf");

        var service = CreateService();

        Assert.True(
            service.Score(generic, context).Score <
            service.Score(specific, context).Score);
    }

    [Fact]
    public void Rank_returns_at_most_three_by_default()
    {
        var folders = Enumerable.Range(1, 10)
            .Select(i => Folder(
                i.ToString(),
                $"EDF {i}",
                depth: 2,
                companies: new[] { "EDF" }))
            .ToArray();

        var result = CreateService().Rank(
            folders,
            Context(
                "Facture EDF.pdf",
                companies: new[] { "EDF" }));

        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.True(x.Score > 0));
    }

    [Fact]
    public void Invalid_result_limit_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateService().Rank(
                Array.Empty<FolderCandidate>(),
                Context("document.pdf"),
                maxResults: 0));
    }

    private static FolderCandidate Folder(
        string id,
        string name,
        int depth,
        bool excluded = false,
        double quality = 0.5,
        DateTime? lastUsedUtc = null,
        int previousChoices = 0,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? places = null,
        IReadOnlyList<string>? companies = null,
        IReadOnlyList<int>? years = null,
        IReadOnlyDictionary<DocumentType, int>? documentTypes = null)
    {
        return new FolderCandidate(
            id,
            name,
            $@"C:\OneDrive\{name}",
            depth,
            excluded,
            quality,
            lastUsedUtc,
            previousChoices,
            keywords ?? Array.Empty<string>(),
            places ?? Array.Empty<string>(),
            companies ?? Array.Empty<string>(),
            years ?? Array.Empty<int>(),
            documentTypes ?? new Dictionary<DocumentType, int>());
    }

    private static FileSuggestionContext Context(
        string fileName,
        string text = "",
        DocumentType type = DocumentType.Unknown,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? places = null,
        IReadOnlyList<string>? companies = null,
        IReadOnlyList<int>? years = null)
    {
        return new FileSuggestionContext(
            fileName,
            text,
            type,
            keywords ?? Array.Empty<string>(),
            places ?? Array.Empty<string>(),
            companies ?? Array.Empty<string>(),
            years ?? Array.Empty<int>());
    }
}
