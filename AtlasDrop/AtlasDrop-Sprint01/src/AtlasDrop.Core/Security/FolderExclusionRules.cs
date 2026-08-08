using AtlasDrop.Core.Configuration;

namespace AtlasDrop.Core.Security;

public sealed class FolderExclusionRules
{
    private readonly HashSet<string> _explicitNames;

    public FolderExclusionRules(AtlasDropOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _explicitNames = new HashSet<string>(
            options.ExcludedFolderNames ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsExcludedName(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return true;

        var name = folderName.Trim();

        if (_explicitNames.Contains(name))
            return true;

        if (name.StartsWith(".", StringComparison.Ordinal) ||
            name.StartsWith("$", StringComparison.Ordinal))
            return true;

        return IsTemporaryName(name) ||
               IsCacheName(name) ||
               IsRecycleBinName(name) ||
               IsTechnicalName(name);
    }

    public bool IsExcludedPath(string rootPath, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) ||
            string.IsNullOrWhiteSpace(candidatePath))
            return true;

        string root;
        string candidate;

        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        }
        catch
        {
            return true;
        }

        if (!candidate.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return true;

        var relative = Path.GetRelativePath(root, candidate);

        if (relative == ".")
            return true;

        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(IsExcludedName);
    }

    private static bool IsTemporaryName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized is
            "temp" or
            "tmp" or
            "temporary" or
            "provisoire" or
            "provisoires" or
            "a supprimer" or
            "à supprimer";
    }

    private static bool IsCacheName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized.Contains("cache", StringComparison.Ordinal) ||
               normalized is "thumbs" or "thumbnail" or "thumbnails";
    }

    private static bool IsRecycleBinName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized is
            "$recycle.bin" or
            "recycler" or
            "recycled" or
            "corbeille";
    }

    private static bool IsTechnicalName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized is
            "system volume information" or
            "node_modules" or
            "bin" or
            "obj" or
            ".git" or
            ".vs";
    }
}
