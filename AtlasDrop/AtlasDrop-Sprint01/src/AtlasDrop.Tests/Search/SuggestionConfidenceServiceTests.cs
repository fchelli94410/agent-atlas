using Xunit;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.Tests.Search;

public sealed class SuggestionConfidenceServiceTests
{
    [Theory]
    [InlineData(0.00)]
    [InlineData(0.20)]
    [InlineData(0.44)]
    public void Low_confidence_never_allows_auto_classification(double score)
    {
        var result = new SuggestionConfidenceService().Evaluate(score);

        Assert.Equal(SuggestionConfidenceLevel.Low, result.Level);
        Assert.False(result.CanAutoClassify);
    }

    [Theory]
    [InlineData(0.45)]
    [InlineData(0.60)]
    [InlineData(0.74)]
    public void Medium_confidence_requires_user_validation(double score)
    {
        var result = new SuggestionConfidenceService().Evaluate(score);

        Assert.Equal(SuggestionConfidenceLevel.Medium, result.Level);
        Assert.False(result.CanAutoClassify);
    }

    [Theory]
    [InlineData(0.75)]
    [InlineData(0.90)]
    [InlineData(1.00)]
    public void High_confidence_can_allow_auto_classification(double score)
    {
        var result = new SuggestionConfidenceService().Evaluate(score);

        Assert.Equal(SuggestionConfidenceLevel.High, result.Level);
        Assert.True(result.CanAutoClassify);
    }

    [Fact]
    public void Score_is_clamped_to_zero_and_one()
    {
        var service = new SuggestionConfidenceService();

        Assert.Equal(0d, service.Evaluate(-5).Score);
        Assert.Equal(1d, service.Evaluate(5).Score);
    }

    [Fact]
    public void Invalid_threshold_order_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => new SuggestionConfidenceService(
                mediumThreshold: 0.8,
                highThreshold: 0.7));
    }

    [Fact]
    public void Custom_thresholds_are_supported()
    {
        var service = new SuggestionConfidenceService(
            mediumThreshold: 0.50,
            highThreshold: 0.90);

        Assert.Equal(
            SuggestionConfidenceLevel.Medium,
            service.Evaluate(0.80).Level);

        Assert.Equal(
            SuggestionConfidenceLevel.High,
            service.Evaluate(0.95).Level);
    }
}
