using AtlasDrop.Core.Indexing;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class DirectoryChangeWatcher : IDirectoryChangeWatcher
{
    private FileSystemWatcher? _watcher;
    private readonly object _gate = new();

    public event EventHandler<DirectoryChange>? Changed;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _watcher?.EnableRaisingEvents == true;
        }
    }

    public void Start(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("La racine de surveillance est obligatoire.", nameof(rootPath));

        var fullRoot = Path.GetFullPath(rootPath);

        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Racine introuvable : {fullRoot}");

        lock (_gate)
        {
            StopInternal();

            var watcher = new FileSystemWatcher(fullRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.DirectoryName |
                    NotifyFilters.FileName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.CreationTime,
                InternalBufferSize = 32 * 1024,
                EnableRaisingEvents = false
            };

            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Changed += OnChanged;
            watcher.Error += OnError;

            _watcher = watcher;
            watcher.EnableRaisingEvents = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
            StopInternal();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void StopInternal()
    {
        var watcher = _watcher;
        _watcher = null;

        if (watcher is null)
            return;

        watcher.EnableRaisingEvents = false;
        watcher.Created -= OnCreated;
        watcher.Deleted -= OnDeleted;
        watcher.Renamed -= OnRenamed;
        watcher.Changed -= OnChanged;
        watcher.Error -= OnError;
        watcher.Dispose();
    }

    private void OnCreated(object sender, FileSystemEventArgs e) =>
        Raise(DirectoryChangeKind.Created, e.FullPath, null);

    private void OnDeleted(object sender, FileSystemEventArgs e) =>
        Raise(DirectoryChangeKind.Deleted, e.FullPath, null);

    private void OnChanged(object sender, FileSystemEventArgs e) =>
        Raise(DirectoryChangeKind.Changed, e.FullPath, null);

    private void OnRenamed(object sender, RenamedEventArgs e) =>
        Raise(DirectoryChangeKind.Renamed, e.FullPath, e.OldFullPath);

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Un overflow FileSystemWatcher ne doit jamais faire planter l'application.
        // Le Sprint 9 déclenchera une réconciliation de sécurité complète.
    }

    private void Raise(DirectoryChangeKind kind, string fullPath, string? oldFullPath)
    {
        var handler = Changed;
        if (handler is null)
            return;

        handler(
            this,
            new DirectoryChange(
                kind,
                Path.GetFullPath(fullPath),
                oldFullPath is null ? null : Path.GetFullPath(oldFullPath),
                DateTime.UtcNow));
    }
}
