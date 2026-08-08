using Xunit;
using AtlasDrop.Core.Indexing;
using AtlasDrop.Infrastructure.Indexing;

namespace AtlasDrop.Tests.Indexing;

public sealed class DirectoryChangeWatcherTests
{
    [Fact]
    public void Start_and_stop_update_running_state()
    {
        var root = CreateTempRoot();

        try
        {
            using var watcher = new DirectoryChangeWatcher();

            Assert.False(watcher.IsRunning);

            watcher.Start(root);
            Assert.True(watcher.IsRunning);

            watcher.Stop();
            Assert.False(watcher.IsRunning);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Created_directory_is_observed()
    {
        var root = CreateTempRoot();

        try
        {
            using var watcher = new DirectoryChangeWatcher();
            watcher.Start(root);

            var expected = Path.Combine(root, "Created");
            var observed = WaitForAsync(
                watcher,
                x => x.Kind == DirectoryChangeKind.Created &&
                     SamePath(x.FullPath, expected));

            Directory.CreateDirectory(expected);

            var change = await observed.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(DirectoryChangeKind.Created, change.Kind);
            Assert.True(SamePath(change.FullPath, expected));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Deleted_directory_is_observed()
    {
        var root = CreateTempRoot();
        var target = Path.Combine(root, "DeleteMe");
        Directory.CreateDirectory(target);

        try
        {
            using var watcher = new DirectoryChangeWatcher();
            watcher.Start(root);

            var observed = WaitForAsync(
                watcher,
                x => x.Kind == DirectoryChangeKind.Deleted &&
                     SamePath(x.FullPath, target));

            Directory.Delete(target);

            var change = await observed.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(DirectoryChangeKind.Deleted, change.Kind);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Renamed_directory_preserves_old_and_new_paths()
    {
        var root = CreateTempRoot();
        var oldPath = Path.Combine(root, "OldName");
        var newPath = Path.Combine(root, "NewName");
        Directory.CreateDirectory(oldPath);

        try
        {
            using var watcher = new DirectoryChangeWatcher();
            watcher.Start(root);

            var observed = WaitForAsync(
                watcher,
                x => x.Kind == DirectoryChangeKind.Renamed &&
                     SamePath(x.FullPath, newPath));

            Directory.Move(oldPath, newPath);

            var change = await observed.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotNull(change.OldFullPath);
            Assert.True(SamePath(change.OldFullPath!, oldPath));
            Assert.True(SamePath(change.FullPath, newPath));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Nested_changes_are_observed()
    {
        var root = CreateTempRoot();
        var nested = Path.Combine(root, "A", "B");
        Directory.CreateDirectory(nested);

        try
        {
            using var watcher = new DirectoryChangeWatcher();
            watcher.Start(root);

            var file = Path.Combine(nested, "test.txt");
            var observed = WaitForAsync(
                watcher,
                x => SamePath(x.FullPath, file) &&
                     (x.Kind == DirectoryChangeKind.Created ||
                      x.Kind == DirectoryChangeKind.Changed));

            await File.WriteAllTextAsync(file, "atlas");

            var change = await observed.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(SamePath(change.FullPath, file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public void Invalid_root_is_rejected()
    {
        using var watcher = new DirectoryChangeWatcher();

        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => watcher.Start(missing));
    }

    [Fact]
    public void Dispose_stops_watcher()
    {
        var root = CreateTempRoot();

        try
        {
            var watcher = new DirectoryChangeWatcher();
            watcher.Start(root);

            Assert.True(watcher.IsRunning);

            watcher.Dispose();

            Assert.False(watcher.IsRunning);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static Task<DirectoryChange> WaitForAsync(
        DirectoryChangeWatcher watcher,
        Func<DirectoryChange, bool> predicate)
    {
        var tcs = new TaskCompletionSource<DirectoryChange>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<DirectoryChange>? handler = null;
        handler = (_, change) =>
        {
            if (!predicate(change))
                return;

            watcher.Changed -= handler;
            tcs.TrySetResult(change);
        };

        watcher.Changed += handler;
        return tcs.Task;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Dossier temporaire uniquement.
        }
    }
}
