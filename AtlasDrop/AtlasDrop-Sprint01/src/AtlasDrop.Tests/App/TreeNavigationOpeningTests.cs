using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class TreeNavigationOpeningTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath + " introuvable.");
    }

    [Fact]
    public void Tree_click_opens_explorer_refinement_without_direct_move()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var code = File.ReadAllText(FindFile("MainWindow.TreeNavigationFix.cs"));

        Assert.Contains("PreviewMouseLeftButtonUp=\"OnFolderTreePreviewMouseLeftButtonUp\"", xaml, StringComparison.Ordinal);
        Assert.Contains("await EnterExplorerRefinementModeAsync(destination)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteMoveOnceAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_arrow_is_not_intercepted_by_opening_behavior()
    {
        var code = File.ReadAllText(FindFile("MainWindow.TreeNavigationFix.cs"));

        Assert.Contains("FindAncestor<ToggleButton>", code, StringComparison.Ordinal);
        Assert.Contains("return;", code, StringComparison.Ordinal);
    }
}
