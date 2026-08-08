using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class MovePreflightService : IMovePreflightService
{
    private readonly IWindowsFileNamePolicy _fileNamePolicy;
    private readonly IWindowsPathLengthPolicy _pathLengthPolicy;
    private readonly IDuplicateDetectionService _duplicateDetection;

    public MovePreflightService(
        IWindowsFileNamePolicy? fileNamePolicy = null,
        IWindowsPathLengthPolicy? pathLengthPolicy = null,
        IDuplicateDetectionService? duplicateDetection = null)
    {
        _fileNamePolicy = fileNamePolicy ?? new WindowsFileNamePolicy();
        _pathLengthPolicy = pathLengthPolicy ?? new WindowsPathLengthPolicy();
        _duplicateDetection = duplicateDetection ?? new DuplicateDetectionService();
    }

    public MovePreflightResult Validate(MovePreflightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var warnings = new List<string>();

        var source = Normalize(request.SourceFilePath, errors, "source");
        var root = Normalize(request.OneDriveRoot, errors, "racine OneDrive");
        var destinationDirectory = Normalize(
            request.DestinationDirectory,
            errors,
            "destination");

        var emptyDuplicate = new DuplicateCheckResult(
            DuplicateMatchKind.None,
            string.Empty,
            string.Empty,
            false,
            "Non évalué.");

        if (errors.Count > 0)
        {
            return new MovePreflightResult(
                false,
                source ?? string.Empty,
                string.Empty,
                emptyDuplicate,
                errors,
                warnings);
        }

        if (!File.Exists(source!))
            errors.Add("Le fichier source n'existe pas.");

        if (!Directory.Exists(destinationDirectory!))
            errors.Add("Le dossier destination n'existe pas.");

        if (!IsInsideRoot(root!, destinationDirectory!))
            errors.Add("La destination est hors du OneDrive autorisé.");

        var safeName = _fileNamePolicy.Sanitize(request.ProposedFileName);

        if (!safeName.IsValid)
            errors.Add("Le nom de fichier proposé est invalide.");

        if (!string.Equals(
            safeName.SafeFileName,
            request.ProposedFileName,
            StringComparison.Ordinal))
        {
            warnings.Add("Le nom proposé nécessite une normalisation Windows.");
        }

        var pathCheck = _pathLengthPolicy.CheckDestination(
            destinationDirectory!,
            safeName.SafeFileName);

        if (!pathCheck.IsSafe)
            errors.Add(pathCheck.Message);

        if (errors.Count > 0)
        {
            return new MovePreflightResult(
                false,
                source!,
                pathCheck.NormalizedPath,
                emptyDuplicate,
                errors,
                warnings);
        }

        if (!CanReadSource(source!))
            errors.Add("Le fichier source est inaccessible ou verrouillé.");

        if (!CanWriteDirectory(destinationDirectory!))
            errors.Add("Le dossier destination n'est pas accessible en écriture.");

        var duplicate = _duplicateDetection.Check(
            new DuplicateCheckRequest(
                source!,
                destinationDirectory!,
                safeName.SafeFileName));

        if (duplicate.MatchKind == DuplicateMatchKind.ExactDuplicate)
            warnings.Add("Un doublon exact existe déjà à destination.");
        else if (duplicate.MatchKind == DuplicateMatchKind.SameSize)
            warnings.Add("Un fichier du même nom et de même taille existe déjà.");
        else if (duplicate.MatchKind == DuplicateMatchKind.NameCollision)
            warnings.Add("Un fichier porte déjà ce nom.");

        return new MovePreflightResult(
            errors.Count == 0,
            source!,
            duplicate.DestinationPath,
            duplicate,
            errors,
            warnings);
    }

    private static string? Normalize(
        string path,
        ICollection<string> errors,
        string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"Chemin {label} vide.");
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            errors.Add($"Chemin {label} invalide.");
            return null;
        }
    }

    private static bool IsInsideRoot(
        string root,
        string candidate)
    {
        root = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        candidate = candidate.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;

        return candidate.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanReadSource(string sourcePath)
    {
        try
        {
            using var stream = File.Open(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            return stream.CanRead;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanWriteDirectory(string directory)
    {
        var probe = Path.Combine(
            directory,
            $".atlasdrop-write-{Guid.NewGuid():N}.tmp");

        try
        {
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            catch
            {
            }

            return false;
        }
    }
}
