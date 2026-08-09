using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint03FixedLayoutTests
{
    private static string FindMainWindow()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", "MainWindow.xaml");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml introuvable.");
    }

    [Fact]
    public void Only_the_folder_tree_is_inside_the_main_scroll_area()
    {
        var document = XDocument.Load(FindMainWindow());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var scroll = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "MainContentScrollViewer");
        var tree = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "FolderTree");
        var movePreview = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "MovePreviewPanel");
        var decisionButtons = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "DecisionButtons");

        Assert.Contains(tree.Ancestors(), element => ReferenceEquals(element, scroll));
        Assert.DoesNotContain(movePreview.Ancestors(), element => ReferenceEquals(element, scroll));
        Assert.DoesNotContain(decisionButtons.Ancestors(), element => ReferenceEquals(element, scroll));
    }

    [Fact]
    public void Move_preview_shows_only_the_item_name_to_the_user()
    {
        var xaml = File.ReadAllText(FindMainWindow());

        Assert.Contains("ÉLÉMENT À DÉPLACER", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Text, ElementName=ItemNameText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CurrentMovePreviewText\" Visibility=\"Collapsed\"", xaml, StringComparison.Ordinal);
    }
}
