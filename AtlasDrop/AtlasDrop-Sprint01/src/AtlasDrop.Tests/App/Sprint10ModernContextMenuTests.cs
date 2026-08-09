using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint10ModernContextMenuTests
{
    private const string CommandClsid = "6B1C4C31-7F71-4C75-9E16-8D3FB7511E34";

    private static string FindRepositoryFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var root = current.FullName;
            var atlasDropRoot = Path.Combine(root, "installer");
            if (Directory.Exists(atlasDropRoot))
            {
                var candidate = Path.Combine(new[] { root }.Concat(segments).ToArray());
                if (File.Exists(candidate)) return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, segments));
    }

    [Fact]
    public void Sparse_package_registers_the_same_native_command_for_files_and_directories()
    {
        var manifest = File.ReadAllText(FindRepositoryFile("installer", "modern-context-menu", "AppxManifest.xml.template"));

        Assert.Contains("windows.comServer", manifest, StringComparison.Ordinal);
        Assert.Contains("windows.fileExplorerContextMenus", manifest, StringComparison.Ordinal);
        Assert.Contains($"Id=\"{CommandClsid}\"", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Clsid=\"{CommandClsid}\"", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=\"AtlasDropContextMenu.dll\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<desktop5:ItemType Type=\"*\">", manifest, StringComparison.Ordinal);
        Assert.Contains("<desktop5:ItemType Type=\"Directory\">", manifest, StringComparison.Ordinal);
        Assert.Contains("<uap10:AllowExternalContent>true</uap10:AllowExternalContent>", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_dll_implements_IExplorerCommand_and_launches_the_existing_app()
    {
        var native = File.ReadAllText(FindRepositoryFile("src", "AtlasDrop.ContextMenu.Native", "AtlasDropContextMenu.cpp"));

        Assert.Contains("public IExplorerCommand", native, StringComparison.Ordinal);
        Assert.Contains("Ranger avec Atlas Drop", native, StringComparison.Ordinal);
        Assert.Contains("SIGDN_FILESYSPATH", native, StringComparison.Ordinal);
        Assert.Contains("AtlasDrop.App.exe", native, StringComparison.Ordinal);
        Assert.Contains("DllGetClassObject", native, StringComparison.Ordinal);
        Assert.Contains("DllCanUnloadNow", native, StringComparison.Ordinal);
        Assert.Contains("0x6b1c4c31", native, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void App_identity_and_sparse_package_identity_are_kept_in_sync()
    {
        var appManifest = File.ReadAllText(FindRepositoryFile("src", "AtlasDrop.App", "app.manifest"));
        var project = File.ReadAllText(FindRepositoryFile("src", "AtlasDrop.App", "AtlasDrop.App.csproj"));

        Assert.Contains("publisher=\"CN=AtlasDrop\"", appManifest, StringComparison.Ordinal);
        Assert.Contains("packageName=\"AtlasDrop.ContextMenu\"", appManifest, StringComparison.Ordinal);
        Assert.Contains("applicationId=\"AtlasDrop\"", appManifest, StringComparison.Ordinal);
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_registers_and_uninstalls_the_sparse_identity_without_shipping_private_keys()
    {
        var register = File.ReadAllText(FindRepositoryFile("installer", "Register-ModernContextMenu.ps1"));
        var unregister = File.ReadAllText(FindRepositoryFile("installer", "Unregister-ModernContextMenu.ps1"));
        var installer = File.ReadAllText(FindRepositoryFile("installer", "AtlasDrop.iss"));

        Assert.Contains("Add-AppxPackage -Path $packagePathFull -ExternalLocation $externalLocationFull", register, StringComparison.Ordinal);
        Assert.Contains("Cert:\\CurrentUser\\TrustedPeople", register, StringComparison.Ordinal);
        Assert.Contains("Get-AppxPackage -Name $packageName", unregister, StringComparison.Ordinal);
        Assert.Contains("Remove-AppxPackage", unregister, StringComparison.Ordinal);
        Assert.Contains("Register-ModernContextMenu.ps1", installer, StringComparison.Ordinal);
        Assert.Contains("Unregister-ModernContextMenu.ps1", installer, StringComparison.Ordinal);
        Assert.DoesNotContain(".pfx", installer, StringComparison.OrdinalIgnoreCase);
    }
}
