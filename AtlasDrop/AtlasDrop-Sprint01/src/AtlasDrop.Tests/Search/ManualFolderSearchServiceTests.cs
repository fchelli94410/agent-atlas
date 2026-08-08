using Xunit;
using AtlasDrop.Core.Search;
using AtlasDrop.Search.Engine;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class ManualFolderSearchServiceTests
{
    private static ManualFolderSearchService CreateService() =>
        new(new SimpleSearchEngine(new TextNormalizer()));

    [Fact]
    public void Manual_search_returns_engine_hits()
    {
        var documents = new[]
        {
            new SearchDocument(
                "1",
                "EDF Courbevoie",
                @"C:\OneDrive\Clients\EDF\Courbevoie",
                "factures")
        };

        var result = CreateService().Search(
            documents,
            "edf courbevoie");

        var hit = Assert.Single(result);

        Assert.Equal("1", hit.Id);
        Assert.Contains("EDF", hit.Name);
        Assert.True(hit.Score > 0);
    }

    [Fact]
    public void Manual_search_is_accent_and_case_insensitive()
    {
        var documents = new[]
        {
            new SearchDocument(
                "1",
                "Montévrain",
                @"C:\OneDrive\Montévrain",
                "contrats")
        };

        var result = CreateService().Search(
            documents,
            "MONTEVRAIN");

        Assert.Single(result);
    }

    [Fact]
    public void Manual_search_keeps_typo_tolerance()
    {
        var documents = new[]
        {
            new SearchDocument(
                "1",
                "Courbevoie",
                @"C:\OneDrive\Courbevoie",
                "documents")
        };

        var result = CreateService().Search(
            documents,
            "courbevoei");

        Assert.Single(result);
    }

    [Fact]
    public void Manual_search_respects_result_limit()
    {
        var documents = Enumerable.Range(1, 10)
            .Select(i => new SearchDocument(
                i.ToString(),
                $"EDF {i}",
                $@"C:\OneDrive\EDF\{i}",
                "edf"))
            .ToArray();

        var result = CreateService().Search(
            documents,
            "edf",
            maxResults: 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Empty_query_returns_no_results()
    {
        var result = CreateService().Search(
            Array.Empty<SearchDocument>(),
            "");

        Assert.Empty(result);
    }
}
