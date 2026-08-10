using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116UiTests
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
    public void Header_no_longer_duplicates_the_active_item_name()
    {
        var document = XDocument.Load(FindMainWindow());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var itemName = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == "ItemNameText");

        Assert.Equal("Collapsed", (string?)itemName.Attribute("Visibility"));
        Assert.Contains("ÉLÉMENT À DÉPLACER", File.ReadAllText(FindMainWindow()), StringComparison.Ordinal);
    }

    [Fact]
    public void Relaunch_action_is_in_the_global_header_before_destination_and_tree()
    {
        var text = File.ReadAllText(FindMainWindow());
        var relaunch = text.IndexOf("x:Name=\"RefreshSuggestionButton\"", StringComparison.Ordinal);
        var destination = text.IndexOf("x:Name=\"SelectedDestinationPanel\"", StringComparison.Ordinal);
        var tree = text.IndexOf("x:Name=\"FolderTree\"", StringComparison.Ordinal);

        Assert.True(relaunch >= 0 && relaunch < destination && destination < tree);
    }

    [Fact]
    public void Confidence_information_is_visually_separated_from_the_tree()
    {
        var text = File.ReadAllText(FindMainWindow());
        var info = text.IndexOf("x:Name=\"ConfidenceInfoPanel\"", StringComparison.Ordinal);
        var tree = text.IndexOf("x:Name=\"FolderTree\"", StringComparison.Ordinal);

        Assert.True(info >= 0 && info < tree);
        Assert.Contains("BorderThickness=\"0,0,0,1\"", text, StringComparison.Ordinal);
        Assert.Contains("Background=\"#F8FAFC\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_exact_button_remains_readable()
    {
        var text = File.ReadAllText(FindMainWindow());

        Assert.Contains("x:Key=\"PrimaryDecisionButtonStyle\"", text, StringComparison.Ordinal);
        Assert.Contains("<Trigger Property=\"IsEnabled\" Value=\"False\">", text, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"#E5E7EB\"", text, StringComparison.Ordinal);
        Assert.Contains("Foreground\" Value=\"#4B5563\"", text, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource PrimaryDecisionButtonStyle}\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Back_button_is_the_last_interactive_control_in_the_footer()
    {
        var text = File.ReadAllText(FindMainWindow());
        var learningStatus = text.IndexOf("x:Name=\"LearningStatusText\"", StringComparison.Ordinal);
        var status = text.IndexOf("x:Name=\"StatusText\"", StringComparison.Ordinal);
        var back = text.IndexOf("x:Name=\"BackButton\"", StringComparison.Ordinal);

        Assert.True(learningStatus >= 0 && learningStatus < status && status < back);
        Assert.Contains("HorizontalAlignment=\"Left\"", text[back..], StringComparison.Ordinal);
    }
}
