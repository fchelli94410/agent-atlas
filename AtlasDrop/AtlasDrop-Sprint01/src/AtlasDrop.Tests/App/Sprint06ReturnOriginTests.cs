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
    public void Confirmation_is_rewired_to_the_116_post_move_workflow()
    {
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "ConfirmClassificationButton");
        var finishing = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Equal("OnConfirmClassificationPreview", (string?)button.Attribute("PreviewMouseLeftButtonDown"));
        Assert.Contains("ConfirmClassificationButton.Click -= OnClassificationConfirmed", finishing, StringComparison.Ordinal);
        Assert.Contains("ConfirmClassificationButton.Click += OnFinishingClassificationConfirmedClicked", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Return_onedrive_is_only_available_after_confirmation_and_targets_destination()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("CorrectClassificationButton.Visibility = _finishingClassificationConfirmed", code, StringComparison.Ordinal);
        Assert.Contains("var move = _finishingConfirmedMove ?? _pendingMove", code, StringComparison.Ordinal);
        Assert.Contains("BringExplorerImmediatelyBehindAtlasAsync(move.Destination)", code, StringComparison.Ordinal);
        Assert.Contains("FinishingSetForegroundWindow(hwnd)", code, StringComparison.Ordinal);
        Assert.Contains("Topmost = true", code, StringComparison.Ordinal);
    }
}
