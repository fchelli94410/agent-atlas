using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowXamlTests
{
    private static string FindFile(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", name);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException(name + " introuvable.");
    }

    [Fact]
    public void Main_window_xaml_is_valid_xml() => Assert.Equal("Window", XDocument.Load(FindFile("MainWindow.xaml")).Root!.Name.LocalName);

    [Theory]
    [InlineData("YesButton")]
    [InlineData("NoButton")]
    [InlineData("ChooseButton")]
    [InlineData("CancelButton")]
    [InlineData("MoveHereButton")]
    [InlineData("ProposedPathText")]
    [InlineData("FolderTree")]
    [InlineData("StatusText")]
    public void Validated_workflow_controls_exist(string controlName)
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Contains(document.Descendants(), e => (string?)e.Attribute(x + "Name") == controlName);
    }

    [Fact]
    public void Manual_move_button_is_part_of_manual_mode()
    {
        var text = File.ReadAllText(FindFile("MainWindow.xaml"));
        Assert.Contains("DÉPLACER ICI", text, StringComparison.Ordinal);
        Assert.Contains("OnMoveHere", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_folder_tree_is_large_and_has_visible_vertical_scroll()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tree = Assert.Single(
            document.Descendants()
                .Where(e => (string?)e.Attribute(x + "Name") == "FolderTree"));

        Assert.Equal("300", (string?)tree.Attribute("MinHeight"));
        Assert.Equal("15", (string?)tree.Attribute("FontSize"));
        Assert.Equal(
            "Visible",
            (string?)tree.Attribute("ScrollViewer.VerticalScrollBarVisibility"));
        Assert.Equal(
            "Auto",
            (string?)tree.Attribute("ScrollViewer.HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void Manual_search_results_show_relative_display_paths()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var results = Assert.Single(
            document.Descendants()
                .Where(e => (string?)e.Attribute(x + "Name") == "SearchResultsList"));

        Assert.Equal("DisplayPath", (string?)results.Attribute("DisplayMemberPath"));
        Assert.Equal("140", (string?)results.Attribute("MaxHeight"));
    }

    [Fact]
    public void Window_is_positioned_manually_and_stays_visible()
    {
        var root = XDocument.Load(FindFile("MainWindow.xaml")).Root!;
        Assert.Equal("Manual", (string?)root.Attribute("WindowStartupLocation"));
        Assert.Equal("True", (string?)root.Attribute("Topmost"));
    }
}
