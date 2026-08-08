namespace AtlasDrop.Infrastructure.Database;

public static class AtlasDropDatabaseFactory
{
    public static AtlasDropDatabase CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
            throw new InvalidOperationException("LocalAppData est indisponible.");

        var path = Path.Combine(localAppData, "AtlasDrop", "Data", "atlasdrop.db");
        return new AtlasDropDatabase(path);
    }
}
