namespace AtlasDrop.Core.Performance;

public sealed class AsyncGate
{
    private readonly SemaphoreSlim _semaphore;

    public AsyncGate(int maxConcurrency = 1)
    {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        _semaphore = new SemaphoreSlim(
            maxConcurrency,
            maxConcurrency);
    }

    public async Task<IDisposable> EnterAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            Interlocked.Exchange(
                ref _semaphore,
                null)?.Release();
        }
    }
}
