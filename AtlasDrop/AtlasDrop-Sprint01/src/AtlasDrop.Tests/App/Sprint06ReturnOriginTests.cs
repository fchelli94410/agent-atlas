using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint06ReturnOriginTests
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
    public void Confirmation_captures_the_original_folder_before_the_move_is_finalized()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "ConfirmClassificationButton");

        Assert.Equal("OnConfirmClassificationPreview", (string?)button.Attribute("PreviewMouseLeftButtonDown"));
        Assert.Equal("OnClassificationConfirmed", (string?)button.Attribute("Click"));
    }

    [Fact]
    public void Successful_confirmation_returns_to_desktop_or_original_explorer_folder()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("Path.GetDirectoryName(_pendingMove.Source)", code, StringComparison.Ordinal);
        Assert.Contains("Environment.SpecialFolder.DesktopDirectory", code, StringComparison.Ordinal);
        Assert.Contains("ShowDesktop()", code, StringComparison.Ordinal);
        Assert.Contains("OpenExplorerAndTrackAsync(sourceFolder)", code, StringComparison.Ordinal);
        Assert.Contains("ShowWindow(hwnd, ShowWindowMaximized)", code, StringComparison.Ordinal);
        Assert.Contains("Hide();", code, StringComparison.Ordinal);
    }
}
