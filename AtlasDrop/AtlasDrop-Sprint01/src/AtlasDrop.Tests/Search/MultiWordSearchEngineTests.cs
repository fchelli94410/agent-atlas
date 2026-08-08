using Xunit;
using AtlasDrop.Core.Search;
using AtlasDrop.Search.Engine;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class MultiWordSearchEngineTests
{
    private static SimpleSearchEngine CreateEngine() =>
        new(new TextNormalizer());

    [Fact]
    public void Matches_terms_across_name_path_and_content()
    {
        var docs = new[]
        {
            Doc("1", "Factures", @"C:\OneDrive\Clients\EDF", "courbevoie 2026")
        };

        var result = CreateEngine().Search(docs, "edf courbevoie");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
        Assert.Contains("tous les termes correspondent", hit.Reasons);
    }

    [Fact]
    public void Full_query_coverage_ranks_above_partial_match()
    {
        var docs = new[]
        {
            Doc("1", "EDF Courbevoie", @"C:\OneDrive\EDF\Courbevoie", "factures"),
            Doc("2", "EDF", @"C:\OneDrive\EDF", "factures")
        };

        var result = CreateEngine().Search(docs, "edf courbevoie");

        Assert.Equal("1", result[0].Document.Id);
        Assert.True(result[0].Score > result[1].Score);
    }

    [Fact]
    public void Exact_full_name_match_keeps_absolute_priority()
    {
        var docs = new[]
        {
            Doc("1", "EDF Courbevoie", @"C:\OneDrive\Clients", "documents"),
            Doc("2", "Clients EDF Courbevoie", @"C:\OneDrive\EDF\Courbevoie", "edf courbevoie")
        };

        var result = CreateEngine().Search(docs, "edf courbevoie");

        Assert.Equal("1", result[0].Document.Id);
        Assert.Equal(1.0, result[0].Score);
        Assert.True(result[1].Score < 1.0);
    }

    [Fact]
    public void Terms_are_accent_and_case_insensitive()
    {
        var docs = new[]
        {
            Doc("1", "Montévrain", @"C:\OneDrive\Saint-Maurice", "Contrats")
        };

        var result = CreateEngine().Search(docs, "MONTEVRAIN stmaurice");

        var hit = Assert.Single(result);
        Assert.Equal("1", hit.Document.Id);
        Assert.Contains("tous les termes correspondent", hit.Reasons);
    }

    [Fact]
    public void Repeated_query_terms_are_deduplicated()
    {
        var docs = new[]
        {
            Doc("1", "EDF", @"C:\OneDrive\EDF", "edf")
        };

        var once = CreateEngine().Search(docs, "edf");
        var repeated = CreateEngine().Search(docs, "edf edf edf");

        Assert.Single(once);
        Assert.Single(repeated);
        Assert.Equal(once[0].Score, repeated[0].Score);
    }

    [Fact]
    public void Partial_match_is_returned_but_scored_lower()
    {
        var docs = new[]
        {
            Doc("1", "EDF", @"C:\OneDrive\EDF", "factures")
        };

        var result = CreateEngine().Search(docs, "edf courbevoie");

        var hit = Assert.Single(result);
        Assert.True(hit.Score < 1.0);
        Assert.DoesNotContain("tous les termes correspondent", hit.Reasons);
    }

    [Fact]
    public void Three_word_query_prefers_three_word_match()
    {
        var docs = new[]
        {
            Doc("1", "Factures EDF Courbevoie", @"C:\OneDrive\Clients", "2026"),
            Doc("2", "Factures EDF", @"C:\OneDrive\Clients", "2026")
        };

        var result = CreateEngine().Search(docs, "factures edf courbevoie");

        Assert.Equal("1", result[0].Document.Id);
    }

    [Fact]
    public void No_term_match_returns_empty()
    {
        var docs = new[]
        {
            Doc("1", "Paris", @"C:\OneDrive\Paris", "contrats")
        };

        var result = CreateEngine().Search(docs, "edf courbevoie");

        Assert.Empty(result);
    }

    [Fact]
    public void Multi_word_search_respects_result_limit()
    {
        var docs = Enumerable.Range(1, 10)
            .Select(i => Doc(
                i.ToString(),
                $"EDF Courbevoie {i}",
                $@"C:\OneDrive\Clients\{i}",
                "factures"))
            .ToArray();

        var result = CreateEngine().Search(
            docs,
            "edf courbevoie",
            maxResults: 4);

        Assert.Equal(4, result.Count);
    }

    private static SearchDocument Doc(
        string id,
        string name,
        string path,
        string text) =>
        new(id, name, path, text);
}
