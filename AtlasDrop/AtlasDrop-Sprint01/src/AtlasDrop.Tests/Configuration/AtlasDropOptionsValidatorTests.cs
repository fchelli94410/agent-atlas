using Xunit;
using AtlasDrop.Core.Configuration;

namespace AtlasDrop.Tests.Configuration;

public sealed class AtlasDropOptionsValidatorTests
{
    private readonly AtlasDropOptionsValidator _validator = new();

    [Fact]
    public void Default_configuration_is_valid()
    {
        var errors = _validator.Validate(new AtlasDropOptions());
        Assert.Empty(errors);
    }

    [Fact]
    public void Empty_root_is_rejected()
    {
        var errors = _validator.Validate(new AtlasDropOptions
        {
            OneDriveRoot = ""
        });

        Assert.Contains(errors, x => x.Contains("racine OneDrive", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Invalid_depth_is_rejected(int depth)
    {
        var errors = _validator.Validate(new AtlasDropOptions
        {
            MaxSuggestedDepth = depth
        });

        Assert.Contains(errors, x => x.Contains("MaxSuggestedDepth", StringComparison.Ordinal));
    }

    [Fact]
    public void Depth_four_is_valid()
    {
        var errors = _validator.Validate(new AtlasDropOptions
        {
            MaxSuggestedDepth = 4
        });

        Assert.Empty(errors);
    }
}
