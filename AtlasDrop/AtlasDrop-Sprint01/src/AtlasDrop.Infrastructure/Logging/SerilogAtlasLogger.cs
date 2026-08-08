using AtlasDrop.Core.Logging;
using Serilog;

namespace AtlasDrop.Infrastructure.Logging;

public sealed class SerilogAtlasLogger : IAtlasLogger, IDisposable
{
    private readonly ILogger _logger;
    private readonly bool _ownsLogger;

    public SerilogAtlasLogger(
        string logDirectory)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            throw new ArgumentException(
                "Dossier de logs obligatoire.",
                nameof(logDirectory));

        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(
            logDirectory,
            "atlasdrop-.log");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Application", "AtlasDrop")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();

        _ownsLogger = true;
    }

    public SerilogAtlasLogger(ILogger logger)
    {
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        _ownsLogger = false;
    }

    public void Information(
        string eventName,
        string message)
    {
        _logger.Information(
            "{EventName} {Message}",
            Safe(eventName),
            Safe(message));
    }

    public void Warning(
        string eventName,
        string message)
    {
        _logger.Warning(
            "{EventName} {Message}",
            Safe(eventName),
            Safe(message));
    }

    public void Error(
        string eventName,
        string message,
        Exception? exception = null)
    {
        if (exception is null)
        {
            _logger.Error(
                "{EventName} {Message}",
                Safe(eventName),
                Safe(message));

            return;
        }

        _logger.Error(
            exception,
            "{EventName} {Message}",
            Safe(eventName),
            Safe(message));
    }

    private static string Safe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (sanitized.Length > 500)
            sanitized = sanitized[..500];

        return sanitized;
    }

    public void Dispose()
    {
        if (_ownsLogger &&
            _logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
