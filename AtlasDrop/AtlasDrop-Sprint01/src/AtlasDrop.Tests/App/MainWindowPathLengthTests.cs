using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowPathLengthTests
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

    [Fact]
    public void Path_length_status_is_visible()
    {
        var xaml = File.ReadAllText(
            FindFile("MainWindow.xaml"));

        Assert.Contains(
            "x:Name=\"PathLengthStatusText\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_uses_windows_path_length_policy()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "WindowsPathLengthPolicy",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "CheckDestination",
            code,
            StringComparison.Ordinal);
    }
}
