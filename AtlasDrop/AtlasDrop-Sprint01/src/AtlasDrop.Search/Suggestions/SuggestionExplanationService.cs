using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Search.Suggestions;

public sealed class SuggestionExplanationService
    : ISuggestionExplanationService
{
    private static readonly string[] NegativeMarkers =
    {
        "faible",
        "générique",
        "generique",
        "profondeur",
        "exclu",
        "défavorable",
        "defavorable"
    };

    public SuggestionExplanation Explain(FolderScoreResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var positives = new List<string>();
        var negatives = new List<string>();

        foreach (var reason in result.Reasons)
        {
            if (string.IsNullOrWhiteSpace(reason))
                continue;

            var clean = reason.Trim();

            if (IsNegative(clean))
                negatives.Add(ToReadable(clean));
            else
                positives.Add(ToReadable(clean));
        }

        positives = positives
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        negatives = negatives
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var confidence = result.Confidence?.Level
            ?? SuggestionConfidenceLevel.Low;

        var summary = confidence switch
        {
            SuggestionConfidenceLevel.High =>
                BuildSummary("Très bonne correspondance", positives, negatives),

            SuggestionConfidenceLevel.Medium =>
                BuildSummary("Correspondance plausible", positives, negatives),

            _ =>
                BuildSummary("Correspondance faible", positives, negatives)
        };

        return new SuggestionExplanation(
            summary,
            positives,
            negatives);
    }

    private static bool IsNegative(string reason)
    {
        return NegativeMarkers.Any(x =>
            reason.Contains(
                x,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string ToReadable(string reason)
    {
        if (reason.Equals(
            "fichiers similaires déjà présents",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Des fichiers similaires sont déjà classés ici.";
        }

        if (reason.Contains(
            "entreprise(s) commune(s)",
            StringComparison.OrdinalIgnoreCase))
        {
            return "L'entreprise détectée correspond à ce dossier.";
        }

        if (reason.Contains(
            "lieu(x) commun(s)",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le lieu détecté correspond à ce dossier.";
        }

        if (reason.Contains(
            "même type",
            StringComparison.OrdinalIgnoreCase) ||
            reason.Contains(
                "fichier(s) du même type",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Ce dossier contient déjà ce type de document.";
        }

        if (reason.Contains(
            "année correspondante",
            StringComparison.OrdinalIgnoreCase))
        {
            return "L'année détectée correspond au contenu du dossier.";
        }

        if (reason.Contains(
            "choix précédent",
            StringComparison.OrdinalIgnoreCase) ||
            reason.Contains(
                "historique utilisateur favorable",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Vos choix précédents favorisent ce dossier.";
        }

        if (reason.Contains(
            "historique utilisateur défavorable",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Vos choix précédents défavorisent ce dossier.";
        }

        if (reason.Contains(
            "nom du fichier proche",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le nom du fichier correspond au nom du dossier.";
        }

        if (reason.Contains(
            "contenu proche",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le contenu du document correspond au nom du dossier.";
        }

        if (reason.Contains(
            "bonne qualité",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le dossier est bien structuré et fiable.";
        }

        if (reason.Contains(
            "nom de dossier générique",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le nom du dossier est trop générique.";
        }

        if (reason.Contains(
            "qualité de dossier faible",
            StringComparison.OrdinalIgnoreCase))
        {
            return "La qualité du dossier est faible.";
        }

        if (reason.Contains(
            "profondeur maximale",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Le dossier est déjà au niveau maximal autorisé pour les suggestions.";
        }

        if (reason.Contains(
            "profondeur",
            StringComparison.OrdinalIgnoreCase) &&
            reason.Contains(
                "supérieure",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Ce dossier est trop profond pour être proposé automatiquement.";
        }

        if (reason.Contains(
            "dossier exclu",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Ce dossier est exclu des suggestions automatiques.";
        }

        return EnsureSentence(reason);
    }

    private static string BuildSummary(
        string prefix,
        IReadOnlyList<string> positives,
        IReadOnlyList<string> negatives)
    {
        if (positives.Count > 0)
        {
            var core = positives[0].TrimEnd('.');
            return $"{prefix} : {core.ToLowerInvariant()}.";
        }

        if (negatives.Count > 0)
        {
            var core = negatives[0].TrimEnd('.');
            return $"{prefix} : {core.ToLowerInvariant()}.";
        }

        return $"{prefix}.";
    }

    private static string EnsureSentence(string value)
    {
        var text = value.Trim();

        if (text.Length == 0)
            return text;

        text = char.ToUpperInvariant(text[0]) + text[1..];

        return text.EndsWith(".", StringComparison.Ordinal)
            ? text
            : text + ".";
    }
}
