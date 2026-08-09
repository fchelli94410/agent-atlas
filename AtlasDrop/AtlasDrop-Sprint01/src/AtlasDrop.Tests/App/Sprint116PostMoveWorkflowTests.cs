using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116PostMoveWorkflowTests
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
    public void Secondary_actions_are_hidden_until_explicit_confirmation()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("CorrectClassificationButton.Visibility = _finishingClassificationConfirmed", code, StringComparison.Ordinal);
        Assert.Contains("ExplainChoiceButton.Visibility = _finishingClassificationConfirmed", code, StringComparison.Ordinal);
        Assert.Contains("ConfirmClassificationButton.Visibility = _finishingClassificationConfirmed", code, StringComparison.Ordinal);
        Assert.Contains("Visibility.Collapsed", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_no_longer_runs_the_legacy_close_handler()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("ConfirmClassificationButton.Click -= OnClassificationConfirmed", code, StringComparison.Ordinal);
        Assert.Contains("ConfirmClassificationButton.Click += OnFinishingClassificationConfirmedClicked", code, StringComparison.Ordinal);
        Assert.Contains("_closeTimer.Stop()", code, StringComparison.Ordinal);
        Assert.Contains("_busy = false", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Return_onedrive_after_confirmation_does_not_restore_the_file()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));
        var start = code.IndexOf("private async void OnFinishingReturnOneDriveClicked", StringComparison.Ordinal);
        var end = code.IndexOf("private async Task BringExplorerImmediatelyBehindAtlasAsync", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var block = code[start..end];

        Assert.Contains("move.Destination", block, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreMove", block, StringComparison.Ordinal);
        Assert.DoesNotContain("OnClassificationRejected", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_is_foregrounded_then_atlas_is_reactivated_on_top()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("FinishingSetForegroundWindow(hwnd)", code, StringComparison.Ordinal);
        Assert.Contains("Topmost = true", code, StringComparison.Ordinal);
        Assert.Contains("Activate()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Move_preview_is_reparented_next_to_confirmation_instead_of_staying_far_below()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("MovePreviewIntoPostMovePanel", code, StringComparison.Ordinal);
        Assert.Contains("postMoveStack.Children.Insert", code, StringComparison.Ordinal);
        Assert.Contains("RestoreMovePreviewToFooter", code, StringComparison.Ordinal);
    }
}
