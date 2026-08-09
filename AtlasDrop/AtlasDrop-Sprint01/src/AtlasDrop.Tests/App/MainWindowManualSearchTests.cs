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
    public void Direct_folder_tree_replaces_the_old_manual_search_menu()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.DoesNotContain("ManualPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchResultsList", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchTextBox", xaml, StringComparison.Ordinal);
        Assert.Contains("FolderTree", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFolderButton", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C’EST EXACT")]
    [InlineData("ANNULER")]
    [InlineData("DÉPOSER DANS CE DOSSIER")]
    public void Decision_labels_match_the_validated_workflow(string label)
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        Assert.Contains(label, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Tree_click_moves_to_a_folder_but_onedrive_root_only_opens_explorer()
    {
        var code = ReadCode();
        var click = MethodBlock(
            code,
            "private async void OnFolderTreeNodeClicked",
            "private async Task OpenDestinationBehindAsync");

        Assert.Contains("PathsEqualSafe(destination, _oneDriveRoot)", click, StringComparison.Ordinal);
        Assert.Contains("await EnterExplorerRefinementModeAsync(_oneDriveRoot)", click, StringComparison.Ordinal);
        Assert.Contains("await ExecuteMoveOnceAsync(destination)", click, StringComparison.Ordinal);
    }

    [Fact]
    public void The_selected_branch_tree_is_built_from_the_cached_folder_index()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var tree = MethodBlock(
            code,
            "private void BuildFolderDecisionTree",
            "private string? GetBranchPath");

        Assert.Contains("x:Name=\"FolderTree\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsSameOrChild(folder.Path, branchPath)", tree, StringComparison.Ordinal);
        Assert.Contains("rootItem.IsExpanded = true", tree, StringComparison.Ordinal);
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

        Assert.Contains("? 150d : 12d", positioning, StringComparison.Ordinal);
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

    [Fact]
    public void Fiscal_documents_are_boosted_toward_finances_and_taxes()
    {
        var code = ReadCode();

        Assert.Contains("\"revenus\"", code, StringComparison.Ordinal);
        Assert.Contains("\"impôts\"", code, StringComparison.Ordinal);
        Assert.Contains("\"finances\"", code, StringComparison.Ordinal);
        Assert.Contains("fiscal && fiscalFolder", code, StringComparison.Ordinal);
        Assert.Contains("return 0.32d", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_is_maximized_and_destination_is_refreshed_live()
    {
        var code = ReadCode();

        Assert.Contains("ShowWindow(explorerHwnd, ShowWindowMaximized)", code, StringComparison.Ordinal);
        Assert.Contains("_explorerPathTimer.Start()", code, StringComparison.Ordinal);
        Assert.Contains("OnExplorerPathTimerTick", code, StringComparison.Ordinal);
        Assert.Contains("Destination prête", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_editing_enables_apply_and_learning_can_be_partially_deleted()
    {
        var code = ReadCode();

        Assert.Contains("AutoRenameCheckBox.IsChecked = false", code, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoRenameCheckBox.IsChecked = true", code, StringComparison.Ordinal);
        Assert.Contains("OnManageLearningClicked", code, StringComparison.Ordinal);
        Assert.Contains("SUPPRIMER LA SÉLECTION", code, StringComparison.Ordinal);
        Assert.Contains("SaveLearningDictionary()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_explanations_are_local_and_require_explicit_confirmation()
    {
        var code = ReadCode();
        var voice = File.ReadAllText(FindFile("LocalVoiceExplanationService.cs"));
        var project = File.ReadAllText(FindFile("AtlasDrop.App.csproj"));

        Assert.Contains("WhisperFactory.FromPath", voice, StringComparison.Ordinal);
        Assert.Contains("WithLanguage(\"fr\")", voice, StringComparison.Ordinal);
        Assert.Contains("WhisperGgmlDownloader", voice, StringComparison.Ordinal);
        Assert.Contains("OnConfirmVoiceRuleClicked", code, StringComparison.Ordinal);
        Assert.Contains("Voici ce qu’Atlas Drop a compris", code, StringComparison.Ordinal);
        Assert.Contains("Whisper.net.Runtime", project, StringComparison.Ordinal);
        Assert.DoesNotContain("api.openai.com", voice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_the_selected_destination_path_is_expanded_and_highlighted()
    {
        var code = ReadCode();
        var tree = MethodBlock(
            code,
            "private void BuildFolderDecisionTree",
            "private string? GetBranchPath");
        var item = MethodBlock(
            code,
            "private TreeViewItem NewTreeItem",
            "private async void OnFolderTreeNodeClicked");

        Assert.Contains("IsSameOrChild(proposedFolder, folder.Path)", tree, StringComparison.Ordinal);
        Assert.DoesNotContain("node.IsExpanded = true", tree, StringComparison.Ordinal);
        Assert.Contains("var isProposed = PathsEqualSafe(path, _proposedFolder)", item, StringComparison.Ordinal);
        Assert.Contains("CornerRadius = new CornerRadius(5)", item, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_learning_is_always_visible_but_enabled_only_after_a_move()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("EXPLICATION VOCALE — DISPONIBLE APRÈS CLASSEMENT", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExplainChoiceButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ExplainChoiceButton.IsEnabled = true", code, StringComparison.Ordinal);
        Assert.Contains("ExplainChoiceButton.IsEnabled = false", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_rebuilds_the_index_and_recalculates_the_suggestion_without_moving()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var refresh = MethodBlock(
            code,
            "private async void OnRefreshSuggestionClicked",
            "private async Task AnalyzeActiveFileAsync");

        Assert.Contains("x:Name=\"RefreshSuggestionButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("_folders = await Task.Run(BuildIndex)", refresh, StringComparison.Ordinal);
        Assert.Contains("_suggestions = BuildSuggestions(_analysis)", refresh, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveAsync", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void Closing_keeps_the_moved_file_but_skips_unconfirmed_learning()
    {
        var code = ReadCode();

        Assert.Contains("CLOSED_WITHOUT_CONFIRMATION", code, StringComparison.Ordinal);
        Assert.Contains("_pendingMove = null", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Réponds à la question de conformité avant de fermer.", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Direct_tree_move_waits_for_maximized_explorer_and_shows_confirmation()
    {
        var code = ReadCode();

        Assert.Contains("await OpenDestinationBehindAsync(destination)", code, StringComparison.Ordinal);
        Assert.Contains("PostMovePanel.BringIntoView()", code, StringComparison.Ordinal);
        Assert.Contains("MainContentScrollViewer.ScrollToEnd()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Mouse_wheel_over_the_tree_scrolls_the_single_main_page()
    {
        var code = ReadCode();
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("PreviewMouseWheel=\"OnFolderTreePreviewMouseWheel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MainContentScrollViewer.ScrollToVerticalOffset", code, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Back_from_confirmation_restores_the_file_without_learning()
    {
        var code = ReadCode();
        var back = MethodBlock(
            code,
            "private async void OnBack",
            "private void OnWindowPreviewKeyDown");

        Assert.Contains("RestoreMove(move)", back, StringComparison.Ordinal);
        Assert.Contains("BACK_AND_RESTORED", back, StringComparison.Ordinal);
        Assert.Contains("_pendingMove = null", back, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyConfirmedLearning", back, StringComparison.Ordinal);
        Assert.Contains("BackButton.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_is_forced_to_the_full_work_area_before_Atlas_returns_on_top()
    {
        var code = ReadCode();
        var positioning = MethodBlock(
            code,
            "private static void PositionExplorerWindow",
            "private static void ReleaseComObject");

        Assert.Contains("ShowWindow(hwnd, ShowWindowRestore)", positioning, StringComparison.Ordinal);
        Assert.Contains("area.Right - area.Left", positioning, StringComparison.Ordinal);
        Assert.Contains("area.Bottom - area.Top", positioning, StringComparison.Ordinal);
        Assert.Contains("ShowWindow(hwnd, ShowWindowMaximized)", positioning, StringComparison.Ordinal);
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
        Assert.Contains("<Grid Margin=\"18,14,18,16\">", xaml, StringComparison.Ordinal);
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
        Assert.Contains("Grid.Row=\"1\" Background=\"#ECFDF3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MainContentScrollViewer\" Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
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

    [Fact]
    public void Deposit_button_is_pinned_outside_the_scrolling_content()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var scrollEnd = xaml.IndexOf("</ScrollViewer>", StringComparison.Ordinal);
        var deposit = xaml.IndexOf("x:Name=\"MoveHereButton\"", StringComparison.Ordinal);

        Assert.True(scrollEnd >= 0);
        Assert.True(deposit > scrollEnd);
        Assert.Contains("Grid.Row=\"3\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_button_always_shows_immediate_feedback()
    {
        var code = ReadCode();
        var voice = MethodBlock(
            code,
            "private async void OnExplainChoiceClicked",
            "private void OnConfirmVoiceRuleClicked");

        Assert.Contains("VoiceRulePanel.Visibility = Visibility.Visible", voice, StringComparison.Ordinal);
        Assert.Contains("Activation du microphone…", voice, StringComparison.Ordinal);
        Assert.Contains("await Dispatcher.Yield(DispatcherPriority.Render)", voice, StringComparison.Ordinal);
        Assert.Contains("Aucun déplacement en attente", voice, StringComparison.Ordinal);
        Assert.Contains("Microphone indisponible", voice, StringComparison.Ordinal);
    }

}
