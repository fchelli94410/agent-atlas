using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowExplorerRefinementTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                relativePath);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    private static string ReadCode() =>
        File.ReadAllText(FindFile("MainWindow.xaml.cs"));

    private static string MethodBlock(string code, string start, string next)
    {
        var startIndex = code.IndexOf(start, StringComparison.Ordinal);
        var endIndex = code.IndexOf(next, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Début de méthode introuvable : {start}");
        Assert.True(endIndex > startIndex, $"Fin de méthode introuvable : {next}");
        return code[startIndex..endIndex];
    }

    [Fact]
    public void Obsolete_manual_tree_and_flat_search_menu_are_absent()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.DoesNotContain("ManualPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchResultsList", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchTextBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderTree", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFolderButton", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C’EST EXACT")]
    [InlineData("BONNE BRANCHE")]
    [InlineData("MAUVAIS DOSSIER")]
    [InlineData("ANNULER")]
    [InlineData("DÉPOSER ICI")]
    public void Decision_labels_match_the_validated_workflow(string label)
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        Assert.Contains(label, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Good_branch_opens_the_proposal_and_wrong_folder_opens_onedrive_root()
    {
        var code = ReadCode();
        var goodBranch = MethodBlock(
            code,
            "private async void OnNo",
            "private async void OnChoose");
        var wrongFolder = MethodBlock(
            code,
            "private async void OnChoose",
            "private async Task EnterExplorerRefinementModeAsync");

        Assert.Contains(
            "await EnterExplorerRefinementModeAsync(_proposedFolder)",
            goodBranch,
            StringComparison.Ordinal);
        Assert.Contains(
            "await EnterExplorerRefinementModeAsync(_oneDriveRoot)",
            wrongFolder,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync", goodBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync", wrongFolder, StringComparison.Ordinal);
    }

    [Fact]
    public void Proposed_folder_is_clickable_and_opens_without_moving()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var click = MethodBlock(
            code,
            "private async void OnProposedPathClicked",
            "private void PrepareRename");

        Assert.Contains("MouseLeftButtonUp=\"OnProposedPathClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Cursor=\"Hand\"", xaml, StringComparison.Ordinal);
        Assert.Contains("await EnterExplorerRefinementModeAsync(_proposedFolder)", click, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync", click, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_refinement_does_not_move_before_deposit_here()
    {
        var code = ReadCode();
        var refinement = MethodBlock(
            code,
            "private async Task EnterExplorerRefinementModeAsync",
            "private async void OnMoveHere");
        var deposit = MethodBlock(
            code,
            "private async void OnMoveHere",
            "private async Task ExecuteMoveOnceAsync");

        Assert.Contains("aucun déplacement effectué", refinement, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync", refinement, StringComparison.Ordinal);
        Assert.Contains("TryGetExplorerPathByHwnd", deposit, StringComparison.Ordinal);
        Assert.Contains("ExecuteMoveOnceAsync(destination)", deposit, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_window_is_tracked_by_hwnd_not_foreground_window()
    {
        var code = ReadCode();

        Assert.Contains("_trackedExplorerHwnd", code, StringComparison.Ordinal);
        Assert.Contains("var before = ReadExplorerWindows()", code, StringComparison.Ordinal);
        Assert.Contains("item.Hwnd == trackedHwnd", code, StringComparison.Ordinal);
        Assert.Contains("TryGetExplorerPathByHwnd", code, StringComparison.Ordinal);
        Assert.DoesNotContain("GetForegroundWindow", code, StringComparison.Ordinal);
    }

    [Fact]
    public void OneDrive_root_is_level_zero_and_destination_stops_at_level_four()
    {
        var code = ReadCode();

        Assert.Contains("private const int MaxDepth = 4;", code, StringComparison.Ordinal);
        Assert.Contains("queue.Enqueue((root, 0));", code, StringComparison.Ordinal);
        Assert.Contains("var depth = current.Depth + 1;", code, StringComparison.Ordinal);
        Assert.Contains("depth > MaxDepth", code, StringComparison.Ordinal);
        Assert.Contains("destinationDepth > MaxDepth", code, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Math.Max(_options.MaxSuggestedDepth, 12)",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Main_OneDrive_folders_are_protected_but_root_files_are_not()
    {
        var code = ReadCode();
        var protection = MethodBlock(
            code,
            "private bool IsProtectedOneDriveSource",
            "private static bool IsGenericFolder");

        Assert.Contains(
            "Dossier principal OneDrive protégé — aucun déplacement.",
            code,
            StringComparison.Ordinal);
        Assert.Contains("if (!Directory.Exists(path)", protection, StringComparison.Ordinal);
        Assert.Contains("GetDepth(_oneDriveRoot, path) is 0 or 1", protection, StringComparison.Ordinal);
    }

    [Fact]
    public void Learning_is_weighted_deferred_and_normalized()
    {
        var code = ReadCode();
        var rejected = MethodBlock(
            code,
            "private async void OnClassificationRejected",
            "private static string RestoreMove");

        Assert.Contains("WeakPositiveLearningWeight = 1", code, StringComparison.Ordinal);
        Assert.Contains("StrongPositiveLearningWeight = 3", code, StringComparison.Ordinal);
        Assert.Contains("NegativeLearningWeight = -3", code, StringComparison.Ordinal);
        Assert.Contains("learnedValues.Average()", code, StringComparison.Ordinal);
        Assert.Contains("learned * 0.03", code, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(score + signal.Value, -20, 50)", code, StringComparison.Ordinal);
        Assert.Contains("IsSameOrDescendant(_initialSuggestedFolder, finalDestination)", code, StringComparison.Ordinal);
        Assert.Equal(
            2,
            code.Split("ApplyConfirmedLearning(", StringSplitOptions.None).Length - 1);
        Assert.Contains("_rejectedDestinations.Add(move.Destination)", rejected, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordLearningBatch", rejected, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyConfirmedLearning", rejected, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_keeps_a_visible_right_gap_on_the_active_monitor()
    {
        var code = ReadCode();
        var positioning = MethodBlock(
            code,
            "private void PositionTopRight",
            "private static MonitorPlacement GetCursorMonitorPlacement");

        Assert.Contains("? 110d : 12d", positioning, StringComparison.Ordinal);
        Assert.Contains("area.Right - rightGapPixels - widthPixels", positioning, StringComparison.Ordinal);
        Assert.Contains("MonitorFromPoint", code, StringComparison.Ordinal);
        Assert.Contains("info.WorkArea", code, StringComparison.Ordinal);
        Assert.Contains("new WindowInteropHelper(this).Handle", positioning, StringComparison.Ordinal);
    }
    [Fact]
    public void Automatic_search_is_restricted_to_the_five_authorized_root_folders()
    {
        var code = ReadCode();

        Assert.Contains("01 - Immobilier", code, StringComparison.Ordinal);
        Assert.Contains("02 - Activités Professionnelles", code, StringComparison.Ordinal);
        Assert.Contains("03 - Finances personnelles", code, StringComparison.Ordinal);
        Assert.Contains("04 - Quotidien", code, StringComparison.Ordinal);
        Assert.Contains("05 - Projets", code, StringComparison.Ordinal);
        Assert.Contains(
            "current.Depth == 0 && !AllowedRootFolderNames.Contains(Path.GetFileName(child))",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_destination_can_use_any_OneDrive_branch()
    {
        var code = ReadCode();

        Assert.DoesNotContain("IsAllowedDestination", code, StringComparison.Ordinal);
        Assert.Contains("!IsUnderRoot(destination)", code, StringComparison.Ordinal);
        Assert.Contains("destinationDepth < 0", code, StringComparison.Ordinal);
        Assert.Contains("folder-index-v112.json", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Health_documents_receive_a_health_folder_boost()
    {
        var code = ReadCode();

        Assert.Contains("ExpandBusinessTokens(tokens)", code, StringComparison.Ordinal);
        Assert.Contains("\"ordonnance\"", code, StringComparison.Ordinal);
        Assert.Contains("\"biologie\"", code, StringComparison.Ordinal);
        Assert.Contains("GetThematicBoost(analysis.Tokens, folder.Tokens)", code, StringComparison.Ordinal);
        Assert.Contains("health && healthFolder ? 0.25d : 0d", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmed_learning_generalizes_to_parent_folders_only_after_validation()
    {
        var code = ReadCode();
        var apply = MethodBlock(
            code,
            "private void ApplyConfirmedLearning",
            "private void RecordLearningBatch");
        var rejected = MethodBlock(
            code,
            "private async void OnClassificationRejected",
            "private static string RestoreMove");

        Assert.Contains("GetLearningAncestors(finalDestination)", apply, StringComparison.Ordinal);
        Assert.Contains("WeakPositiveLearningWeight", apply, StringComparison.Ordinal);
        Assert.Contains("!SamePath(current.FullName, _oneDriveRoot)", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordLearningBatch", rejected, StringComparison.Ordinal);
    }

}
