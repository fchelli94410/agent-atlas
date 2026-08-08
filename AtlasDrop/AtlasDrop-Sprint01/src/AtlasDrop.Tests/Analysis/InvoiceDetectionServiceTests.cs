using Xunit;
using AtlasDrop.Analysis.Invoices;

namespace AtlasDrop.Tests.Analysis;

public sealed class InvoiceDetectionServiceTests
{
    [Fact]
    public void Detects_invoice_number_and_amount()
    {
        var result = new InvoiceDetectionService().Detect(
            "FACTURE N° FA-2026-001 Total TTC : 1 250,50 EUR");

        Assert.True(result.IsLikelyInvoice);
        Assert.Equal("FA-2026-001", result.InvoiceNumber);
        Assert.Contains(result.Amounts, x =>
            x.Value == 1250.50m &&
            x.Currency == "EUR");
        Assert.True(result.Confidence >= 0.8);
    }

    [Theory]
    [InlineData("125,50 €", 125.50, "EUR")]
    [InlineData("1 250,50 EUR", 1250.50, "EUR")]
    [InlineData("$99.99", 99.99, "USD")]
    [InlineData("250 GBP", 250.00, "GBP")]
    public void Detects_common_amount_formats(
        string text,
        double expectedValue,
        string expectedCurrency)
    {
        var result = new InvoiceDetectionService().Detect(text);

        Assert.Contains(result.Amounts, x =>
            x.Value == (decimal)expectedValue &&
            x.Currency == expectedCurrency);
    }

    [Fact]
    public void Amount_alone_is_not_enough_to_call_it_invoice()
    {
        var result = new InvoiceDetectionService().Detect(
            "Prix indicatif : 125,50 EUR");

        Assert.False(result.IsLikelyInvoice);
        Assert.NotEmpty(result.Amounts);
    }

    [Fact]
    public void Invoice_keywords_and_amount_make_invoice_likely()
    {
        var result = new InvoiceDetectionService().Detect(
            "Facture - Total TTC 450,00 EUR");

        Assert.True(result.IsLikelyInvoice);
        Assert.Contains("facture", result.Signals);
        Assert.NotEmpty(result.Amounts);
    }

    [Fact]
    public void Detects_invoice_number_without_accents_or_special_format()
    {
        var result = new InvoiceDetectionService().Detect(
            "Invoice # INV_2026_7788 Total 99 USD");

        Assert.Equal("INV-2026-7788", result.InvoiceNumber);
    }

    [Fact]
    public void Duplicate_amounts_are_deduplicated()
    {
        var result = new InvoiceDetectionService().Detect(
            "Facture 125,50 EUR puis rappel 125,50 €");

        Assert.Single(result.Amounts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Document administratif sans montant")]
    public void Non_invoice_text_returns_safe_result(string? text)
    {
        var result = new InvoiceDetectionService().Detect(text);

        Assert.False(result.IsLikelyInvoice);
        Assert.True(result.Confidence < 0.6);
    }

    [Fact]
    public void Strong_invoice_signal_without_amount_remains_detectable()
    {
        var result = new InvoiceDetectionService().Detect(
            "Facture n° FAC-7788 Net à payer");

        Assert.True(result.IsLikelyInvoice);
        Assert.Equal("FAC-7788", result.InvoiceNumber);
    }
}
