using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class FileSimilarityServiceTests
{
    private static FileSimilarityService CreateService() =>
        new(new TextNormalizer());

    [Fact]
    public void Same_document_type_raises_similarity()
    {
        var context = Context(type: DocumentType.Invoice);
        var candidate = Candidate(type: DocumentType.Invoice);

        var score = CreateService().Score(
            context,
            candidate,
            out var reasons);

        Assert.True(score >= 0.30);
        Assert.Contains("même type documentaire", reasons);
    }

    [Fact]
    public void Keywords_places_companies_and_year_raise_similarity()
    {
        var context = Context(
            keywords: new[] { "facture", "edf" },
            places: new[] { "Courbevoie" },
            companies: new[] { "EDF" },
            years: new[] { 2026 });

        var candidate = Candidate(
            keywords: new[] { "EDF", "facture" },
            places: new[] { "courbevoie" },
            companies: new[] { "edf" },
            years: new[] { 2026 });

        var service = CreateService();

        var score = service.Score(
            context,
            candidate,
            out _);

        var unrelated = service.Score(
            context,
            Candidate(
                keywords: new[] { "rh" },
                places: new[] { "Paris" },
                companies: new[] { "Contoso" },
                years: new[] { 2020 }),
            out _);

        Assert.True(score > unrelated);
        Assert.True(score >= 0.40);
    }

    [Fact]
    public void Accent_and_case_normalization_are_used()
    {
        var context = Context(
            places: new[] { "Montévrain" });

        var candidate = Candidate(
            places: new[] { "MONTEVRAIN" });

        var score = CreateService().Score(
            context,
            candidate,
            out _);

        Assert.True(score > 0);
    }

    [Fact]
    public void Unrelated_files_have_zero_similarity()
    {
        var context = Context(
            fileName: "Facture EDF.pdf",
            type: DocumentType.Invoice,
            companies: new[] { "EDF" });

        var candidate = Candidate(
            fileName: "Rapport RH.docx",
            type: DocumentType.Report,
            companies: new[] { "Contoso" });

        var score = CreateService().Score(
            context,
            candidate,
            out var reasons);

        Assert.Equal(0d, score);
        Assert.Empty(reasons);
    }

    [Fact]
    public void Similar_file_name_raises_similarity()
    {
        var context = Context(
            fileName: "Contrat EDF 2026.pdf");

        var candidate = Candidate(
            fileName: "Contrat EDF 2026 signé.pdf");

        var score = CreateService().Score(
            context,
            candidate,
            out var reasons);

        Assert.True(score > 0);
        Assert.Contains("nom de fichier proche", reasons);
    }

    private static FileSuggestionContext Context(
        string fileName = "document.pdf",
        DocumentType type = DocumentType.Unknown,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? places = null,
        IReadOnlyList<string>? companies = null,
        IReadOnlyList<int>? years = null)
    {
        return new FileSuggestionContext(
            fileName,
            "",
            type,
            keywords ?? Array.Empty<string>(),
            places ?? Array.Empty<string>(),
            companies ?? Array.Empty<string>(),
            years ?? Array.Empty<int>());
    }

    private static SimilarFileProfile Candidate(
        string fileName = "ancien.pdf",
        DocumentType type = DocumentType.Unknown,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? places = null,
        IReadOnlyList<string>? companies = null,
        IReadOnlyList<int>? years = null)
    {
        return new SimilarFileProfile(
            fileName,
            type,
            keywords ?? Array.Empty<string>(),
            places ?? Array.Empty<string>(),
            companies ?? Array.Empty<string>(),
            years ?? Array.Empty<int>());
    }
}
