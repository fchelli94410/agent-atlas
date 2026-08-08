namespace AtlasDrop.Infrastructure.Logging;

public static class AtlasLogPath
{
    public static string GetDefaultDirectory()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            "AtlasDrop",
            "Logs");
    }
}
