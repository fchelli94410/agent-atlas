using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Search.Suggestions;

public sealed class SuggestionConfidenceService
    : ISuggestionConfidenceService
{
    private readonly double _mediumThreshold;
    private readonly double _highThreshold;

    public SuggestionConfidenceService(
        double mediumThreshold = 0.45,
        double highThreshold = 0.75)
    {
        if (mediumThreshold < 0 || mediumThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(mediumThreshold));

        if (highThreshold < 0 || highThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(highThreshold));

        if (highThreshold <= mediumThreshold)
            throw new ArgumentException(
                "Le seuil élevé doit être strictement supérieur au seuil moyen.");

        _mediumThreshold = mediumThreshold;
        _highThreshold = highThreshold;
    }

    public SuggestionConfidence Evaluate(double score)
    {
        var normalized = Math.Clamp(score, 0d, 1d);

        if (normalized >= _highThreshold)
        {
            return new SuggestionConfidence(
                SuggestionConfidenceLevel.High,
                normalized,
                true,
                "Confiance élevée : plusieurs signaux convergents.");
        }

        if (normalized >= _mediumThreshold)
        {
            return new SuggestionConfidence(
                SuggestionConfidenceLevel.Medium,
                normalized,
                false,
                "Confiance moyenne : validation utilisateur requise.");
        }

        return new SuggestionConfidence(
            SuggestionConfidenceLevel.Low,
            normalized,
            false,
            "Confiance faible : aucune classification automatique.");
    }
}
