using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowAutoRenameTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                relativePath);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    [Theory]
    [InlineData("AutoRenameCheckBox")]
    [InlineData("AutoRenameStatusText")]
    public void Auto_rename_controls_exist(string name)
    {
        var xaml = File.ReadAllText(
            FindFile("MainWindow.xaml"));

        Assert.Contains(
            $"x:Name=\"{name}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_is_opt_in_every_time()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "AutoRenameCheckBox.IsChecked = false",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "AutoRenameCheckBox.IsChecked = true",
            code,
            StringComparison.Ordinal);
    }
}
