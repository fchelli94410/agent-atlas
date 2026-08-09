using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Naming;
using Xunit;

namespace AtlasDrop.Tests.Analysis;

public sealed class Sprint116RenameReliabilityTests
{
    private readonly FileRenameSuggestionService _service = new();

    [Fact]
    public void Meaningful_letter_name_is_preserved_and_unrelated_ocr_date_is_rejected()
    {
        var context = new FileRenameContext(
            "courrier-d-adressage.pdf",
            DocumentType.Letter,
            new DateTime(1977, 10, 22),
            1977,
            10,
            "Arles",
            "Mssante",
            null,
            null);

        var suggestion = _service.Suggest(context);

        Assert.Equal("courrier-d-adressage.pdf", suggestion.ProposedFileName);
        Assert.DoesNotContain("1977", suggestion.ProposedFileName);
        Assert.DoesNotContain("Arles", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mssante", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("date détectée ignorée", string.Join(' ', suggestion.Reasons), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Meaningful_invoice_name_is_not_enriched_from_ocr_even_if_document_is_structured()
    {
        var context = new FileRenameContext(
            "facture-capex.pdf",
            DocumentType.Invoice,
            new DateTime(2026, 8, 9),
            2026,
            8,
            null,
            "Capex",
            null,
            null);

        var suggestion = _service.Suggest(context);

        Assert.Equal("facture-capex.pdf", suggestion.ProposedFileName);
        Assert.DoesNotContain("2026", suggestion.ProposedFileName);
    }

    [Fact]
    public void Month_or_year_found_only_in_content_is_not_invented_in_the_filename()
    {
        var context = new FileRenameContext(
            "courrier-adressage.pdf",
            DocumentType.Letter,
            null,
            1977,
            10,
            null,
            null,
            null,
            null);

        var suggestion = _service.Suggest(context);

        Assert.Equal("courrier-adressage.pdf", suggestion.ProposedFileName);
        Assert.DoesNotContain("1977", suggestion.ProposedFileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Date_already_present_in_meaningful_original_filename_is_preserved_without_ocr_additions()
    {
        var context = new FileRenameContext(
            "2026-08-09-courrier-adressage.pdf",
            DocumentType.Letter,
            new DateTime(2026, 8, 9),
            2026,
            8,
            "Paris",
            "Entreprise OCR",
            null,
            null);

        var suggestion = _service.Suggest(context);

        Assert.Equal("2026-08-09-courrier-adressage.pdf", suggestion.ProposedFileName);
        Assert.DoesNotContain("Paris", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Entreprise OCR", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
    }
}
