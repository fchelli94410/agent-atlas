using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Ocr;

public sealed class PdfOcrDecisionService : IPdfOcrDecisionService
{
    private readonly int _minimumUsefulCharacters;

    public PdfOcrDecisionService(int minimumUsefulCharacters = 80)
    {
        if (minimumUsefulCharacters < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumUsefulCharacters));

        _minimumUsefulCharacters = minimumUsefulCharacters;
    }

    public PdfOcrDecision Decide(string? extractedText)
    {
        var normalized = string.IsNullOrWhiteSpace(extractedText)
            ? string.Empty
            : extractedText.Trim();

        if (normalized.Length == 0)
        {
            return new PdfOcrDecision(
                true,
                "Aucun texte PDF exploitable.",
                0);
        }

        if (normalized.Length < _minimumUsefulCharacters)
        {
            return new PdfOcrDecision(
                true,
                "Texte PDF insuffisant pour une analyse fiable.",
                normalized.Length);
        }

        return new PdfOcrDecision(
            false,
            "Texte PDF natif suffisant.",
            normalized.Length);
    }
}
