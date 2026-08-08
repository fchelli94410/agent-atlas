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
    public void Main_window_xaml_is_valid_xml() =>
        Assert.Equal("Window", XDocument.Load(FindFile("MainWindow.xaml")).Root!.Name.LocalName);

    [Theory]
    [InlineData("YesButton")]
    [InlineData("NoButton")]
    [InlineData("ChooseButton")]
    [InlineData("CancelButton")]
    [InlineData("MoveHereButton")]
    [InlineData("ProposedPathText")]
    [InlineData("ExplorerRefinementPanel")]
    [InlineData("TrackedExplorerDestinationText")]
    [InlineData("StatusText")]
    public void Validated_workflow_controls_exist(string controlName)
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Contains(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == controlName);
    }

    [Fact]
    public void Deposit_button_is_hidden_and_disabled_until_an_explorer_is_tracked()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "MoveHereButton");

        Assert.Equal("DÉPOSER ICI", (string?)button.Attribute("Content"));
        Assert.Equal("Collapsed", (string?)button.Attribute("Visibility"));
        Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
        Assert.Equal("OnMoveHere", (string?)button.Attribute("Click"));
    }

    [Fact]
    public void Explorer_refinement_instructions_are_visible_in_the_dedicated_panel()
    {
        var text = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("Affiner dans l’Explorateur", text, StringComparison.Ordinal);
        Assert.Contains("Navigue dans la fenêtre Explorateur ouverte par Atlas Drop", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchResultsList", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderTree", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_is_positioned_manually_and_stays_visible()
    {
        var root = XDocument.Load(FindFile("MainWindow.xaml")).Root!;
        Assert.Equal("Manual", (string?)root.Attribute("WindowStartupLocation"));
        Assert.Equal("True", (string?)root.Attribute("Topmost"));
    }
}
