using Xunit;
using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Naming;

namespace AtlasDrop.Tests.Naming;

public sealed class FileRenameSuggestionServiceTests
{
    private static FileRenameSuggestionService Service() => new();

    [Fact]
    public void Exact_date_uses_full_iso_date()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Invoice,
                new DateTime(2026, 8, 7),
                null,
                null,
                "Courbevoie",
                "EDF",
                null,
                null));

        Assert.StartsWith(
            "2026-08-07 - Facture - Courbevoie - EDF",
            result.ProposedFileName);
    }

    [Fact]
    public void Month_precision_never_invents_day()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Invoice,
                null,
                2026,
                8,
                "Courbevoie",
                "EDF",
                null,
                null));

        Assert.StartsWith(
            "2026-08 - Facture",
            result.ProposedFileName);

        Assert.DoesNotContain(
            "2026-08-01",
            result.ProposedFileName);
    }

    [Fact]
    public void Year_precision_never_invents_month()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Contract,
                null,
                2026,
                null,
                null,
                "EDF",
                null,
                null));

        Assert.StartsWith(
            "2026 - Contrat",
            result.ProposedFileName);
    }

    [Fact]
    public void Extension_is_preserved()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.PDF",
                DocumentType.Invoice,
                null,
                2026,
                null,
                null,
                "EDF",
                null,
                null));

        Assert.EndsWith(".PDF", result.ProposedFileName);
    }

    [Fact]
    public void Invalid_windows_characters_are_cleaned()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Invoice,
                null,
                2026,
                null,
                "Paris/France",
                "EDF|France",
                null,
                null));

        Assert.DoesNotContain("/", result.ProposedFileName);
        Assert.DoesNotContain("|", result.ProposedFileName);
    }

    [Fact]
    public void Detail_has_priority_over_company_and_reference()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Invoice,
                null,
                2026,
                null,
                null,
                "EDF",
                "Abonnement électricité",
                "FAC-123"));

        Assert.Contains(
            "Abonnement électricité",
            result.ProposedFileName);

        Assert.DoesNotContain(
            "FAC-123",
            result.ProposedFileName);
    }

    [Fact]
    public void Junk_name_is_replaced_when_useful_data_exists()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "document1.pdf",
                DocumentType.Invoice,
                null,
                2026,
                null,
                null,
                "EDF",
                null,
                null));

        Assert.True(result.Changed);
        Assert.Contains(
            "nom source peu informatif remplacé",
            result.Reasons);
    }

    [Fact]
    public void No_metadata_only_cleans_existing_name()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "  mon   fichier  .txt",
                DocumentType.Unknown,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

        Assert.Equal(
            "mon fichier.txt",
            result.ProposedFileName);
    }

    [Fact]
    public void Proposed_name_is_limited_in_length()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Report,
                null,
                2026,
                null,
                null,
                null,
                new string('A', 300),
                null));

        Assert.True(
            Path.GetFileNameWithoutExtension(
                result.ProposedFileName).Length <= 150);
    }
    [Fact]
    public void Reliable_original_name_words_are_prioritized_before_ocr_metadata()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "2021-04-09 - AXA - contrat habitation.pdf",
                DocumentType.Contract,
                new DateTime(2021, 4, 9),
                null,
                null,
                "COURBEVOIEType de bien",
                "FRANCK CHELLI144 A",
                null,
                null));

        Assert.StartsWith(
            "2021-04-09 - AXA habitation - Contrat",
            result.ProposedFileName);
        Assert.Contains(
            "mots fiables du nom source prioritaires",
            result.Reasons);
    }

}
