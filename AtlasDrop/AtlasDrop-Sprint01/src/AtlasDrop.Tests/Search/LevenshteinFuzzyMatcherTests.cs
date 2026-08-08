using Xunit;
using AtlasDrop.Search.Fuzzy;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class LevenshteinFuzzyMatcherTests
{
    private static LevenshteinFuzzyMatcher CreateMatcher() =>
        new(new TextNormalizer());

    [Fact]
    public void Exact_match_has_similarity_one()
    {
        var matched = CreateMatcher().IsMatch(
            "courbevoie",
            "courbevoie",
            out var similarity);

        Assert.True(matched);
        Assert.Equal(1.0, similarity);
    }

    [Fact]
    public void One_typo_is_tolerated_on_medium_word()
    {
        var matched = CreateMatcher().IsMatch(
            "courbevoie",
            "courbevoei",
            out var similarity);

        Assert.True(matched);
        Assert.True(similarity >= 0.8);
    }

    [Fact]
    public void Two_typos_are_tolerated_on_longer_word()
    {
        var matched = CreateMatcher().IsMatch(
            "montévrain",
            "montevriin",
            out var similarity);

        Assert.True(matched);
        Assert.True(similarity >= 0.7);
    }

    [Theory]
    [InlineData("edf", "ef")]
    [InlineData("paris", "lyon")]
    [InlineData("contrat", "facture")]
    public void Unrelated_or_too_short_words_are_rejected(
        string candidate,
        string query)
    {
        var matched = CreateMatcher().IsMatch(
            candidate,
            query,
            out _);

        Assert.False(matched);
    }

    [Fact]
    public void Accent_normalization_still_applies()
    {
        var matched = CreateMatcher().IsMatch(
            "Montévrain",
            "montevrain",
            out var similarity);

        Assert.True(matched);
        Assert.Equal(1.0, similarity);
    }
}
