using Xunit;
using AtlasDrop.Search.Normalization;

namespace AtlasDrop.Tests.Search;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Courbevoie", "courbevoie")]
    [InlineData("COURBEVOIE", "courbevoie")]
    [InlineData("Montévrain", "montevrain")]
    [InlineData("Montevrain", "montevrain")]
    [InlineData("Le Havre", "le havre")]
    public void Basic_normalization_works(string input, string expected)
    {
        var normalizer = new TextNormalizer();

        Assert.Equal(expected, normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Saint Maurice", "saint maurice")]
    [InlineData("Saint-Maurice", "saint maurice")]
    [InlineData("stmaurice", "saint maurice")]
    [InlineData("st maurice", "saint maurice")]
    public void Saint_maurice_variants_are_equivalent(string input, string expected)
    {
        var normalizer = new TextNormalizer();

        Assert.Equal(expected, normalizer.Normalize(input));
    }

    [Theory]
    [InlineData("EDF_Facture-2026.pdf", "edf facture 2026 pdf")]
    [InlineData("Facture   EDF", "facture edf")]
    [InlineData("  Facture\tEDF  ", "facture edf")]
    [InlineData("Paris/La Défense", "paris la defense")]
    public void Separators_and_spaces_are_normalized(string input, string expected)
    {
        var normalizer = new TextNormalizer();

        Assert.Equal(expected, normalizer.Normalize(input));
    }

    [Fact]
    public void Custom_aliases_are_supported()
    {
        var options = new AtlasDrop.Core.Search.NormalizationOptions
        {
            Aliases = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["alted"] = "altedis"
            }
        };

        var normalizer = new TextNormalizer(options);

        Assert.Equal("altedis facture", normalizer.Normalize("ALTED facture"));
    }

    [Fact]
    public void Null_or_empty_returns_empty()
    {
        var normalizer = new TextNormalizer();

        Assert.Equal(string.Empty, normalizer.Normalize(null));
        Assert.Equal(string.Empty, normalizer.Normalize(""));
        Assert.Equal(string.Empty, normalizer.Normalize("   "));
    }

    [Fact]
    public void Normalization_is_idempotent()
    {
        var normalizer = new TextNormalizer();

        var once = normalizer.Normalize("Saint-Maurice / EDF / 2026");
        var twice = normalizer.Normalize(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Accents_are_removed_without_losing_letters()
    {
        var normalizer = new TextNormalizer();

        Assert.Equal(
            "ete a arles",
            normalizer.Normalize("Été à Arles"));
    }
}
