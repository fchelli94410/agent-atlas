using AtlasDrop.Core.Configuration;

namespace AtlasDrop.Core.Security;

public sealed class PathPolicy
{
    private readonly string _root;
    private readonly int _maxDepth;
    private readonly FolderExclusionRules _exclusionRules;

    public PathPolicy(AtlasDropOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.OneDriveRoot))
            throw new ArgumentException("La racine OneDrive est obligatoire.", nameof(options));

        _root = Normalize(options.OneDriveRoot);
        _maxDepth = options.MaxSuggestedDepth;
        _exclusionRules = new FolderExclusionRules(options);
    }

    public bool IsInsideAuthorizedRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        try
        {
            fullPath = Normalize(path);
        }
        catch
        {
            return false;
        }

        return string.Equals(fullPath, _root, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(
                   _root + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    public bool IsDestinationAllowed(string path)
    {
        if (!IsInsideAuthorizedRoot(path))
            return false;

        string fullPath;
        try
        {
            fullPath = Normalize(path);
        }
        catch
        {
            return false;
        }

        var relative = Path.GetRelativePath(_root, fullPath);

        if (relative == ".")
            return false;

        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > _maxDepth)
            return false;

        return !_exclusionRules.IsExcludedPath(_root, fullPath);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
