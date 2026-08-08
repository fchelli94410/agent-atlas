using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowManualSearchTests
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
    [InlineData("SearchResultsList")]
    [InlineData("SelectedManualDestinationText")]
    public void Manual_search_controls_exist(string controlName)
    {
        var xaml = File.ReadAllText(
            FindFile("MainWindow.xaml"));

        Assert.Contains(
            $"x:Name=\"{controlName}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Search_button_is_connected()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "SearchButton.Click += OnSearchClicked",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Enter_in_search_runs_manual_search()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "SearchTextBox.KeyDown += OnSearchTextBoxKeyDown",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "RunManualSearch()",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Selecting_manual_result_updates_destination()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "SelectedManualDestinationText.Text",
            code,
            StringComparison.Ordinal);
    }
}
