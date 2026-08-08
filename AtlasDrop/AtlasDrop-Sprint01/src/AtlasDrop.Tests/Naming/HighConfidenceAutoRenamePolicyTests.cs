using Xunit;
using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Naming;
using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Tests.Naming;

public sealed class HighConfidenceAutoRenamePolicyTests
{
    private static HighConfidenceAutoRenamePolicy Policy() => new();

    private static FileRenameSuggestion Suggestion(bool changed = true) =>
        new(
            "2026 - Facture - EDF.pdf",
            changed,
            Array.Empty<string>());

    private static SuggestionConfidence Confidence(
        SuggestionConfidenceLevel level,
        bool canAutoClassify) =>
        new(
            level,
            level == SuggestionConfidenceLevel.High ? 0.90 : 0.50,
            canAutoClassify,
            "test");

    [Fact]
    public void High_confidence_allows_auto_rename_when_enabled()
    {
        var result = Policy().Decide(
            Suggestion(),
            Confidence(
                SuggestionConfidenceLevel.High,
                true),
            userEnabledAutoRename: true);

        Assert.True(result.ShouldApply);
    }

    [Fact]
    public void Medium_confidence_never_allows_auto_rename()
    {
        var result = Policy().Decide(
            Suggestion(),
            Confidence(
                SuggestionConfidenceLevel.Medium,
                false),
            userEnabledAutoRename: true);

        Assert.False(result.ShouldApply);
    }

    [Fact]
    public void Low_confidence_never_allows_auto_rename()
    {
        var result = Policy().Decide(
            Suggestion(),
            Confidence(
                SuggestionConfidenceLevel.Low,
                false),
            userEnabledAutoRename: true);

        Assert.False(result.ShouldApply);
    }

    [Fact]
    public void Disabled_option_never_allows_auto_rename()
    {
        var result = Policy().Decide(
            Suggestion(),
            Confidence(
                SuggestionConfidenceLevel.High,
                true),
            userEnabledAutoRename: false);

        Assert.False(result.ShouldApply);
    }

    [Fact]
    public void Unchanged_name_is_not_auto_applied()
    {
        var result = Policy().Decide(
            Suggestion(changed: false),
            Confidence(
                SuggestionConfidenceLevel.High,
                true),
            userEnabledAutoRename: true);

        Assert.False(result.ShouldApply);
    }

    [Fact]
    public void High_level_without_auto_classify_permission_is_rejected()
    {
        var result = Policy().Decide(
            Suggestion(),
            Confidence(
                SuggestionConfidenceLevel.High,
                false),
            userEnabledAutoRename: true);

        Assert.False(result.ShouldApply);
    }
}
