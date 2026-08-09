using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint11EndToEndContractTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath + " introuvable.");
    }

    private static string Read(string relativePath) => File.ReadAllText(FindFile(relativePath));

    [Fact]
    public void File_and_folder_activation_remain_supported_and_protected()
    {
        var code = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml.cs"));

        Assert.Contains("!File.Exists(fullPath) && !Directory.Exists(fullPath)", code, StringComparison.Ordinal);
        Assert.Contains("if (Directory.Exists(path))", code, StringComparison.Ordinal);
        Assert.Contains("IsProtectedOneDriveSource(fullPath)", code, StringComparison.Ordinal);
        Assert.Contains("Dossier principal OneDrive protégé — aucun déplacement.", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Proposed_destination_and_source_item_stay_visible_while_only_tree_scrolls()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));

        Assert.Contains("DOSSIER CHOISI", xaml, StringComparison.Ordinal);
        Assert.Contains("ÉLÉMENT À DÉPLACER", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"MainContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FolderTree\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseWheel=\"OnFolderTreePreviewMouseWheel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AVANT LE DÉPLACEMENT", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Automatic_branch_guidance_keeps_the_whole_tree_manually_navigable()
    {
        var finishing = Read(Path.Combine("AtlasDrop.App", "MainWindow.Finishing.cs"));

        Assert.Contains("AllowedRootFolderNames.All(visibleRoots.Contains)", finishing, StringComparison.Ordinal);
        Assert.Contains("RebuildFullNavigableTree", finishing, StringComparison.Ordinal);
        Assert.Contains("node.IsExpanded = !string.IsNullOrWhiteSpace(_proposedFolder)", finishing, StringComparison.Ordinal);
        Assert.Contains("IsSameOrChild(_proposedFolder, folder.Path)", finishing, StringComparison.Ordinal);
        Assert.Contains("proposedNode.BringIntoView()", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_correction_returns_to_OneDrive_and_undo_is_not_exposed()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));
        var finishing = Read(Path.Combine("AtlasDrop.App", "MainWindow.Finishing.cs"));

        Assert.Contains("x:Name=\"CorrectClassificationButton\" Content=\"RETOUR ONEDRIVE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Border Visibility=\"Collapsed\" Height=\"0\">", xaml, StringComparison.Ordinal);
        Assert.Contains("UndoMoveButton.Visibility = Visibility.Collapsed", finishing, StringComparison.Ordinal);
        Assert.Contains("_undoTimer.Stop()", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_returns_to_the_original_desktop_or_Explorer_folder()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));
        var finishing = Read(Path.Combine("AtlasDrop.App", "MainWindow.Finishing.cs"));

        Assert.Contains("PreviewMouseLeftButtonDown=\"OnConfirmClassificationPreview\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Path.GetDirectoryName(_pendingMove.Source)", finishing, StringComparison.Ordinal);
        Assert.Contains("Environment.SpecialFolder.DesktopDirectory", finishing, StringComparison.Ordinal);
        Assert.Contains("ShowDesktop()", finishing, StringComparison.Ordinal);
        Assert.Contains("OpenExplorerAndTrackAsync(sourceFolder)", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Analysis_and_Explorer_opening_always_have_visible_progress_feedback()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"SearchLoadingPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Recherche du meilleur emplacement…", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SearchLoadingProgress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExplorerLoadingProgress\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_capture_has_live_level_stop_redo_and_explicit_validation()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));
        var finishing = Read(Path.Combine("AtlasDrop.App", "MainWindow.Finishing.cs"));
        var voice = Read(Path.Combine("AtlasDrop.App", "LocalVoiceExplanationService.cs"));

        Assert.Contains("x:Name=\"VoiceListeningVisual\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"VoiceLevelMeter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Écoute en cours…", xaml, StringComparison.Ordinal);
        Assert.Contains("■ TERMINER", finishing, StringComparison.Ordinal);
        Assert.Contains("Content=\"VALIDER\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"REFAIRE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AudioLevelChanged", voice, StringComparison.Ordinal);
        Assert.Contains("CalculatePeakLevel", voice, StringComparison.Ordinal);
        Assert.Contains("AudioLevelChanged?.Invoke(0f)", voice, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_is_present_before_confirmation_and_old_voice_placeholder_is_not_visible()
    {
        var xaml = Read(Path.Combine("AtlasDrop.App", "MainWindow.xaml"));

        Assert.Contains("NOM PROPOSÉ", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RenamePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExplainChoiceButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"🎤 EXPLICATION VOCALE — DISPONIBLE APRÈS CLASSEMENT\"", xaml, StringComparison.Ordinal);
    }
}
