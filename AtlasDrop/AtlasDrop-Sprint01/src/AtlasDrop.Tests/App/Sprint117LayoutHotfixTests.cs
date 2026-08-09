using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint117LayoutHotfixTests
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
    public void Folder_tree_is_before_rename_panel_and_keeps_real_height()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var suggestion = xaml.IndexOf("x:Name=\"SuggestionPanel\"", StringComparison.Ordinal);
        var tree = xaml.IndexOf("x:Name=\"FolderTree\"", StringComparison.Ordinal);
        var rename = xaml.IndexOf("x:Name=\"RenamePanel\"", StringComparison.Ordinal);

        Assert.True(suggestion >= 0);
        Assert.True(tree > suggestion);
        Assert.True(rename > tree);
        Assert.Contains("MinHeight=\"250\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainContentScrollViewer\" MinHeight=\"165\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_help_text_is_smaller_and_discreet()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("x:Name=\"AutoRenameStatusText\" Foreground=\"#7A8498\" FontSize=\"10.5\"", xaml, StringComparison.Ordinal);
    }
}
