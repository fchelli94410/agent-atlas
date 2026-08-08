using Xunit;
using AtlasDrop.Core.Indexing;
using AtlasDrop.Infrastructure.Indexing;

namespace AtlasDrop.Tests.Indexing;

public sealed class ScanCoordinatorTests
{
    [Fact]
    public async Task Status_moves_to_completed_after_success()
    {
        var root = CreateTempRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "A"));
            Directory.CreateDirectory(Path.Combine(root, "B"));

            var coordinator = new ScanCoordinator(new FolderScanner());

            var result = await coordinator.RunAsync(root);

            Assert.Equal(3, result.Count);
            Assert.Equal(ScanExecutionState.Completed, coordinator.Status.State);
            Assert.Equal(3, coordinator.Status.FoldersScanned);
            Assert.NotNull(coordinator.Status.StartedUtc);
            Assert.NotNull(coordinator.Status.CompletedUtc);
            Assert.Null(coordinator.Status.ErrorMessage);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Progress_events_are_raised()
    {
        var root = CreateTempRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "A"));
            Directory.CreateDirectory(Path.Combine(root, "B"));

            var coordinator = new ScanCoordinator(new FolderScanner());
            var observed = new List<ScanExecutionStatus>();

            coordinator.StatusChanged += (_, status) => observed.Add(status);

            await coordinator.RunAsync(root);

            Assert.Contains(observed, x => x.State == ScanExecutionState.Running);
            Assert.Contains(observed, x => x.FoldersScanned >= 1);
            Assert.Equal(ScanExecutionState.Completed, observed[^1].State);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_sets_cancelled_state()
    {
        var root = CreateTempRoot();

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var coordinator = new ScanCoordinator(new FolderScanner());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => coordinator.RunAsync(root, cts.Token));

            Assert.Equal(ScanExecutionState.Cancelled, coordinator.Status.State);
            Assert.NotNull(coordinator.Status.CompletedUtc);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Invalid_root_sets_failed_state()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        var coordinator = new ScanCoordinator(new FolderScanner());

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => coordinator.RunAsync(missing));

        Assert.Equal(ScanExecutionState.Failed, coordinator.Status.State);
        Assert.False(string.IsNullOrWhiteSpace(coordinator.Status.ErrorMessage));
    }

    [Fact]
    public async Task Initial_state_is_not_started()
    {
        var coordinator = new ScanCoordinator(new FolderScanner());

        Assert.Equal(ScanExecutionState.NotStarted, coordinator.Status.State);
        Assert.Equal(0, coordinator.Status.FoldersScanned);

        await Task.CompletedTask;
    }

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
