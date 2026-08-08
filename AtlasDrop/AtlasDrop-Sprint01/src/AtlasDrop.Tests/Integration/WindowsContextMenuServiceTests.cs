using Xunit;

namespace AtlasDrop.Tests.Integration;

public sealed class WindowsContextMenuServiceTests
{
    private static string FindInfrastructureFile()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.Infrastructure",
                "Integration",
                "WindowsContextMenuService.cs");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "WindowsContextMenuService.cs introuvable.");
    }

    [Fact]
    public void Registration_is_per_user_not_hklm()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains(
            "Registry.CurrentUser",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Registry.LocalMachine",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Context_menu_targets_all_files()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains(
            @"Software\Classes\*\shell\AtlasDrop",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_label_is_correct()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains(
            "Ranger avec Atlas Drop",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_is_requested_at_the_top_and_uses_the_app_icon()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains("\"Position\"", code, StringComparison.Ordinal);
        Assert.Contains("\"Top\"", code, StringComparison.Ordinal);
        Assert.Contains("\"Icon\"", code, StringComparison.Ordinal);
        Assert.Contains("Quote(fullPath)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_passes_selected_file_as_first_argument()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains(
            "\\\"%1\\\"",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unregister_is_supported()
    {
        var code = File.ReadAllText(
            FindInfrastructureFile());

        Assert.Contains(
            "DeleteSubKeyTree",
            code,
            StringComparison.Ordinal);
    }
}
