using Xunit;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class SuggestionExplanationServiceTests
{
    [Fact]
    public void High_confidence_has_clear_summary()
    {
        var result = Result(
            0.90,
            SuggestionConfidenceLevel.High,
            "1 entreprise(s) commune(s)",
            "année correspondante",
            "bonne qualité de dossier");

        var explanation = new SuggestionExplanationService()
            .Explain(result);

        Assert.StartsWith(
            "Très bonne correspondance",
            explanation.Summary);
        Assert.NotEmpty(explanation.PositiveReasons);
    }

    [Fact]
    public void Medium_confidence_mentions_plausible_match()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.60,
                SuggestionConfidenceLevel.Medium,
                "1 lieu(x) commun(s)"));

        Assert.StartsWith(
            "Correspondance plausible",
            explanation.Summary);
    }

    [Fact]
    public void Low_confidence_has_low_summary()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.20,
                SuggestionConfidenceLevel.Low,
                "nom de dossier générique"));

        Assert.StartsWith(
            "Correspondance faible",
            explanation.Summary);
        Assert.NotEmpty(explanation.NegativeReasons);
    }

    [Fact]
    public void Company_reason_is_human_readable()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.80,
                SuggestionConfidenceLevel.High,
                "1 entreprise(s) commune(s)"));

        Assert.Contains(
            "L'entreprise détectée correspond à ce dossier.",
            explanation.PositiveReasons);
    }

    [Fact]
    public void Similar_files_reason_is_human_readable()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.80,
                SuggestionConfidenceLevel.High,
                "fichiers similaires déjà présents"));

        Assert.Contains(
            "Des fichiers similaires sont déjà classés ici.",
            explanation.PositiveReasons);
    }

    [Fact]
    public void Negative_reasons_are_separated()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.30,
                SuggestionConfidenceLevel.Low,
                "bonne qualité de dossier",
                "nom de dossier générique",
                "qualité de dossier faible"));

        Assert.Single(explanation.PositiveReasons);
        Assert.Equal(2, explanation.NegativeReasons.Count);
    }

    [Fact]
    public void Excluded_folder_explanation_is_explicit()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.0,
                SuggestionConfidenceLevel.Low,
                "dossier exclu"));

        Assert.Contains(
            "Ce dossier est exclu des suggestions automatiques.",
            explanation.NegativeReasons);
    }

    [Fact]
    public void Deep_folder_explanation_is_explicit()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.0,
                SuggestionConfidenceLevel.Low,
                "profondeur 5 supérieure à 4"));

        Assert.Contains(
            "Ce dossier est trop profond pour être proposé automatiquement.",
            explanation.NegativeReasons);
    }

    [Fact]
    public void Explanation_limits_number_of_reasons()
    {
        var explanation = new SuggestionExplanationService()
            .Explain(Result(
                0.90,
                SuggestionConfidenceLevel.High,
                "raison positive a",
                "raison positive b",
                "raison positive c",
                "raison positive d",
                "raison positive e",
                "qualité de dossier faible",
                "nom de dossier générique",
                "historique utilisateur défavorable",
                "profondeur maximale autorisée"));

        Assert.True(explanation.PositiveReasons.Count <= 4);
        Assert.True(explanation.NegativeReasons.Count <= 3);
    }

    private static FolderScoreResult Result(
        double score,
        SuggestionConfidenceLevel confidence,
        params string[] reasons)
    {
        var folder = new FolderCandidate(
            "1",
            "EDF",
            @"C:\OneDrive\EDF",
            2,
            false,
            0.5,
            null,
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            new Dictionary<AtlasDrop.Core.Analysis.DocumentType, int>());

        return new FolderScoreResult(
            folder,
            score,
            reasons,
            new SuggestionConfidence(
                confidence,
                score,
                confidence == SuggestionConfidenceLevel.High,
                "test"));
    }
}
