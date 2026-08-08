using System.Security.Cryptography;
using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class FileHashService : IFileHashService
{
    public string ComputeSha256(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Chemin fichier obligatoire.", nameof(filePath));

        using var stream = File.Open(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var sha256 = SHA256.Create();

        var hash = sha256.ComputeHash(stream);

        return Convert.ToHexString(hash);
    }
}
