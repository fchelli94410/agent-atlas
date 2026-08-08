using Xunit;
using AtlasDrop.Analysis.Companies;

namespace AtlasDrop.Tests.Analysis;

public sealed class CompanyDetectionServiceTests
{
    [Theory]
    [InlineData("ALTEDIS SAS")]
    [InlineData("Dupont Conseil SARL")]
    [InlineData("Immo Patrimoine SCI")]
    public void Detects_company_with_legal_form(string text)
    {
        var result = new CompanyDetectionService().Detect(text);

        var company = Assert.Single(result);
        Assert.Contains(
            text.Split(' ')[0],
            company.Name,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("legal-form", company.Source);
        Assert.True(company.Confidence >= 0.95);
    }

    [Fact]
    public void Detects_explicit_company_label()
    {
        var result = new CompanyDetectionService().Detect(
            "Fournisseur : Electricité de France");

        var company = Assert.Single(result);

        Assert.Contains(
            "Electricité de France",
            company.Name,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("explicit-label", company.Source);
    }

    [Fact]
    public void Detects_company_from_professional_email_domain()
    {
        var result = new CompanyDetectionService().Detect(
            "Contact : facturation@alted-is.fr");

        var company = Assert.Single(result);

        Assert.Equal("email-domain", company.Source);
        Assert.Equal("alted is", company.NormalizedName);
    }

    [Theory]
    [InlineData("contact@gmail.com")]
    [InlineData("test@outlook.com")]
    [InlineData("perso@orange.fr")]
    public void Ignores_generic_email_domains(string email)
    {
        var result = new CompanyDetectionService().Detect(email);

        Assert.Empty(result);
    }

    [Fact]
    public void Deduplicates_company_variants()
    {
        var result = new CompanyDetectionService().Detect(
            "ALTÉDIS SAS - contact@altedis.fr");

        Assert.Single(result);
        Assert.Equal("altedis", result[0].NormalizedName);
        Assert.Equal("legal-form", result[0].Source);
    }

    [Fact]
    public void Legal_form_has_priority_over_email_domain()
    {
        var result = new CompanyDetectionService().Detect(
            "ALTEDIS SAS contact@altedis.fr");

        var company = Assert.Single(result);

        Assert.Equal("legal-form", company.Source);
        Assert.True(company.Confidence > 0.9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aucune société identifiable")]
    public void No_company_returns_empty(string? text)
    {
        var result = new CompanyDetectionService().Detect(text);

        Assert.Empty(result);
    }

    [Fact]
    public void Normalization_removes_accents_and_legal_form()
    {
        var result = new CompanyDetectionService().Detect(
            "ÉNERGIE CONSEIL SARL");

        var company = Assert.Single(result);

        Assert.Equal("energie conseil", company.NormalizedName);
    }

    [Fact]
    public void Does_not_build_company_across_file_name_and_next_line()
    {
        var result = new CompanyDetectionService().Detect(
            "AVIS DECHEANCE 58_3M_ETH.docx\r\nSCI FREERIDERS");

        Assert.DoesNotContain(result, x =>
            x.Name.Contains(".docx", StringComparison.OrdinalIgnoreCase));
    }
}
