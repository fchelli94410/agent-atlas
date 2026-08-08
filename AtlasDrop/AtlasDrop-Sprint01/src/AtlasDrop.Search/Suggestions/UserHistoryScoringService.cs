using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Search.Suggestions;

public sealed class UserHistoryScoringService : IUserHistoryScoringService
{
    public double GetAdjustment(
        string folderId,
        IEnumerable<UserHistorySignal> signals,
        string? contextKey,
        out IReadOnlyList<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            throw new ArgumentException("FolderId obligatoire.", nameof(folderId));

        ArgumentNullException.ThrowIfNull(signals);

        var relevant = signals
            .Where(x => string.Equals(
                x.FolderId,
                folderId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (relevant.Length == 0)
        {
            reasons = Array.Empty<string>();
            return 0d;
        }

        var now = DateTime.UtcNow;
        var adjustment = 0d;
        var details = new List<string>();

        foreach (var signal in relevant)
        {
            var age = now - signal.TimestampUtc;
            var recencyFactor = age <= TimeSpan.FromDays(30)
                ? 1.0
                : age <= TimeSpan.FromDays(180)
                    ? 0.65
                    : 0.35;

            var contextFactor = 1.0;

            if (!string.IsNullOrWhiteSpace(contextKey) &&
                !string.IsNullOrWhiteSpace(signal.ContextKey))
            {
                contextFactor = string.Equals(
                    contextKey,
                    signal.ContextKey,
                    StringComparison.OrdinalIgnoreCase)
                    ? 1.25
                    : 0.75;
            }

            var delta = signal.Type switch
            {
                UserHistorySignalType.Chosen => 0.055,
                UserHistorySignalType.CreatedFolder => 0.040,
                UserHistorySignalType.RenameCorrected => 0.020,
                UserHistorySignalType.Rejected => -0.060,
                UserHistorySignalType.Undo => -0.090,
                _ => 0d
            };

            adjustment += delta * recencyFactor * contextFactor;
        }

        var chosen = relevant.Count(x => x.Type == UserHistorySignalType.Chosen);
        var rejected = relevant.Count(x => x.Type == UserHistorySignalType.Rejected);
        var undone = relevant.Count(x => x.Type == UserHistorySignalType.Undo);

        if (chosen > 0)
            details.Add($"{chosen} choix utilisateur");

        if (rejected > 0)
            details.Add($"{rejected} refus utilisateur");

        if (undone > 0)
            details.Add($"{undone} annulation(s)");

        if (relevant.Any(x => x.Type == UserHistorySignalType.RenameCorrected))
            details.Add("correction de renommage observée");

        if (relevant.Any(x => x.Type == UserHistorySignalType.CreatedFolder))
            details.Add("dossier créé par l'utilisateur");

        reasons = details.ToArray();

        return Math.Clamp(adjustment, -0.30, 0.30);
    }
}
