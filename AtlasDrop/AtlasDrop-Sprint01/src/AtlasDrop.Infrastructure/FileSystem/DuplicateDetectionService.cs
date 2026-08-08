using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class DuplicateDetectionService
    : IDuplicateDetectionService
{
    private readonly IFileHashService _hashService;

    public DuplicateDetectionService(
        IFileHashService? hashService = null)
    {
        _hashService = hashService
            ?? new FileHashService();
    }

    public DuplicateCheckResult Check(
        DuplicateCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SourceFilePath))
            throw new ArgumentException("Fichier source obligatoire.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.DestinationDirectory))
            throw new ArgumentException("Destination obligatoire.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProposedFileName))
            throw new ArgumentException("Nom proposé obligatoire.", nameof(request));

        var source = Path.GetFullPath(request.SourceFilePath);
        var destinationDirectory = Path.GetFullPath(request.DestinationDirectory);
        var proposedName = Path.GetFileName(request.ProposedFileName);
        var destination = Path.Combine(destinationDirectory, proposedName);

        if (!File.Exists(destination))
        {
            return new DuplicateCheckResult(
                DuplicateMatchKind.None,
                destination,
                destination,
                false,
                "Aucun conflit de nom.");
        }

        var available = FindAvailableName(
            destinationDirectory,
            proposedName);

        if (!File.Exists(source))
        {
            return new DuplicateCheckResult(
                DuplicateMatchKind.NameCollision,
                destination,
                available,
                true,
                "Un fichier porte déjà ce nom.");
        }

        var sourceInfo = new FileInfo(source);
        var destinationInfo = new FileInfo(destination);

        if (sourceInfo.Length == destinationInfo.Length)
        {
            try
            {
                var sourceHash = _hashService.ComputeSha256(source);
                var destinationHash = _hashService.ComputeSha256(destination);

                if (string.Equals(
                    sourceHash,
                    destinationHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new DuplicateCheckResult(
                        DuplicateMatchKind.ExactDuplicate,
                        destination,
                        available,
                        true,
                        "Doublon exact détecté par SHA-256.");
                }
            }
            catch (IOException)
            {
                // Le signal même taille reste exploitable si le hash échoue.
            }
            catch (UnauthorizedAccessException)
            {
                // Le signal même taille reste exploitable si le hash échoue.
            }

            return new DuplicateCheckResult(
                DuplicateMatchKind.SameSize,
                destination,
                available,
                true,
                "Un fichier du même nom et de même taille existe déjà.");
        }

        return new DuplicateCheckResult(
            DuplicateMatchKind.NameCollision,
            destination,
            available,
            true,
            "Un fichier porte déjà ce nom.");
    }

    private static string FindAvailableName(
        string destinationDirectory,
        string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        for (var i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(
                destinationDirectory,
                $"{baseName} ({i}){extension}");

            if (!File.Exists(candidate) &&
                !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException(
            "Impossible de trouver un nom de fichier disponible.");
    }
}
