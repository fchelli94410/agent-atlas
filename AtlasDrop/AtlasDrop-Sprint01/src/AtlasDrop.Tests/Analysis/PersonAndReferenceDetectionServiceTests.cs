using Xunit;
using AtlasDrop.Analysis.Entities;

namespace AtlasDrop.Tests.Analysis;

public sealed class PersonAndReferenceDetectionServiceTests
{
    [Theory]
    [InlineData("Contact : Jean Dupont", "jean dupont")]
    [InlineData("Monsieur Pierre Martin", "pierre martin")]
    [InlineData("Mme Élodie Durand", "elodie durand")]
    public void Detects_person_with_explicit_label(
        string text,
        string expectedNormalized)
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectPersons(text);

        var person = Assert.Single(result);

        Assert.Equal(expectedNormalized, person.NormalizedName);
        Assert.Equal("explicit-label", person.Source);
        Assert.True(person.Confidence > 0.9);
    }

    [Fact]
    public void Detects_uppercase_person_name_as_weaker_signal()
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectPersons("Signé par JEAN DUPONT");

        Assert.Contains(result, x =>
            x.NormalizedName == "jean dupont");
    }

    [Fact]
    public void Deduplicates_person_variants()
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectPersons("Contact : Jean Dupont. JEAN DUPONT");

        Assert.Single(result);
        Assert.Equal("explicit-label", result[0].Source);
    }

    [Theory]
    [InlineData("Contrat n° CT-2026-001", "contrat", "CT-2026-001")]
    [InlineData("Dossier : DOS/7788", "dossier", "DOS-7788")]
    [InlineData("Commande # CMD_4455", "commande", "CMD-4455")]
    [InlineData("Référence REF-2026-A12", "reference", "REF-2026-A12")]
    public void Detects_explicit_references(
        string text,
        string expectedKind,
        string expectedValue)
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectReferences(text);

        var reference = Assert.Single(result);

        Assert.Equal(expectedKind, reference.Kind);
        Assert.Equal(expectedValue, reference.NormalizedValue);
        Assert.True(reference.Confidence > 0.9);
    }

    [Fact]
    public void Detects_generic_structured_reference()
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectReferences("Document ABC-2026-7788");

        Assert.Contains(result, x =>
            x.NormalizedValue == "ABC-2026-7788");
    }

    [Fact]
    public void Explicit_reference_has_priority_over_generic_match()
    {
        var result = new PersonAndReferenceDetectionService()
            .DetectReferences("Contrat CT-2026-001");

        var contract = Assert.Single(
            result,
            x => x.NormalizedValue == "CT-2026-001");

        Assert.Equal("contrat", contract.Kind);
        Assert.True(contract.Confidence > 0.9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aucune personne ni référence")]
    public void Empty_or_unrelated_text_returns_nothing(string? text)
    {
        var service = new PersonAndReferenceDetectionService();

        Assert.Empty(service.DetectPersons(text));
        Assert.Empty(service.DetectReferences(text));
    }

    [Fact]
    public void Accents_do_not_change_person_identity()
    {
        var service = new PersonAndReferenceDetectionService();

        var a = service.DetectPersons("Contact : Élodie Durand");
        var b = service.DetectPersons("Contact : Elodie Durand");

        Assert.Equal(a[0].NormalizedName, b[0].NormalizedName);
    }
}
