using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint05NavigationButtonsTests
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
    public void Back_is_fixed_at_the_bottom_left_and_manual_correction_returns_to_OneDrive()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var status = xaml.IndexOf("x:Name=\"StatusText\"", StringComparison.Ordinal);
        var back = xaml.IndexOf("x:Name=\"BackButton\"", StringComparison.Ordinal);
        var item = xaml.IndexOf("x:Name=\"ItemNameText\"", StringComparison.Ordinal);

        Assert.True(item >= 0 && status > item && back > status);
        Assert.Contains("x:Name=\"BackButton\" Content=\"←  RETOUR\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Left\"", xaml[back..], StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CorrectClassificationButton\" Content=\"RETOUR ONEDRIVE\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Temporary_undo_is_not_exposed_to_the_user()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var undo = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "UndoMoveButton");
        var hiddenContainer = undo.Ancestors().FirstOrDefault(
            element => (string?)element.Attribute("Visibility") == "Collapsed");
        var finishing = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.NotNull(hiddenContainer);
        Assert.Contains("_undoTimer.Stop()", finishing, StringComparison.Ordinal);
        Assert.Contains("UndoMoveButton.Visibility = Visibility.Collapsed", finishing, StringComparison.Ordinal);
    }
}
