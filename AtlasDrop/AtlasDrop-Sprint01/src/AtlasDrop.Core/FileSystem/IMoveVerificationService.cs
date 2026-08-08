namespace AtlasDrop.Core.FileSystem;

public interface IMoveVerificationService
{
    MoveVerificationResult Verify(
        string sourcePath,
        string destinationPath,
        long expectedSize,
        string? expectedSha256 = null);
}
