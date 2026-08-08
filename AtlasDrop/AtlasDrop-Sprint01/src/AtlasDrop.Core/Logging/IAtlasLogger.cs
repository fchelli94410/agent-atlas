namespace AtlasDrop.Core.Logging;

public interface IAtlasLogger
{
    void Information(string eventName, string message);

    void Warning(string eventName, string message);

    void Error(string eventName, string message, Exception? exception = null);
}
