using Xunit;
using AtlasDrop.FileOperations;

namespace AtlasDrop.Tests.FileSystem;

public sealed class MoveRollbackServiceTests : IDisposable
{
    private readonly string _root;

    public MoveRollbackServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropRollbackTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Destination_is_restored_to_original_source()
    {
        var source = Path.Combine(_root, "source.pdf");
        var destination = Path.Combine(_root, "dest.pdf");

        File.WriteAllText(destination, "content");

        var result = new MoveRollbackService().TryRollback(
            source,
            destination);

        Assert.True(result.Success);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(destination));
        Assert.Equal("content", File.ReadAllText(source));
    }

    [Fact]
    public void Existing_original_source_prevents_overwrite()
    {
        var source = Path.Combine(_root, "source.pdf");
        var destination = Path.Combine(_root, "dest.pdf");

        File.WriteAllText(source, "original");
        File.WriteAllText(destination, "moved");

        var result = new MoveRollbackService().TryRollback(
            source,
            destination);

        Assert.False(result.Success);
        Assert.Equal("original", File.ReadAllText(source));
        Assert.Equal("moved", File.ReadAllText(destination));
    }

    [Fact]
    public void Missing_destination_is_reported()
    {
        var result = new MoveRollbackService().TryRollback(
            Path.Combine(_root, "source.pdf"),
            Path.Combine(_root, "missing.pdf"));

        Assert.False(result.Success);
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
