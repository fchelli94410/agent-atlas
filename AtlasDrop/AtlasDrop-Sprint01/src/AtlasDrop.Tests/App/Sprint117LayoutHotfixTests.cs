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
    public void Folder_tree_is_before_rename_panel_and_is_one_third_taller()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var suggestion = xaml.IndexOf("x:Name=\"SuggestionPanel\"", StringComparison.Ordinal);
        var tree = xaml.IndexOf("x:Name=\"FolderTree\"", StringComparison.Ordinal);
        var rename = xaml.IndexOf("x:Name=\"RenamePanel\"", StringComparison.Ordinal);

        Assert.True(suggestion >= 0);
        Assert.True(tree > suggestion);
        Assert.True(rename > tree);
        Assert.Contains("MinHeight=\"390\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"300\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Confidence_header_and_rename_help_are_compact()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("Padding=\"5,2,5,3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ConfidenceLevelText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"9\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AutoRenameStatusText\" Foreground=\"#7A8498\" FontSize=\"9.5\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_panel_has_a_visible_frame_and_learning_controls_are_last()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var rename = xaml.IndexOf("x:Name=\"RenamePanel\"", StringComparison.Ordinal);
        var back = xaml.IndexOf("x:Name=\"BackButton\"", StringComparison.Ordinal);
        var learning = xaml.IndexOf("x:Name=\"LearningControlsPanel\"", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"RenamePanel\" Grid.Row=\"1\" Background=\"White\" BorderBrush=\"#64748B\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"1.75\"", xaml, StringComparison.Ordinal);
        Assert.True(learning > back);
        Assert.Contains("Value=\"Valide explicitement avant tout déplacement.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", xaml, StringComparison.Ordinal);
    }
}
