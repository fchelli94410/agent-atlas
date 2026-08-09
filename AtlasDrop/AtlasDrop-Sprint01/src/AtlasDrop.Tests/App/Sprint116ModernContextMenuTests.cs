using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116ModernContextMenuTests
{
    private static string FindFromSolution(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath + " introuvable.");
    }

    [Fact]
    public void Sparse_package_registers_modern_windows11_command_for_files_and_directories()
    {
        var manifest = File.ReadAllText(FindFromSolution(Path.Combine("installer", "modern-context-menu", "AppxManifest.xml.template")));

        Assert.Contains("Category=\"windows.fileExplorerContextMenus\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<desktop5:ItemType Type=\"*\">", manifest, StringComparison.Ordinal);
        Assert.Contains("<desktop5:ItemType Type=\"Directory\">", manifest, StringComparison.Ordinal);
        Assert.Contains("Clsid=\"6B1C4C31-7F71-4C75-9E16-8D3FB7511E34\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_extension_is_a_real_iexplorercommand_and_keeps_menu_build_fast()
    {
        var native = File.ReadAllText(FindFromSolution(Path.Combine("src", "AtlasDrop.ContextMenu.Native", "AtlasDropContextMenu.cpp")));

        Assert.Contains("public IExplorerCommand", native, StringComparison.Ordinal);
        Assert.Contains("Ranger avec Atlas Drop", native, StringComparison.Ordinal);
        Assert.Contains("GetState(IShellItemArray* items", native, StringComparison.Ordinal);
        Assert.Contains("count == 1", native, StringComparison.Ordinal);
        Assert.Contains("ShellExecuteW", native, StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_reloads_explorer_after_success_so_shell_sees_updated_extension()
    {
        var script = File.ReadAllText(FindFromSolution(Path.Combine("installer", "Register-ModernContextMenu.ps1")));

        Assert.Contains("Add-AppxPackage", script, StringComparison.Ordinal);
        Assert.Contains("Restart-AtlasExplorerShell", script, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Force", script, StringComparison.Ordinal);
        Assert.Contains("Menu contextuel moderne enregistré", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_fallback_exists_for_both_files_and_folders_but_is_not_the_modern_registration()
    {
        var installer = File.ReadAllText(FindFromSolution(Path.Combine("installer", "AtlasDrop.iss")));

        Assert.Contains("Software\\Classes\\*\\shell\\AtlasDrop", installer, StringComparison.Ordinal);
        Assert.Contains("Software\\Classes\\Directory\\shell\\AtlasDrop", installer, StringComparison.Ordinal);
        Assert.Contains("Afficher plus d'options", installer, StringComparison.Ordinal);
    }
}
