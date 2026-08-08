using Xunit;
using AtlasDrop.Core.Performance;

namespace AtlasDrop.Tests.Performance;

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task Transient_failure_is_retried()
    {
        var attempts = 0;

        var policy = new RetryPolicy(
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(1));

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;

                if (attempts < 3)
                    throw new IOException("transient");

                return Task.FromResult("ok");
            },
            ex => ex is IOException);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Non_retryable_failure_is_not_retried()
    {
        var attempts = 0;

        var policy = new RetryPolicy(
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => policy.ExecuteAsync<string>(
                _ =>
                {
                    attempts++;
                    throw new InvalidOperationException("fatal");
                },
                ex => ex is IOException));

        Assert.Equal(1, attempts);
    }
}
