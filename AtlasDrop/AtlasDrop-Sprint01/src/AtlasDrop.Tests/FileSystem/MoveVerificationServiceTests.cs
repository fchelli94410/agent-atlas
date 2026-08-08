using Xunit;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class MoveVerificationServiceTests : IDisposable
{
    private readonly string _root;

    public MoveVerificationServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropMoveVerificationTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Valid_move_state_is_verified()
    {
        var source = Path.Combine(_root, "source.pdf");
        var destination = Path.Combine(_root, "dest.pdf");

        File.WriteAllText(destination, "atlas-drop");

        var expectedSize = new FileInfo(destination).Length;
        var hash = new FileHashService().ComputeSha256(destination);

        var result = new MoveVerificationService().Verify(
            source,
            destination,
            expectedSize,
            hash);

        Assert.True(result.Success);
        Assert.True(result.DestinationExists);
        Assert.True(result.SourceAbsent);
        Assert.True(result.SizeMatches);
        Assert.True(result.HashMatches);
    }

    [Fact]
    public void Existing_source_fails_verification()
    {
        var source = Path.Combine(_root, "source.pdf");
        var destination = Path.Combine(_root, "dest.pdf");

        File.WriteAllText(source, "abc");
        File.WriteAllText(destination, "abc");

        var result = new MoveVerificationService().Verify(
            source,
            destination,
            new FileInfo(destination).Length);

        Assert.False(result.Success);
        Assert.False(result.SourceAbsent);
    }

    [Fact]
    public void Missing_destination_fails_verification()
    {
        var result = new MoveVerificationService().Verify(
            Path.Combine(_root, "source.pdf"),
            Path.Combine(_root, "missing.pdf"),
            3);

        Assert.False(result.Success);
        Assert.False(result.DestinationExists);
    }

    [Fact]
    public void Size_mismatch_fails_verification()
    {
        var destination = Path.Combine(_root, "dest.pdf");
        File.WriteAllText(destination, "abcdef");

        var result = new MoveVerificationService().Verify(
            Path.Combine(_root, "source.pdf"),
            destination,
            expectedSize: 1);

        Assert.False(result.Success);
        Assert.False(result.SizeMatches);
    }

    [Fact]
    public void Hash_mismatch_fails_verification()
    {
        var destination = Path.Combine(_root, "dest.pdf");
        File.WriteAllText(destination, "abcdef");

        var result = new MoveVerificationService().Verify(
            Path.Combine(_root, "source.pdf"),
            destination,
            new FileInfo(destination).Length,
            new string('A', 64));

        Assert.False(result.Success);
        Assert.False(result.HashMatches);
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
