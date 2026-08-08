namespace AtlasDrop.Installer;

public static class InstallPaths
{
    public static string GetInstallRoot()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, "AtlasDrop");
    }

    public static string GetAppDirectory() =>
        Path.Combine(GetInstallRoot(), "App");

    public static string GetInstalledExePath() =>
        Path.Combine(GetAppDirectory(), "AtlasDrop.App.exe");
}
