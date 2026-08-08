using Xunit;
using AtlasDrop.Core.Configuration;
using AtlasDrop.Core.Security;

namespace AtlasDrop.Tests.Security;

public sealed class PathPolicyTests
{
    private static PathPolicy Policy() => new(new AtlasDropOptions());

    [Fact]
    public void Outside_root_is_rejected() =>
        Assert.False(Policy().IsDestinationAllowed(@"C:\Users\fchelli\Downloads"));

    [Theory]
    [InlineData(@"C:\Users\fchelli\OneDrive - ALTEDIS\Bureau")]
    [InlineData(@"C:\Users\fchelli\OneDrive - ALTEDIS\99 - Archives")]
    [InlineData(@"C:\Users\fchelli\OneDrive - ALTEDIS\01 - Immobilier\99 - Archives")]
    public void Exclusions_are_rejected(string path) =>
        Assert.False(Policy().IsDestinationAllowed(path));

    [Fact]
    public void Depth_four_is_allowed() =>
        Assert.True(Policy().IsDestinationAllowed(
            @"C:\Users\fchelli\OneDrive - ALTEDIS\01\02\03\04"));

    [Fact]
    public void Depth_five_is_allowed() =>
        Assert.True(Policy().IsDestinationAllowed(
            @"C:\Users\fchelli\OneDrive - ALTEDIS\01\02\03\04\05"));

    [Fact]
    public void Depth_six_is_rejected() =>
        Assert.False(Policy().IsDestinationAllowed(
            @"C:\Users\fchelli\OneDrive - ALTEDIS\01\02\03\04\05\06"));
}
