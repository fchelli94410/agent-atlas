using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowLearningSettingsTests
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
    [InlineData("LearningEnabledCheckBox")]
    [InlineData("ResetLearningButton")]
    [InlineData("LearningStatusText")]
    public void Learning_controls_exist(string name)
    {
        var xaml = File.ReadAllText(
            FindFile("MainWindow.xaml"));

        Assert.Contains(
            $"x:Name=\"{name}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Learning_toggle_is_wired()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "OnLearningEnabledChanged",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_learningSettings.SetEnabled",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_learning_is_wired()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "OnResetLearningClicked",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_learningService.ClearAsync",
            code,
            StringComparison.Ordinal);
    }
}
