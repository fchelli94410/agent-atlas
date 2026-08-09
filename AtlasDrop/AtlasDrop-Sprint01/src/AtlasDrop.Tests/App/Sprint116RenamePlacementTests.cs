using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116RenamePlacementTests
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
    public void Proposed_name_is_visible_before_the_scrollable_tree()
    {
        var text = File.ReadAllText(FindMainWindow());
        var rename = text.IndexOf("x:Name=\"RenamePanel\"", StringComparison.Ordinal);
        var tree = text.IndexOf("x:Name=\"FolderTree\"", StringComparison.Ordinal);
        var moveItem = text.IndexOf("ÉLÉMENT À DÉPLACER", StringComparison.Ordinal);

        Assert.True(rename >= 0 && rename < tree && tree < moveItem);
        Assert.Contains("NOM PROPOSÉ", text, StringComparison.Ordinal);
    }
}
