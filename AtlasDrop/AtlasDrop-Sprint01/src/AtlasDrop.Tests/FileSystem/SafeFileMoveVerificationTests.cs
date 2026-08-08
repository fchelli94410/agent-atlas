using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.FileOperations;

namespace AtlasDrop.Tests.FileSystem;

public sealed class SafeFileMoveVerificationTests : IDisposable
{
    private readonly string _root;
    private readonly string _dest;

    public SafeFileMoveVerificationTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropSafeMoveVerifyTests",
            Guid.NewGuid().ToString("N"));

        _dest = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(_dest);
    }

    [Fact]
    public async Task Successful_move_reports_verified_message()
    {
        var source = Path.Combine(_root, "source.pdf");
        File.WriteAllText(source, "verified-content");

        var result = await new SafeFileMoveService().MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.True(result.Success);
        Assert.Contains(
            "vérifié",
            result.Message,
            StringComparison.OrdinalIgnoreCase);
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
