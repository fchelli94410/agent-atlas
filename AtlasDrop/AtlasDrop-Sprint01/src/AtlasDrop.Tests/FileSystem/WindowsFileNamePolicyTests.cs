using Xunit;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class WindowsFileNamePolicyTests
{
    private static WindowsFileNamePolicy Policy() => new();

    [Theory]
    [InlineData("facture:edf.pdf")]
    [InlineData("facture?edf.pdf")]
    [InlineData("facture|edf.pdf")]
    [InlineData("facture<edf>.pdf")]
    public void Invalid_characters_are_removed(string input)
    {
        var result = Policy().Sanitize(input);

        Assert.True(result.IsValid);
        Assert.True(result.Changed);
        Assert.DoesNotContain("?", result.SafeFileName);
        Assert.DoesNotContain("|", result.SafeFileName);
        Assert.DoesNotContain("<", result.SafeFileName);
        Assert.DoesNotContain(">", result.SafeFileName);
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("PRN.pdf")]
    [InlineData("AUX.docx")]
    [InlineData("NUL.csv")]
    [InlineData("COM1.txt")]
    [InlineData("LPT9.txt")]
    public void Reserved_names_are_neutralized(string input)
    {
        var result = Policy().Sanitize(input);

        Assert.True(result.IsValid);
        Assert.StartsWith("_", result.SafeFileName);
    }

    [Fact]
    public void Trailing_dot_or_space_is_removed()
    {
        var result = Policy().Sanitize("rapport. ");

        Assert.True(result.IsValid);
        Assert.False(result.SafeFileName.EndsWith(".", StringComparison.Ordinal));
        Assert.False(result.SafeFileName.EndsWith(" ", StringComparison.Ordinal));
    }

    [Fact]
    public void Extension_is_preserved()
    {
        var result = Policy().Sanitize("rapport final.PDF");

        Assert.EndsWith(".PDF", result.SafeFileName);
    }

    [Fact]
    public void Long_name_is_shortened()
    {
        var input = new string('A', 300) + ".pdf";

        var result = Policy().Sanitize(
            input,
            maxFileNameLength: 100);

        Assert.True(result.SafeFileName.Length <= 100);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Empty_name_is_invalid()
    {
        var result = Policy().Sanitize("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_name_is_unchanged()
    {
        var result = Policy().Sanitize(
            "2026 - Facture - EDF.pdf");

        Assert.True(result.IsValid);
        Assert.False(result.Changed);
        Assert.Equal(
            "2026 - Facture - EDF.pdf",
            result.SafeFileName);
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("A:B.pdf")]
    [InlineData("test?.pdf")]
    public void Is_valid_rejects_invalid_names(string input)
    {
        Assert.False(Policy().IsValid(input));
    }
}
