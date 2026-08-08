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
    public void Auto_rename_policy_is_used_by_ui()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "HighConfidenceAutoRenamePolicy",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_autoRenamePolicy.Decide",
            code,
            StringComparison.Ordinal);
    }
}
