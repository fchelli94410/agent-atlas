using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Naming;
using Xunit;

namespace AtlasDrop.Tests.Analysis;

public sealed class Sprint116RenameReliabilityTests
{
    private readonly FileRenameSuggestionService _service = new();

    [Fact]
    public void Courrier_from_original_name_is_preserved_and_unrelated_letter_date_is_rejected()
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

        Assert.StartsWith("courrier adressage", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1977-10-22", suggestion.ProposedFileName, StringComparison.Ordinal);
        Assert.Contains("date détectée ignorée", string.Join(' ', suggestion.Reasons), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exact_date_is_kept_for_a_structured_invoice()
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

        Assert.Contains("capex", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Facture", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-08-09", suggestion.ProposedFileName, StringComparison.Ordinal);
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

        Assert.DoesNotContain("1977", suggestion.ProposedFileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Date_already_present_in_the_original_filename_is_preserved_even_for_a_letter()
    {
        var context = new FileRenameContext(
            "2026-08-09-courrier-adressage.pdf",
            DocumentType.Letter,
            new DateTime(2026, 8, 9),
            2026,
            8,
            null,
            null,
            null,
            null);

        var suggestion = _service.Suggest(context);

        Assert.Contains("2026-08-09", suggestion.ProposedFileName, StringComparison.Ordinal);
        Assert.Contains("courrier", suggestion.ProposedFileName, StringComparison.OrdinalIgnoreCase);
    }
}
