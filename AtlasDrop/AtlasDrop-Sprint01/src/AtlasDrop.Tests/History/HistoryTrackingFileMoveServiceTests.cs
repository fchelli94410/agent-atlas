using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.FileOperations;
using AtlasDrop.Infrastructure.History;

namespace AtlasDrop.Tests.History;

public sealed class HistoryTrackingFileMoveServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dest;

    public HistoryTrackingFileMoveServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropHistoryMoveTests",
            Guid.NewGuid().ToString("N"));

        _dest = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(_dest);
    }

    [Fact]
    public async Task Successful_move_is_logged()
    {
        var source = Path.Combine(_root, "source.pdf");
        File.WriteAllText(source, "atlas-drop");

        var repo = new InMemoryOperationHistoryRepository();

        var service = new HistoryTrackingFileMoveService(
            new SafeFileMoveService(),
            repo);

        var result = await service.MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.Success);

        var history = await repo.GetRecentAsync();

        var entry = Assert.Single(history);

        Assert.True(entry.Success);
        Assert.Equal("source.pdf", entry.OldFileName);
        Assert.Equal("facture.pdf", entry.NewFileName);
        Assert.Equal(_dest, entry.NewDestination);
        Assert.False(entry.Undone);
        Assert.False(string.IsNullOrWhiteSpace(entry.Sha256));
    }

    [Fact]
    public async Task Failed_move_is_also_logged()
    {
        var source = Path.Combine(_root, "missing.pdf");

        var repo = new InMemoryOperationHistoryRepository();

        var service = new HistoryTrackingFileMoveService(
            new SafeFileMoveService(),
            repo);

        var result = await service.MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.False(result.Success);

        var history = await repo.GetRecentAsync();

        var entry = Assert.Single(history);
        Assert.False(entry.Success);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
