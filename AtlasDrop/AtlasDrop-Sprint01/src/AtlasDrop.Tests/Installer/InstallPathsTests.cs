using Xunit;
using AtlasDrop.Installer;

namespace AtlasDrop.Tests.Installer;

public sealed class InstallPathsTests
{
    [Fact]
    public void Install_path_is_per_user_local_app_data()
    {
        var root = InstallPaths.GetInstallRoot();

        Assert.Contains(
            "AtlasDrop",
            root,
            StringComparison.OrdinalIgnoreCase);

        Assert.EndsWith(
            "AtlasDrop",
            root,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installed_exe_name_is_correct()
    {
        Assert.EndsWith(
            Path.Combine("App", "AtlasDrop.App.exe"),
            InstallPaths.GetInstalledExePath(),
            StringComparison.OrdinalIgnoreCase);
    }
}
