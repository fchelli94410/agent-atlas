using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint121LayoutRegressionTests
{
    private static string ReadMainWindowXaml()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", "MainWindow.xaml");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml");
    }

    [Fact]
    public void Main_window_is_taller_and_tree_has_at_least_thirty_three_percent_more_height()
    {
        var xaml = ReadMainWindowXaml();
        Assert.Contains("Height=\"820\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainContentScrollViewer\" Grid.Row=\"2\" MinHeight=\"300\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Proposed_folder_block_is_compact_and_rename_panel_has_complete_stronger_frame()
    {
        var xaml = ReadMainWindowXaml();
        Assert.Contains("Padding=\"5,2,5,3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RenamePanel\" Grid.Row=\"1\" Background=\"White\" BorderBrush=\"#64748B\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"1.75\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Idle_validation_instruction_is_hidden_by_its_container()
    {
        var xaml = ReadMainWindowXaml();
        Assert.Contains("Value=\"Valide explicitement avant tout déplacement.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Learning_controls_are_last_and_bottom_aligned()
    {
        var xaml = ReadMainWindowXaml();
        var learning = xaml.IndexOf("x:Name=\"LearningControlsPanel\"", StringComparison.Ordinal);
        var status = xaml.IndexOf("x:Name=\"StatusText\"", StringComparison.Ordinal);
        var decisions = xaml.IndexOf("x:Name=\"DecisionButtons\"", StringComparison.Ordinal);

        Assert.True(learning > status);
        Assert.True(learning > decisions);
        Assert.Contains("x:Name=\"LearningControlsPanel\" Margin=\"0,1,0,0\" VerticalAlignment=\"Bottom\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Footer_spacing_is_compact()
    {
        var xaml = ReadMainWindowXaml();
        Assert.Contains("<StackPanel Grid.Row=\"3\" Margin=\"0,1,0,0\" VerticalAlignment=\"Bottom\">", xaml, StringComparison.Ordinal);
        Assert.Contains("DecisionButtons\" Columns=\"2\" Margin=\"-3,0,-3,1\"", xaml, StringComparison.Ordinal);
    }
}
