using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class DuplicateDetectionSha256Tests : IDisposable
{
    private readonly string _root;

    public DuplicateDetectionSha256Tests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropDuplicateHashTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Same_name_size_and_content_is_exact_duplicate()
    {
        var source = Path.Combine(_root, "source.pdf");
        File.WriteAllText(source, "same-content");

        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);

        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "same-content");

        var result = new DuplicateDetectionService().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.Equal(
            DuplicateMatchKind.ExactDuplicate,
            result.MatchKind);

        Assert.Contains(
            "SHA-256",
            result.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Same_name_and_size_but_different_content_is_not_exact_duplicate()
    {
        var source = Path.Combine(_root, "source.pdf");
        File.WriteAllText(source, "ABCDEF");

        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);

        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "GHIJKL");

        var result = new DuplicateDetectionService().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.Equal(
            DuplicateMatchKind.SameSize,
            result.MatchKind);
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
