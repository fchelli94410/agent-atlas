using Xunit;
using AtlasDrop.Infrastructure.Integration;

namespace AtlasDrop.Tests.Integration;

public sealed class NamedPipeFileActivationChannelTests
{
    [Fact]
    public void First_instance_is_primary()
    {
        using var first = new NamedPipeFileActivationChannel();

        Assert.True(first.IsPrimaryInstance);
    }

    [Fact]
    public void Second_instance_is_not_primary_while_first_is_alive()
    {
        using var first = new NamedPipeFileActivationChannel();
        using var second = new NamedPipeFileActivationChannel();

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }
}
