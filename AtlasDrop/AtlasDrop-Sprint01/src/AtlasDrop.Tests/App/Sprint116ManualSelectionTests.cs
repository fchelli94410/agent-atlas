using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116ManualSelectionTests
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
    public void Clicking_a_manual_tree_destination_enables_confirmation_without_moving()
    {
        var code = File.ReadAllText(FindFile("MainWindow.ManualSelection116.cs"));

        Assert.Contains("e.Handled = true", code, StringComparison.Ordinal);
        Assert.Contains("_proposedFolder = destination", code, StringComparison.Ordinal);
        Assert.Contains("YesButton.IsEnabled = true", code, StringComparison.Ordinal);
        Assert.Contains("Clique C’EST EXACT", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteMoveOnceAsync(destination)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync(destination)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_arrow_keeps_its_normal_tree_behavior()
    {
        var code = File.ReadAllText(FindFile("MainWindow.ManualSelection116.cs"));

        Assert.Contains("FindVisualAncestor<ToggleButton>", code, StringComparison.Ordinal);
        Assert.Contains("return;", code, StringComparison.Ordinal);
    }
}
