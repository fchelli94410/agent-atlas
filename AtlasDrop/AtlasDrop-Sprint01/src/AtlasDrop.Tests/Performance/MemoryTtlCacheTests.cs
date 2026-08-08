using Xunit;
using AtlasDrop.Infrastructure.Performance;

namespace AtlasDrop.Tests.Performance;

public sealed class MemoryTtlCacheTests
{
    [Fact]
    public void Value_can_be_read_before_expiration()
    {
        var cache = new MemoryTtlCache<string, string>();

        cache.Set(
            "k",
            "v",
            TimeSpan.FromMinutes(1));

        Assert.True(cache.TryGet("k", out var value));
        Assert.Equal("v", value);
    }

    [Fact]
    public async Task Expired_value_is_not_returned()
    {
        var cache = new MemoryTtlCache<string, string>();

        cache.Set(
            "k",
            "v",
            TimeSpan.FromMilliseconds(30));

        await Task.Delay(80);

        Assert.False(cache.TryGet("k", out _));
    }

    [Fact]
    public void Remove_and_clear_work()
    {
        var cache = new MemoryTtlCache<string, string>();

        cache.Set("a", "1", TimeSpan.FromMinutes(1));
        cache.Set("b", "2", TimeSpan.FromMinutes(1));

        cache.Remove("a");

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));

        cache.Clear();

        Assert.False(cache.TryGet("b", out _));
    }
}
