using Xunit;
using AtlasDrop.Core.Performance;

namespace AtlasDrop.Tests.Performance;

public sealed class AsyncGateTests
{
    [Fact]
    public async Task Gate_limits_concurrency()
    {
        var gate = new AsyncGate(1);

        using var first = await gate.EnterAsync();

        var entered = false;

        var secondTask = Task.Run(async () =>
        {
            using var second = await gate.EnterAsync();
            entered = true;
        });

        await Task.Delay(50);

        Assert.False(entered);

        first.Dispose();

        await secondTask;

        Assert.True(entered);
    }
}
