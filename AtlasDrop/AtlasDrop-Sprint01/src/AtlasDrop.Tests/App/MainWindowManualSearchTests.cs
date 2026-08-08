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
    public void Manual_navigation_uses_readable_relative_onedrive_paths()
    {
        var xaml = File.ReadAllText(
            FindFile("MainWindow.xaml"));
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "DisplayMemberPath=\"DisplayPath\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToOneDriveDisplayPath",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "OneDrive › ",
            code,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SelectedManualDestinationText.Text = result.Path",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OneDrive_root_is_level_zero_and_children_stop_at_level_four()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "private const int MaxDepth = 4;",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "queue.Enqueue((root, 0));",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "var depth = current.Depth + 1;",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "GetDepth(_oneDriveRoot, x.Path) is >= 1 and <= MaxDepth",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (GetDepth(_oneDriveRoot, parent) >= MaxDepth)",
            code,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Math.Max(_options.MaxSuggestedDepth, 12)",
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
