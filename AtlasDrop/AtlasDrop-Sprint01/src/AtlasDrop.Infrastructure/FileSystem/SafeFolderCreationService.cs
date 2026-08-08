using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.Infrastructure.FileSystem;

public sealed class SafeFolderCreationService : IFolderCreationService
{
    private readonly IWindowsPathLengthPolicy _pathLengthPolicy;

    public SafeFolderCreationService(
        IWindowsPathLengthPolicy? pathLengthPolicy = null)
    {
        _pathLengthPolicy = pathLengthPolicy
            ?? new WindowsPathLengthPolicy();
    }

    private static readonly char[] InvalidChars =
    {
        '"', '*', ':', '<', '>', '?', '/', '\\', '|'
    };

    private static readonly HashSet<string> ReservedNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

    private static readonly string[] ForbiddenFolderNames =
    {
        "Bureau",
        "Documents",
        "Images",
        "99 - Archives",
        "pour voir"
    };

    public FolderCreationResult Validate(FolderCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OneDriveRoot))
            return Fail("Racine OneDrive manquante.");

        if (string.IsNullOrWhiteSpace(request.ParentPath))
            return Fail("Dossier parent manquant.");

        if (string.IsNullOrWhiteSpace(request.FolderName))
            return Fail("Nom de dossier vide.");

        var name = request.FolderName.Trim();

        if (name.IndexOfAny(InvalidChars) >= 0)
            return Fail("Le nom contient un caractère Windows interdit.");

        if (name.EndsWith(".", StringComparison.Ordinal) ||
            name.EndsWith(" ", StringComparison.Ordinal))
        {
            return Fail("Le nom ne peut pas finir par un point ou un espace.");
        }

        var stem = name.Split('.')[0];

        if (ReservedNames.Contains(stem))
            return Fail("Nom réservé par Windows.");

        if (ForbiddenFolderNames.Contains(
            name,
            StringComparer.OrdinalIgnoreCase))
        {
            return Fail("Ce dossier est exclu des destinations Atlas Drop.");
        }

        string root;
        string parent;
        string fullPath;

        try
        {
            root = Path.GetFullPath(request.OneDriveRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            parent = Path.GetFullPath(request.ParentPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            fullPath = Path.GetFullPath(
                Path.Combine(parent, name));
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return Fail("Chemin de dossier invalide.");
        }

        if (!IsInsideRoot(root, parent))
            return Fail("Le dossier parent est hors du OneDrive autorisé.");

        if (!IsInsideRoot(root, fullPath))
            return Fail("La destination sort du OneDrive autorisé.");

        var depth = GetDepth(root, fullPath);

        if (depth > 4)
            return Fail("La création dépasse le niveau 4 autorisé.");

        var pathLength = _pathLengthPolicy.Check(
            fullPath,
            safeLimit: 240);

        if (!pathLength.IsSafe)
            return Fail(pathLength.Message);

        if (Directory.Exists(fullPath))
            return Fail("Un dossier portant ce nom existe déjà.");

        return new FolderCreationResult(
            true,
            fullPath,
            "Création autorisée.");
    }

    public async Task<FolderCreationResult> CreateAsync(
        FolderCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);

        if (!validation.Success ||
            string.IsNullOrWhiteSpace(validation.FullPath))
        {
            return validation;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await Task.Run(
                () => Directory.CreateDirectory(validation.FullPath),
                cancellationToken);

            if (!Directory.Exists(validation.FullPath))
                return Fail("Le dossier n'a pas pu être vérifié après création.");

            return new FolderCreationResult(
                true,
                validation.FullPath,
                "Dossier créé.");
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("Accès refusé lors de la création du dossier.");
        }
        catch (PathTooLongException)
        {
            return Fail("Chemin trop long.");
        }
        catch (IOException)
        {
            return Fail("Erreur d'entrée/sortie lors de la création du dossier.");
        }
    }

    private static bool IsInsideRoot(
        string root,
        string candidate)
    {
        if (candidate.Equals(
            root,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int GetDepth(
        string root,
        string candidate)
    {
        if (candidate.Equals(
            root,
            StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var relative = Path.GetRelativePath(
            root,
            candidate);

        return relative
            .Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private static FolderCreationResult Fail(string message) =>
        new(false, null, message);
}
