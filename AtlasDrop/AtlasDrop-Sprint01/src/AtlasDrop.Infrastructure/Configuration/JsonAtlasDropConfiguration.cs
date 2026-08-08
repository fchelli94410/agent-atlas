using System.Text.Json;
using AtlasDrop.Core.Configuration;

namespace AtlasDrop.Infrastructure.Configuration;

public sealed class JsonAtlasDropConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly AtlasDropOptionsValidator _validator;

    public JsonAtlasDropConfiguration(AtlasDropOptionsValidator? validator = null)
    {
        _validator = validator ?? new AtlasDropOptionsValidator();
    }

    public AtlasDropOptions Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin de configuration est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier de configuration Atlas Drop est introuvable.", fullPath);

        var json = File.ReadAllText(fullPath);
        var options = JsonSerializer.Deserialize<AtlasDropOptions>(json, JsonOptions)
                      ?? throw new InvalidDataException("La configuration Atlas Drop est vide ou illisible.");

        _validator.EnsureValid(options);
        return options;
    }

    public void Save(string filePath, AtlasDropOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _validator.EnsureValid(options);

        var fullPath = Path.GetFullPath(filePath);
        var parent = Path.GetDirectoryName(fullPath);

        if (string.IsNullOrWhiteSpace(parent))
            throw new InvalidOperationException("Le dossier de configuration est invalide.");

        Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(options, JsonOptions);
        File.WriteAllText(fullPath, json);
    }
}
