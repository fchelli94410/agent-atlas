using Xunit;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class UserHistoryScoringServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Previous_choice_gives_positive_adjustment()
    {
        var service = new UserHistoryScoringService();

        var value = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Chosen,
                    Now.AddDays(-2),
                    "invoice-edf")
            },
            "invoice-edf",
            out var reasons);

        Assert.True(value > 0);
        Assert.Contains(reasons, x => x.Contains("choix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejection_gives_negative_adjustment()
    {
        var service = new UserHistoryScoringService();

        var value = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Rejected,
                    Now.AddDays(-1))
            },
            null,
            out _);

        Assert.True(value < 0);
    }

    [Fact]
    public void Undo_is_stronger_negative_signal_than_rejection()
    {
        var service = new UserHistoryScoringService();

        var rejected = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Rejected,
                    Now.AddDays(-1))
            },
            null,
            out _);

        var undone = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Undo,
                    Now.AddDays(-1))
            },
            null,
            out _);

        Assert.True(undone < rejected);
    }

    [Fact]
    public void Matching_context_strengthens_signal()
    {
        var service = new UserHistoryScoringService();

        var signal = new UserHistorySignal(
            "A",
            UserHistorySignalType.Chosen,
            Now.AddDays(-2),
            "invoice-edf");

        var matching = service.GetAdjustment(
            "A",
            new[] { signal },
            "invoice-edf",
            out _);

        var other = service.GetAdjustment(
            "A",
            new[] { signal },
            "contract-hr",
            out _);

        Assert.True(matching > other);
    }

    [Fact]
    public void Old_history_is_weaker_than_recent_history()
    {
        var service = new UserHistoryScoringService();

        var recent = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Chosen,
                    Now.AddDays(-5))
            },
            null,
            out _);

        var old = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "A",
                    UserHistorySignalType.Chosen,
                    Now.AddYears(-2))
            },
            null,
            out _);

        Assert.True(recent > old);
    }

    [Fact]
    public void Other_folder_history_is_ignored()
    {
        var service = new UserHistoryScoringService();

        var value = service.GetAdjustment(
            "A",
            new[]
            {
                new UserHistorySignal(
                    "B",
                    UserHistorySignalType.Chosen,
                    Now)
            },
            null,
            out var reasons);

        Assert.Equal(0d, value);
        Assert.Empty(reasons);
    }

    [Fact]
    public void Adjustment_is_clamped()
    {
        var service = new UserHistoryScoringService();

        var signals = Enumerable.Range(0, 100)
            .Select(i => new UserHistorySignal(
                "A",
                UserHistorySignalType.Chosen,
                Now.AddMinutes(-i)))
            .ToArray();

        var value = service.GetAdjustment(
            "A",
            signals,
            null,
            out _);

        Assert.Equal(0.30, value);
    }
}
