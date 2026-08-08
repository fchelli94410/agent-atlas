namespace AtlasDrop.Core.Indexing;

public interface IDirectoryChangeWatcher : IDisposable
{
    event EventHandler<DirectoryChange>? Changed;

    bool IsRunning { get; }

    void Start(string rootPath);

    void Stop();
}
