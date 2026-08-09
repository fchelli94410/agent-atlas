using System.Text.RegularExpressions;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowExplorerRefinementTests
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

    private static string ReadCode()
    {
        return string.Join("\n",
            File.ReadAllText(FindFile("MainWindow.xaml.cs")),
            File.ReadAllText(FindFile("MainWindow.Finishing.cs")));
    }

    private static string MethodBlock(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0) return string.Empty;
        var endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        return endIndex < 0 ? text[startIndex..] : text[startIndex..endIndex];
    }

    [Fact]
    public void Manual_search_is_prepared_before_any_move()
    {
        var code = ReadCode();
        var block = MethodBlock(
            code,
            "private async Task EnterExplorerRefinementModeAsync",
            "private async void OnMoveHere");

        Assert.Contains("_lockedSourcePath = _activePath", block, StringComparison.Ordinal);
        Assert.Contains("MoveHereButton.IsEnabled = false", block, StringComparison.Ordinal);
        Assert.Contains("OpenExplorerAndTrackAsync(startingFolder)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteMoveOnceAsync", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_search_moves_only_the_locked_source_to_the_tracked_explorer_folder()
    {
        var code = ReadCode();
        var block = MethodBlock(
            code,
            "private async void OnMoveHere",
            "private void OnBack");

        Assert.Contains("_lockedSourcePath", block, StringComparison.Ordinal);
        Assert.Contains("TryGetExplorerPathByHwnd", block, StringComparison.Ordinal);
        Assert.Contains("await ExecuteMoveOnceAsync(destination)", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Back_restores_the_suggestion_without_moving()
    {
        var code = ReadCode();
        var block = MethodBlock(
            code,
            "private void OnBack",
            "private void OnWindowPreviewKeyDown");

        Assert.Contains("ExplorerRefinementPanel.Visibility = Visibility.Collapsed", block, StringComparison.Ordinal);
        Assert.Contains("SuggestionPanel.Visibility = Visibility.Visible", block, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteMoveOnceAsync", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Root_and_first_level_are_visible_in_manual_tree()
    {
        var code = ReadCode();
        var block = MethodBlock(
            code,
            "private void BuildFolderDecisionTree",
            "private string? GetBranchPath");

        Assert.Contains("FolderTree.Items.Clear()", block, StringComparison.Ordinal);
        Assert.Contains("OneDrive", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Proposed_folder_is_brought_into_view()
    {
        var code = ReadCode();
        var block = MethodBlock(
            code,
            "private void BuildFolderDecisionTree",
            "private string? GetBranchPath");

        Assert.Contains("proposedNode.BringIntoView()", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Normal_window_uses_most_of_the_monitor_height_for_breathable_spacing()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var positioning = MethodBlock(
            code,
            "private void PositionTopRight",
            "private static MonitorPlacement GetCursorMonitorPlacement");

        Assert.Contains("workHeightDip * .94", positioning, StringComparison.Ordinal);
        Assert.Contains("Math.Max(680", positioning, StringComparison.Ordinal);
        Assert.Contains("<Grid Margin=\"18,14,18,8\">", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Analysis_runs_off_the_ui_thread_and_prefilters_large_indexes()
    {
        var code = ReadCode();

        Assert.Contains("await Task.Run(() => AnalyzeItemAsync(path))", code, StringComparison.Ordinal);
        Assert.Contains("await Task.Run(() => BuildSuggestions(_analysis))", code, StringComparison.Ordinal);
        Assert.Contains("GetRelevantFolderCandidates(analysis)", code, StringComparison.Ordinal);
        Assert.Contains("_folders.Count <= 250", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Selected_folder_is_focused_and_its_summary_stays_pinned()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("proposedNode.BringIntoView()", code, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SelectedDestinationPanel\" Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"#ECFDF3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Hidden\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_correction_mode_hides_irrelevant_controls_and_keeps_only_essential_actions()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var refinement = MethodBlock(
            code,
            "private async Task EnterExplorerRefinementModeAsync",
            "private async void OnMoveHere");

        Assert.Contains("x:Name=\"SelectedDestinationPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedDestinationPanel.Visibility = Visibility.Collapsed", refinement, StringComparison.Ordinal);
        Assert.Contains("ExplainChoiceButton.Visibility = Visibility.Collapsed", refinement, StringComparison.Ordinal);
        Assert.Contains("LearningControlsPanel.Visibility = Visibility.Collapsed", refinement, StringComparison.Ordinal);
        Assert.Contains("Math.Min(maxHeightDip, 430)", code, StringComparison.Ordinal);
    }
}
