using Xunit;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.FileOperations;

namespace AtlasDrop.Tests.FileSystem;

public sealed class SafeFileMoveRollbackTests : IDisposable
{
    private readonly string _root;
    private readonly string _dest;

    public SafeFileMoveRollbackTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropSafeMoveRollbackTests",
            Guid.NewGuid().ToString("N"));

        _dest = Path.Combine(_root, "Dest");
        Directory.CreateDirectory(_dest);
    }

    [Fact]
    public async Task Verification_failure_triggers_rollback()
    {
        var source = Path.Combine(_root, "source.pdf");
        File.WriteAllText(source, "abc");

        var service = new SafeFileMoveService(
            verification: new AlwaysFailVerification());

        var result = await service.MoveAsync(
            new SafeMoveRequest(
                _root,
                source,
                _dest,
                "facture.pdf"));

        Assert.False(result.Success);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(_dest, "facture.pdf")));

        Assert.Contains(
            "Rollback effectué",
            result.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AlwaysFailVerification
        : IMoveVerificationService
    {
        public MoveVerificationResult Verify(
            string sourcePath,
            string destinationPath,
            long expectedSize,
            string? expectedSha256 = null)
        {
            return new MoveVerificationResult(
                false,
                true,
                true,
                false,
                false,
                "Vérification simulée en échec.");
        }
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
