namespace AtlasDrop.Core.Configuration;

public sealed class AtlasDropOptionsValidator
{
    public IReadOnlyList<string> Validate(AtlasDropOptions? options)
    {
        var errors = new List<string>();

        if (options is null)
        {
            errors.Add("La configuration Atlas Drop est absente.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(options.OneDriveRoot))
        {
            errors.Add("La racine OneDrive est obligatoire.");
        }
        else
        {
            try
            {
                var full = Path.GetFullPath(options.OneDriveRoot);

                if (!Path.IsPathFullyQualified(full))
                    errors.Add("La racine OneDrive doit être un chemin absolu.");

                var root = Path.GetPathRoot(full);
                if (string.IsNullOrWhiteSpace(root))
                    errors.Add("La racine OneDrive est invalide.");
            }
            catch
            {
                errors.Add("La racine OneDrive contient un chemin invalide.");
            }
        }

        if (options.MaxSuggestedDepth < 1 || options.MaxSuggestedDepth > 12)
            errors.Add("MaxSuggestedDepth doit être compris entre 1 et 12.");

        if (options.ExcludedFolderNames is null)
            errors.Add("La liste des exclusions est obligatoire.");

        return errors;
    }

    public void EnsureValid(AtlasDropOptions options)
    {
        var errors = Validate(options);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
    }
}
