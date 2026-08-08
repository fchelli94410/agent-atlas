using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class DuplicateDetectionServiceTests : IDisposable
{
    private readonly string _root;

    public DuplicateDetectionServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropDuplicateTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void No_existing_destination_has_no_duplicate()
    {
        var source = CreateFile("source.pdf", "abc");
        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);

        var result = Service().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.Equal(DuplicateMatchKind.None, result.MatchKind);
        Assert.False(result.DestinationExists);
    }

    [Fact]
    public void Existing_name_is_detected()
    {
        var source = CreateFile("source.pdf", "abcdef");
        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);
        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "x");

        var result = Service().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.Equal(
            DuplicateMatchKind.NameCollision,
            result.MatchKind);

        Assert.EndsWith(
            "facture (2).pdf",
            result.SuggestedAvailablePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Same_size_is_detected_as_stronger_signal()
    {
        var source = CreateFile("source.pdf", "abcdef");
        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);
        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "ghijkl");

        var result = Service().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.Equal(
            DuplicateMatchKind.SameSize,
            result.MatchKind);
    }

    [Fact]
    public void Existing_numbered_names_are_skipped()
    {
        var source = CreateFile("source.pdf", "abcdef");
        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);

        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "x");
        File.WriteAllText(
            Path.Combine(destination, "facture (2).pdf"),
            "x");

        var result = Service().Check(
            new DuplicateCheckRequest(
                source,
                destination,
                "facture.pdf"));

        Assert.EndsWith(
            "facture (3).pdf",
            result.SuggestedAvailablePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_source_still_detects_name_collision()
    {
        var destination = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(destination);

        File.WriteAllText(
            Path.Combine(destination, "facture.pdf"),
            "x");

        var result = Service().Check(
            new DuplicateCheckRequest(
                Path.Combine(_root, "missing.pdf"),
                destination,
                "facture.pdf"));

        Assert.Equal(
            DuplicateMatchKind.NameCollision,
            result.MatchKind);
    }

    private string CreateFile(
        string name,
        string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DuplicateDetectionService Service() => new();

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
