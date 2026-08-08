using System.IO.Pipes;
using System.Text;
using AtlasDrop.Core.Integration;

namespace AtlasDrop.Infrastructure.Integration;

public sealed class NamedPipeFileActivationChannel
    : IFileActivationChannel
{
    private const string MutexName =
        @"Local\AtlasDrop.SingleInstance";

    private const string PipeName =
        "AtlasDrop.FileActivation";

    private readonly Mutex _mutex;
    private bool _disposed;

    public bool IsPrimaryInstance { get; }

    public event EventHandler<string>? FileReceived;

    public NamedPipeFileActivationChannel()
    {
        _mutex = new Mutex(
            initiallyOwned: true,
            MutexName,
            out var createdNew);

        IsPrimaryInstance = createdNew;
    }

    public async Task SendToPrimaryAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "Chemin fichier obligatoire.",
                nameof(filePath));

        using var client = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous);

        await client.ConnectAsync(
            timeout: 3000,
            cancellationToken);

        var payload = Encoding.UTF8.GetBytes(
            Path.GetFullPath(filePath));

        await client.WriteAsync(
            payload,
            cancellationToken);

        await client.FlushAsync(cancellationToken);
    }

    public async Task StartListeningAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsPrimaryInstance)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync(
                cancellationToken);

            using var memory = new MemoryStream();

            var buffer = new byte[4096];

            while (server.IsConnected)
            {
                var read = await server.ReadAsync(
                    buffer,
                    cancellationToken);

                if (read == 0)
                    break;

                await memory.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
            }

            var filePath = Encoding.UTF8.GetString(
                memory.ToArray()).Trim();

            if (!string.IsNullOrWhiteSpace(filePath))
                FileReceived?.Invoke(this, filePath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
        }

        _mutex.Dispose();
    }
}
