using Xunit;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class FileHashServiceTests : IDisposable
{
    private readonly string _root;

    public FileHashServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropHashTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Same_content_has_same_sha256()
    {
        var a = Path.Combine(_root, "a.bin");
        var b = Path.Combine(_root, "b.bin");

        File.WriteAllText(a, "atlas-drop");
        File.WriteAllText(b, "atlas-drop");

        var service = new FileHashService();

        Assert.Equal(
            service.ComputeSha256(a),
            service.ComputeSha256(b));
    }

    [Fact]
    public void Different_content_has_different_sha256()
    {
        var a = Path.Combine(_root, "a.bin");
        var b = Path.Combine(_root, "b.bin");

        File.WriteAllText(a, "atlas-drop-a");
        File.WriteAllText(b, "atlas-drop-b");

        var service = new FileHashService();

        Assert.NotEqual(
            service.ComputeSha256(a),
            service.ComputeSha256(b));
    }

    [Fact]
    public void Hash_is_64_hex_characters()
    {
        var a = Path.Combine(_root, "a.bin");
        File.WriteAllText(a, "atlas-drop");

        var hash = new FileHashService().ComputeSha256(a);

        Assert.Equal(64, hash.Length);
        Assert.All(
            hash,
            c => Assert.True(
                char.IsDigit(c) ||
                (c >= 'A' && c <= 'F')));
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
