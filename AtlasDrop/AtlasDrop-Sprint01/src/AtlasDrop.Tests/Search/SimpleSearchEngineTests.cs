using Xunit;
using AtlasDrop.Core.Search;
using AtlasDrop.Search.Engine;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class SimpleSearchEngineTests
{
    private static SimpleSearchEngine CreateEngine() =>
        new(new TextNormalizer());

    [Fact]
    public void Search_is_case_insensitive()
    {
        var docs = new[]
        {
            Doc("1", "Courbevoie", @"C:\\OneDrive\\Courbevoie", "factures")
        };

        var result = CreateEngine().Search(docs, "COURBEVOIE");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
    }

    [Fact]
    public void Search_is_accent_insensitive()
    {
        var docs = new[]
        {
            Doc("1", "Montévrain", @"C:\\OneDrive\\Montévrain", "documents")
        };

        var result = CreateEngine().Search(docs, "montevrain");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
    }

    [Fact]
    public void Search_matches_normalized_aliases()
    {
        var docs = new[]
        {
            Doc("1", "Saint-Maurice", @"C:\\OneDrive\\Saint-Maurice", "contrats")
        };

        var result = CreateEngine().Search(docs, "stmaurice");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
    }

    [Fact]
    public void Search_can_match_indexed_content()
    {
        var docs = new[]
        {
            Doc("1", "Clients", @"C:\\OneDrive\\Clients", "edf facture courbevoie"),
            Doc("2", "Divers", @"C:\\OneDrive\\Divers", "notes")
        };

        var result = CreateEngine().Search(docs, "EDF");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
        Assert.Contains("contenu indexé contient la recherche", hit.Reasons);
    }

    [Fact]
    public void Exact_name_match_ranks_first()
    {
        var docs = new[]
        {
            Doc("1", "EDF", @"C:\\OneDrive\\EDF", "documents"),
            Doc("2", "Clients EDF", @"C:\\OneDrive\\Clients EDF", "edf")
        };

        var result = CreateEngine().Search(docs, "edf");

        Assert.Equal("1", result[0].Document.Id);
        Assert.Equal(1.0, result[0].Score);
    }

    [Fact]
    public void No_match_returns_empty()
    {
        var docs = new[]
        {
            Doc("1", "Paris", @"C:\\OneDrive\\Paris", "factures")
        };

        var result = CreateEngine().Search(docs, "lyon");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_query_returns_empty(string? query)
    {
        var docs = new[]
        {
            Doc("1", "Paris", @"C:\\OneDrive\\Paris", "factures")
        };

        var result = CreateEngine().Search(docs, query!);

        Assert.Empty(result);
    }

    [Fact]
    public void Result_limit_is_respected()
    {
        var docs = Enumerable.Range(1, 10)
            .Select(i => Doc(
                i.ToString(),
                $"Client EDF {i}",
                $@"C:\\OneDrive\\Client EDF {i}",
                "edf"))
            .ToArray();

        var result = CreateEngine().Search(
            docs,
            "edf",
            maxResults: 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Invalid_result_limit_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateEngine().Search(
                Array.Empty<SearchDocument>(),
                "edf",
                maxResults: 0));
    }

    private static SearchDocument Doc(
        string id,
        string name,
        string path,
        string text) =>
        new(id, name, path, text);
}
