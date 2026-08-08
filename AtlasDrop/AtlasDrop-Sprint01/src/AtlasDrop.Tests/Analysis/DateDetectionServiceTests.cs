using Xunit;
using AtlasDrop.Analysis.Dates;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Tests.Analysis;

public sealed class DateDetectionServiceTests
{
    [Theory]
    [InlineData("Facture du 2026-08-07", 2026, 8, 7)]
    [InlineData("Facture du 07/08/2026", 2026, 8, 7)]
    [InlineData("Facture du 07.08.2026", 2026, 8, 7)]
    public void Detects_precise_numeric_dates(
        string text,
        int year,
        int month,
        int day)
    {
        var result = new DateDetectionService().Detect(text);

        var date = Assert.Single(result);
        Assert.Equal(new DateTime(year, month, day), date.Value);
        Assert.Equal(DetectedDatePrecision.Day, date.Precision);
        Assert.True(date.Confidence >= 0.95);
    }

    [Theory]
    [InlineData("7 août 2026", 2026, 8, 7)]
    [InlineData("07 aout 2026", 2026, 8, 7)]
    [InlineData("1 janvier 2025", 2025, 1, 1)]
    public void Detects_french_long_dates(
        string text,
        int year,
        int month,
        int day)
    {
        var result = new DateDetectionService().Detect(text);

        var date = Assert.Single(result);
        Assert.Equal(new DateTime(year, month, day), date.Value);
        Assert.Equal(DetectedDatePrecision.Day, date.Precision);
    }

    [Theory]
    [InlineData("août 2026", 2026, 8)]
    [InlineData("aout 2026", 2026, 8)]
    [InlineData("08/2026", 2026, 8)]
    public void Detects_month_precision_without_inventing_day(
        string text,
        int year,
        int month)
    {
        var result = new DateDetectionService().Detect(text);

        var date = Assert.Single(result);
        Assert.Equal(new DateTime(year, month, 1), date.Value);
        Assert.Equal(DetectedDatePrecision.Month, date.Precision);
    }

    [Fact]
    public void Detects_year_only_without_inventing_month_or_day()
    {
        var result = new DateDetectionService().Detect("Budget 2026");

        var date = Assert.Single(result);
        Assert.Equal(new DateTime(2026, 1, 1), date.Value);
        Assert.Equal(DetectedDatePrecision.Year, date.Precision);
        Assert.True(date.Confidence < 0.8);
    }

    [Fact]
    public void Invalid_calendar_date_is_ignored()
    {
        var result = new DateDetectionService().Detect("31/02/2026");

        Assert.DoesNotContain(
            result,
            x => x.Precision == DetectedDatePrecision.Day);
    }

    [Fact]
    public void Full_date_does_not_duplicate_year_or_month()
    {
        var result = new DateDetectionService().Detect("07/08/2026");

        Assert.Single(result);
        Assert.Equal(DetectedDatePrecision.Day, result[0].Precision);
    }

    [Fact]
    public void Multiple_independent_dates_are_preserved()
    {
        var result = new DateDetectionService().Detect(
            "Contrat signé le 01/03/2025, renouvellement prévu en septembre 2026.");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x =>
            x.Value == new DateTime(2025, 3, 1) &&
            x.Precision == DetectedDatePrecision.Day);
        Assert.Contains(result, x =>
            x.Value == new DateTime(2026, 9, 1) &&
            x.Precision == DetectedDatePrecision.Month);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aucune date ici")]
    public void No_date_returns_empty(string? text)
    {
        var result = new DateDetectionService().Detect(text);

        Assert.Empty(result);
    }
}
