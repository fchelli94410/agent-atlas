using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class MoveVerificationService
    : IMoveVerificationService
{
    private readonly IFileHashService _hashService;

    public MoveVerificationService(
        IFileHashService? hashService = null)
    {
        _hashService = hashService ?? new FileHashService();
    }

    public MoveVerificationResult Verify(
        string sourcePath,
        string destinationPath,
        long expectedSize,
        string? expectedSha256 = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source obligatoire.", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination obligatoire.", nameof(destinationPath));

        var destinationExists = File.Exists(destinationPath);
        var sourceAbsent = !File.Exists(sourcePath);

        if (!destinationExists)
        {
            return new MoveVerificationResult(
                false,
                false,
                sourceAbsent,
                false,
                false,
                "Le fichier destination est absent.");
        }

        var destinationInfo = new FileInfo(destinationPath);
        var sizeMatches = destinationInfo.Length == expectedSize;

        var hashMatches = true;

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            try
            {
                var actualHash = _hashService.ComputeSha256(destinationPath);

                hashMatches = string.Equals(
                    actualHash,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                hashMatches = false;
            }
        }

        var success =
            destinationExists &&
            sourceAbsent &&
            sizeMatches &&
            hashMatches;

        var message = success
            ? "Déplacement vérifié."
            : BuildFailureMessage(
                sourceAbsent,
                sizeMatches,
                hashMatches);

        return new MoveVerificationResult(
            success,
            destinationExists,
            sourceAbsent,
            sizeMatches,
            hashMatches,
            message);
    }

    private static string BuildFailureMessage(
        bool sourceAbsent,
        bool sizeMatches,
        bool hashMatches)
    {
        var parts = new List<string>();

        if (!sourceAbsent)
            parts.Add("la source existe encore");

        if (!sizeMatches)
            parts.Add("la taille destination ne correspond pas");

        if (!hashMatches)
            parts.Add("le SHA-256 ne correspond pas");

        return parts.Count == 0
            ? "Vérification post-déplacement échouée."
            : "Vérification échouée : " + string.Join(", ", parts) + ".";
    }
}
