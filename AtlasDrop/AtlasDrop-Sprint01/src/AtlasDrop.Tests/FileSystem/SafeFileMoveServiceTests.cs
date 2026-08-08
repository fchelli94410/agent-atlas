using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.FileOperations;

namespace AtlasDrop.Tests.FileSystem;

public sealed class SafeFileMoveServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dest;

    public SafeFileMoveServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropMoveTests",
            Guid.NewGuid().ToString("N"));

        _dest = Path.Combine(_root, "Clients");
        Directory.CreateDirectory(_dest);
    }

    [Fact]
    public async Task Valid_file_is_moved()
    {
        var source = CreateSource("source.pdf", "atlas-drop");

        var result = await Service().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.Success);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(result.DestinationPath!));
    }

    [Fact]
    public async Task Existing_destination_is_never_overwritten()
    {
        var source = CreateSource("source.pdf", "new-content");
        var existing = Path.Combine(_dest, "facture.pdf");

        File.WriteAllText(existing, "existing-content");

        var result = await Service().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.Success);
        Assert.True(result.RenamedForConflict);
        Assert.Equal("existing-content", File.ReadAllText(existing));
        Assert.EndsWith(
            "facture (2).pdf",
            result.DestinationPath!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Numbered_conflicts_increment_until_free()
    {
        var source = CreateSource("source.pdf", "new-content");

        File.WriteAllText(Path.Combine(_dest, "facture.pdf"), "a");
        File.WriteAllText(Path.Combine(_dest, "facture (2).pdf"), "b");

        var result = await Service().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.Success);
        Assert.EndsWith(
            "facture (3).pdf",
            result.DestinationPath!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exact_duplicate_is_not_moved()
    {
        var source = CreateSource("source.pdf", "same-content");
        var existing = Path.Combine(_dest, "facture.pdf");

        File.WriteAllText(existing, "same-content");

        var result = await Service().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.False(result.Success);
        Assert.True(result.ExactDuplicateDetected);
        Assert.True(File.Exists(source));
        Assert.Equal("same-content", File.ReadAllText(existing));
    }

    [Fact]
    public async Task Destination_outside_root_is_rejected_without_moving_source()
    {
        var source = CreateSource("source.pdf", "abc");

        var outside = Path.Combine(
            Path.GetTempPath(),
            "OutsideAtlasDrop",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outside);

        var result = await Service().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                outside,
                "facture.pdf"));

        Assert.False(result.Success);
        Assert.True(File.Exists(source));

        try
        {
            Directory.Delete(outside, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task Cancellation_before_move_preserves_source()
    {
        var source = CreateSource("source.pdf", "abc");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Service().MoveAsync(
                new SafeMoveRequest(
                    _root,
                    source,
                    _dest,
                    "facture.pdf"),
                cts.Token));

        Assert.True(File.Exists(source));
    }

    private string CreateSource(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static SafeFileMoveService Service() => new();

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
