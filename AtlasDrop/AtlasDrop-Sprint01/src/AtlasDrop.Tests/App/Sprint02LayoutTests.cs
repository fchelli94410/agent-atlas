using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint02LayoutTests
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
    public void Rename_proposal_is_fixed_and_voice_is_only_available_after_move()
    {
        var path = FindMainWindow();
        var document = XDocument.Load(path);
        var xaml = File.ReadAllText(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var renamePanel = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "RenamePanel");
        var voiceButton = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "ExplainChoiceButton");

        Assert.Contains("NOM PROPOSÉ", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            renamePanel.Ancestors(),
            element => (string?)element.Attribute(x + "Name") == "MainContentScrollViewer");
        Assert.Contains(
            voiceButton.Ancestors(),
            element => (string?)element.Attribute(x + "Name") == "PostMovePanel");
        Assert.Equal("🎤 EXPLIQUER MON CHOIX", (string?)voiceButton.Attribute("Content"));
    }
}
