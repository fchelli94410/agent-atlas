using Xunit;
using AtlasDrop.Analysis.Ocr;

namespace AtlasDrop.Tests.Analysis;

public sealed class PdfOcrDecisionServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_requires_ocr(string? text)
    {
        var service = new PdfOcrDecisionService();

        var decision = service.Decide(text);

        Assert.True(decision.ShouldUseOcr);
        Assert.Equal(0, decision.ExtractedCharacterCount);
    }

    [Fact]
    public void Short_text_requires_ocr()
    {
        var service = new PdfOcrDecisionService(minimumUsefulCharacters: 80);

        var decision = service.Decide("Facture EDF");

        Assert.True(decision.ShouldUseOcr);
        Assert.Contains("insuffisant", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Long_text_does_not_require_ocr()
    {
        var service = new PdfOcrDecisionService(minimumUsefulCharacters: 80);

        var decision = service.Decide(new string('A', 200));

        Assert.False(decision.ShouldUseOcr);
        Assert.Equal(200, decision.ExtractedCharacterCount);
    }
}
