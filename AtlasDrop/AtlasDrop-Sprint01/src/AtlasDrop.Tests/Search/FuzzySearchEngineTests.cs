using Xunit;
using AtlasDrop.Core.Search;
using AtlasDrop.Search.Engine;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class FuzzySearchEngineTests
{
    private static SimpleSearchEngine CreateEngine() =>
        new(new TextNormalizer());

    [Fact]
    public void Search_tolerates_single_typo_in_name()
    {
        var docs = new[]
        {
            Doc("1", "Courbevoie", @"C:\OneDrive\Courbevoie", "factures")
        };

        var result = CreateEngine().Search(
            docs,
            "courbevoei");

        var hit = Assert.Single(result);

        Assert.Equal("1", hit.Document.Id);
        Assert.Contains(
            hit.Reasons,
            x => x.StartsWith("nom proche:", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_tolerates_typo_in_multi_word_query()
    {
        var docs = new[]
        {
            Doc(
                "1",
                "Factures EDF",
                @"C:\OneDrive\Courbevoie",
                "documents")
        };

        var result = CreateEngine().Search(
            docs,
            "edf courbevoei");

        var hit = Assert.Single(result);

        Assert.Equal("1", hit.Document.Id);
        Assert.Contains(
            "tous les termes correspondent",
            hit.Reasons);
    }

    [Fact]
    public void Exact_match_still_ranks_above_fuzzy_match()
    {
        var docs = new[]
        {
            Doc(
                "1",
                "Courbevoie",
                @"C:\OneDrive\Courbevoie",
                "documents"),
            Doc(
                "2",
                "Courbevoei",
                @"C:\OneDrive\Courbevoei",
                "documents")
        };

        var result = CreateEngine().Search(
            docs,
            "courbevoie");

        Assert.Equal("1", result[0].Document.Id);
        Assert.Equal(1.0, result[0].Score);
        Assert.True(result[1].Score < 1.0);
    }

    [Fact]
    public void Short_terms_are_not_fuzzy_matched()
    {
        var docs = new[]
        {
            Doc("1", "EDF", @"C:\OneDrive\EDF", "documents")
        };

        var result = CreateEngine().Search(
            docs,
            "ef");

        Assert.Empty(result);
    }

    [Fact]
    public void Unrelated_word_does_not_match()
    {
        var docs = new[]
        {
            Doc(
                "1",
                "Courbevoie",
                @"C:\OneDrive\Courbevoie",
                "factures")
        };

        var result = CreateEngine().Search(
            docs,
            "versailles");

        Assert.Empty(result);
    }

    private static SearchDocument Doc(
        string id,
        string name,
        string path,
        string text) =>
        new(id, name, path, text);
}
