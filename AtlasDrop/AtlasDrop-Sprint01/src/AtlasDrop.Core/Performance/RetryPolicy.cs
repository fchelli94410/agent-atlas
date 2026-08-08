namespace AtlasDrop.Core.Performance;

public sealed class RetryPolicy
{
    public int MaxAttempts { get; }
    public TimeSpan InitialDelay { get; }

    public RetryPolicy(
        int maxAttempts = 3,
        TimeSpan? initialDelay = null)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        Exception? last = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (
                attempt < MaxAttempts &&
                shouldRetry(ex))
            {
                last = ex;

                var delay = TimeSpan.FromMilliseconds(
                    InitialDelay.TotalMilliseconds *
                    Math.Pow(2, attempt - 1));

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException(
            "La politique de retry s'est terminée sans résultat.");
    }
}
