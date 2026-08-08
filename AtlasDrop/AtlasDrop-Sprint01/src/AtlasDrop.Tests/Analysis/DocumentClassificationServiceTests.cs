using Xunit;
using AtlasDrop.Analysis.Classification;
using AtlasDrop.Analysis.Invoices;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Tests.Analysis;

public sealed class DocumentClassificationServiceTests
{
    private static DocumentClassificationService CreateService() =>
        new(new InvoiceDetectionService());

    [Fact]
    public void Classifies_invoice_from_content()
    {
        var result = CreateService().Classify(
            "document.pdf",
            "FACTURE N° FA-2026-001 Total TTC 125,50 EUR");

        Assert.Equal(DocumentType.Invoice, result.Type);
        Assert.True(result.Confidence >= 0.70);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void Classifies_quote()
    {
        var result = CreateService().Classify(
            "offre.pdf",
            "DEVIS n° DV-001 valable 30 jours");

        Assert.Equal(DocumentType.Quote, result.Type);
        Assert.True(result.Confidence >= 0.70);
    }

    [Fact]
    public void Classifies_contract()
    {
        var result = CreateService().Classify(
            "accord.docx",
            "CONTRAT entre les parties");

        Assert.Equal(DocumentType.Contract, result.Type);
    }

    [Fact]
    public void Classifies_purchase_order()
    {
        var result = CreateService().Classify(
            "commande.pdf",
            "BON DE COMMANDE n° CMD-2026-001");

        Assert.Equal(DocumentType.Order, result.Type);
    }

    [Theory]
    [InlineData("tableau.xlsx", DocumentType.Spreadsheet)]
    [InlineData("donnees.csv", DocumentType.Spreadsheet)]
    [InlineData("presentation.pptx", DocumentType.Presentation)]
    [InlineData("mail.msg", DocumentType.Email)]
    [InlineData("archive.zip", DocumentType.Archive)]
    [InlineData("photo.jpg", DocumentType.Image)]
    public void Classifies_strong_extension_types(
        string fileName,
        DocumentType expected)
    {
        var result = CreateService().Classify(fileName);

        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public void Content_can_outweigh_generic_pdf_extension()
    {
        var result = CreateService().Classify(
            "scan.pdf",
            "Relevé de compte bancaire - statement");

        Assert.Equal(DocumentType.Statement, result.Type);
    }

    [Fact]
    public void Unknown_document_stays_unknown()
    {
        var result = CreateService().Classify(
            "document.pdf",
            "Texte sans signal métier particulier.");

        Assert.Equal(DocumentType.Unknown, result.Type);
        Assert.True(result.Confidence < 0.5);
    }

    [Fact]
    public void Empty_file_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => CreateService().Classify(""));
    }

    [Fact]
    public void Reasons_only_describe_selected_type()
    {
        var result = CreateService().Classify(
            "document.pdf",
            "Contrat et convention entre les parties.");

        Assert.Equal(DocumentType.Contract, result.Type);
        Assert.All(
            result.Reasons,
            x => Assert.False(string.IsNullOrWhiteSpace(x)));
    }

    [Fact]
    public void Receipt_is_detected()
    {
        var result = CreateService().Classify(
            "scan.pdf",
            "Ticket de caisse - Total 12,50 EUR");

        Assert.Equal(DocumentType.Receipt, result.Type);
    }

    [Fact]
    public void Report_is_detected()
    {
        var result = CreateService().Classify(
            "rapport.pdf",
            "Compte rendu mensuel du projet");

        Assert.Equal(DocumentType.Report, result.Type);
    }
}
