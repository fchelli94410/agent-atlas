using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class MovePreflightServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dest;

    public MovePreflightServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropPreflightTests",
            Guid.NewGuid().ToString("N"));

        _dest = Path.Combine(_root, "Clients");

        Directory.CreateDirectory(_dest);
    }

    [Fact]
    public void Valid_move_can_proceed()
    {
        var source = CreateSource("source.pdf", "abc");

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.CanProceed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_source_is_rejected()
    {
        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                Path.Combine(_root, "missing.pdf"),
                _dest,
                "facture.pdf"));

        Assert.False(result.CanProceed);
        Assert.Contains(
            result.Errors,
            x => x.Contains("source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_destination_is_rejected()
    {
        var source = CreateSource("source.pdf", "abc");

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                Path.Combine(_root, "Missing"),
                "facture.pdf"));

        Assert.False(result.CanProceed);
    }

    [Fact]
    public void Destination_outside_root_is_rejected()
    {
        var source = CreateSource("source.pdf", "abc");

        var outside = Path.Combine(
            Path.GetTempPath(),
            "OutsideAtlasDrop",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outside);

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                outside,
                "facture.pdf"));

        Assert.False(result.CanProceed);

        try
        {
            Directory.Delete(outside, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void Invalid_name_is_rejected_or_normalized_with_warning()
    {
        var source = CreateSource("source.pdf", "abc");

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                _dest,
                "facture?.pdf"));

        Assert.Contains(
            result.Warnings,
            x => x.Contains("normalisation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Long_destination_path_is_rejected()
    {
        var source = CreateSource("source.pdf", "abc");

        var longName = new string('A', 230) + ".pdf";

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                _dest,
                longName));

        Assert.False(result.CanProceed);
    }

    [Fact]
    public void Name_collision_is_reported_without_blocking_preflight()
    {
        var source = CreateSource("source.pdf", "abcdef");
        File.WriteAllText(
            Path.Combine(_dest, "facture.pdf"),
            "x");

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.CanProceed);
        Assert.Equal(
            DuplicateMatchKind.NameCollision,
            result.Duplicate.MatchKind);

        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Exact_duplicate_is_reported_as_warning()
    {
        var source = CreateSource("source.pdf", "same");
        File.WriteAllText(
            Path.Combine(_dest, "facture.pdf"),
            "same");

        var result = Service().Validate(
            new MovePreflightRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.CanProceed);
        Assert.Equal(
            DuplicateMatchKind.ExactDuplicate,
            result.Duplicate.MatchKind);

        Assert.Contains(
            result.Warnings,
            x => x.Contains("doublon exact", StringComparison.OrdinalIgnoreCase));
    }

    private string CreateSource(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static MovePreflightService Service() => new();

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
