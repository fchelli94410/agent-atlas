using Xunit;
using AtlasDrop.Analysis.Places;

namespace AtlasDrop.Tests.Analysis;

public sealed class PlaceDetectionServiceTests
{
    [Fact]
    public void Detects_postal_code_and_city()
    {
        var result = new PlaceDetectionService().Detect(
            "Adresse : 92400 Courbevoie");

        var place = Assert.Single(result);

        Assert.Equal("Courbevoie", place.Name);
        Assert.Equal("92400", place.PostalCode);
        Assert.Equal("postal-city", place.Source);
        Assert.True(place.Confidence >= 0.95);
    }

    [Fact]
    public void Detects_explicit_place_label()
    {
        var result = new PlaceDetectionService().Detect(
            "Site : Nanterre");

        var place = Assert.Single(result);

        Assert.Equal("nanterre", place.NormalizedName);
        Assert.Equal("explicit-label", place.Source);
    }

    [Theory]
    [InlineData("Saint-Maurice", "saint maurice")]
    [InlineData("Saint Maurice", "saint maurice")]
    [InlineData("Montévrain", "montevrain")]
    [InlineData("Montevrain", "montevrain")]
    public void Normalizes_place_variants(
        string input,
        string expected)
    {
        var result = new PlaceDetectionService().Detect(input);

        var place = Assert.Single(result);

        Assert.Equal(expected, place.NormalizedName);
    }

    [Fact]
    public void Postal_city_has_priority_over_known_place()
    {
        var result = new PlaceDetectionService().Detect(
            "92400 Courbevoie");

        var place = Assert.Single(result);

        Assert.Equal("postal-city", place.Source);
        Assert.Equal("92400", place.PostalCode);
    }

    [Fact]
    public void Detects_multiple_places()
    {
        var result = new PlaceDetectionService().Detect(
            "Départ Paris, arrivée Courbevoie.");

        Assert.Contains(result, x => x.NormalizedName == "paris");
        Assert.Contains(result, x => x.NormalizedName == "courbevoie");
    }

    [Fact]
    public void Detects_address_city()
    {
        var result = new PlaceDetectionService().Detect(
            "12 avenue Gambetta 75020 Paris");

        Assert.Contains(result, x =>
            x.NormalizedName == "paris" &&
            x.PostalCode == "75020");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aucun lieu identifiable")]
    public void No_place_returns_empty(string? text)
    {
        var result = new PlaceDetectionService().Detect(text);

        Assert.Empty(result);
    }

    [Fact]
    public void Accents_do_not_change_identity()
    {
        var service = new PlaceDetectionService();

        var a = service.Detect("Montévrain");
        var b = service.Detect("Montevrain");

        Assert.Equal(a[0].NormalizedName, b[0].NormalizedName);
    }

    [Fact]
    public void Postal_city_does_not_absorb_next_line_company()
    {
        var result = new PlaceDetectionService().Detect(
            "94220 Charenton le Pont\r\nETHICA GESTION");

        Assert.Contains(result, x => x.Name == "Charenton le Pont");
        Assert.DoesNotContain(result, x =>
            x.Name.Contains("ETHICA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Postal_city_does_not_absorb_uppercase_company_on_same_line()
    {
        var result = new PlaceDetectionService().Detect(
            "94220 Charenton le Pont ETHICA GESTION");

        Assert.Contains(result, x => x.Name == "Charenton le Pont");
        Assert.DoesNotContain(result, x =>
            x.Name.Contains("ETHICA", StringComparison.OrdinalIgnoreCase));
    }
}
