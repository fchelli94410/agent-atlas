namespace AtlasDrop.Core.Integration;

public interface IFileActivationChannel : IDisposable
{
    bool IsPrimaryInstance { get; }

    event EventHandler<string>? FileReceived;

    Task SendToPrimaryAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task StartListeningAsync(
        CancellationToken cancellationToken = default);
}
