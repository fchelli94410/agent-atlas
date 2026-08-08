using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.Tests.FileSystem;

public sealed class SafeFolderCreationServiceTests : IDisposable
{
    private readonly string _root;

    public SafeFolderCreationServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Empty_name_is_rejected()
    {
        var result = Service().Validate(
            new FolderCreationRequest(_root, _root, "   "));

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("A:B")]
    [InlineData("A?B")]
    [InlineData("A/B")]
    [InlineData("A\\B")]
    [InlineData("A|B")]
    public void Invalid_windows_characters_are_rejected(string name)
    {
        var result = Service().Validate(
            new FolderCreationRequest(_root, _root, name));

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void Reserved_windows_names_are_rejected(string name)
    {
        var result = Service().Validate(
            new FolderCreationRequest(_root, _root, name));

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("Bureau")]
    [InlineData("Documents")]
    [InlineData("Images")]
    [InlineData("99 - Archives")]
    [InlineData("pour voir")]
    public void Excluded_destination_names_are_rejected(string name)
    {
        var result = Service().Validate(
            new FolderCreationRequest(_root, _root, name));

        Assert.False(result.Success);
    }

    [Fact]
    public void Parent_outside_root_is_rejected()
    {
        var outside = Path.Combine(
            Path.GetTempPath(),
            "OutsideAtlasDrop");

        var result = Service().Validate(
            new FolderCreationRequest(
                _root,
                outside,
                "Client"));

        Assert.False(result.Success);
    }

    [Fact]
    public void Level_four_creation_is_allowed()
    {
        var parent = Path.Combine(
            _root,
            "A",
            "B",
            "C");

        Directory.CreateDirectory(parent);

        var result = Service().Validate(
            new FolderCreationRequest(
                _root,
                parent,
                "D"));

        Assert.True(result.Success);
    }

    [Fact]
    public void Level_five_creation_is_rejected()
    {
        var parent = Path.Combine(
            _root,
            "A",
            "B",
            "C",
            "D");

        Directory.CreateDirectory(parent);

        var result = Service().Validate(
            new FolderCreationRequest(
                _root,
                parent,
                "E"));

        Assert.False(result.Success);
    }

    [Fact]
    public void Existing_folder_is_rejected()
    {
        var existing = Path.Combine(
            _root,
            "Client");

        Directory.CreateDirectory(existing);

        var result = Service().Validate(
            new FolderCreationRequest(
                _root,
                _root,
                "Client"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Valid_folder_is_created_and_verified()
    {
        var result = await Service().CreateAsync(
            new FolderCreationRequest(
                _root,
                _root,
                "Client EDF"));

        Assert.True(result.Success);
        Assert.NotNull(result.FullPath);
        Assert.True(Directory.Exists(result.FullPath!));
    }

    [Fact]
    public async Task Cancellation_is_honored()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Service().CreateAsync(
                new FolderCreationRequest(
                    _root,
                    _root,
                    "Client"),
                cts.Token));
    }

    private static SafeFolderCreationService Service() => new();

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
