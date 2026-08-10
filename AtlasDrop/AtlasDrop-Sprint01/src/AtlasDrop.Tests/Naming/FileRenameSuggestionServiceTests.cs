using Xunit;
using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Naming;

namespace AtlasDrop.Tests.Naming;

public sealed class FileRenameSuggestionServiceTests
{
    private static FileRenameSuggestionService Service() => new();

    [Fact]
    public void Generic_scan_can_use_reliable_full_date_and_metadata()
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
    public void Generic_scan_month_precision_never_invents_day()
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

        Assert.StartsWith("2026-08 - Facture", result.ProposedFileName);
        Assert.DoesNotContain("2026-08-01", result.ProposedFileName);
    }

    [Fact]
    public void Generic_scan_year_precision_never_invents_month()
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

        Assert.StartsWith("2026 - Contrat", result.ProposedFileName);
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
    public void Invalid_windows_characters_are_cleaned_for_generic_source()
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
    public void Detail_has_priority_over_company_and_reference_for_generic_source()
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

        Assert.Contains("Abonnement électricité", result.ProposedFileName);
        Assert.DoesNotContain("FAC-123", result.ProposedFileName);
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
        Assert.Contains("nom source peu informatif remplacé", result.Reasons);
    }

    [Fact]
    public void Meaningful_attestation_name_never_receives_ocr_date_or_words()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "attestation.pdf",
                DocumentType.Contract,
                new DateTime(2024, 12, 31),
                2024,
                12,
                "Montreuil CedexNous contacterCourriel",
                "ARESIA-Valenton",
                "Contrat",
                "1945"));

        Assert.Equal("attestation.pdf", result.ProposedFileName);
        Assert.DoesNotContain("2024", result.ProposedFileName);
        Assert.DoesNotContain("Contrat", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Montreuil", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Courriel", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ARESIA", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Meaningful_human_name_is_preserved_even_when_ocr_detects_noise()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "certificat travail stefy Gamby.pdf",
                DocumentType.Contract,
                new DateTime(1945, 5, 8),
                1945,
                5,
                "BRUNOY Nous contacter Courriel",
                "ARESIA-Valenton",
                null,
                null));

        Assert.Equal("certificat travail stefy Gamby.pdf", result.ProposedFileName);
        Assert.DoesNotContain("1945", result.ProposedFileName);
        Assert.DoesNotContain("BRUNOY", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ARESIA", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_meaningful_name_with_date_is_preserved_as_written()
    {
        var result = Service().Suggest(
            new FileRenameContext(
                "2021-04-09 - AXA - contrat habitation.pdf",
                DocumentType.Contract,
                new DateTime(2021, 4, 9),
                2021,
                4,
                "COURBEVOIEType de bien",
                "FRANCK CHELLI144 A",
                null,
                null));

        Assert.Equal(
            "2021-04-09 - AXA - contrat habitation.pdf",
            result.ProposedFileName);
        Assert.DoesNotContain("COURBEVOIE", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FRANCK CHELLI", result.ProposedFileName, StringComparison.OrdinalIgnoreCase);
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

        Assert.Equal("mon fichier.txt", result.ProposedFileName);
    }

    [Fact]
    public void Proposed_name_is_limited_in_length_for_generic_source()
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
            Path.GetFileNameWithoutExtension(result.ProposedFileName).Length <= 150);
    }
}
