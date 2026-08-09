using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private bool _finishingTreeRefreshInProgress;
    private bool _voiceLevelSubscribed;
    private string? _returnSourceFolder;

    private void OnFinishingWindowLayoutUpdated(object? sender, EventArgs e)
    {
        if (_undoTimer.IsEnabled)
            _undoTimer.Stop();

        if (UndoMoveButton.Visibility != Visibility.Collapsed)
            UndoMoveButton.Visibility = Visibility.Collapsed;

        EnsureVoiceLevelSubscription();
        var recording = _voiceService?.IsRecording == true;
        VoiceListeningVisual.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        if (recording)
        {
            if (!string.Equals(ExplainChoiceButton.Content?.ToString(), "■ TERMINER", StringComparison.Ordinal))
                ExplainChoiceButton.Content = "■ TERMINER";
        }
        else
        {
            VoiceLevelMeter.Value = 0d;
        }
    }

    private void EnsureVoiceLevelSubscription()
    {
        if (_voiceLevelSubscribed || _voiceService is null)
            return;

        _voiceService.AudioLevelChanged += OnVoiceLevelChanged;
        _voiceLevelSubscribed = true;
    }

    private void OnVoiceLevelChanged(float level)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() => VoiceLevelMeter.Value = Math.Clamp(level, 0f, 1f)));
    }

    private void OnConfirmClassificationPreview(object sender, MouseButtonEventArgs e)
    {
        if (_pendingMove is null)
            return;

        _returnSourceFolder = Path.GetDirectoryName(_pendingMove.Source);
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => _ = ReturnToOriginalLocationAsync()));
    }

    private async Task ReturnToOriginalLocationAsync()
    {
        var sourceFolder = _returnSourceFolder;
        _returnSourceFolder = null;

        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return;

        Hide();

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop) && SamePath(sourceFolder, desktop))
        {
            ShowDesktop();
            return;
        }

        var explorer = await OpenExplorerAndTrackAsync(sourceFolder);
        if (explorer is nint hwnd)
            ShowWindow(hwnd, ShowWindowMaximized);
    }

    private static void ShowDesktop()
    {
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return;

            shell = Activator.CreateInstance(shellType);
            if (shell is not null)
                ((dynamic)shell).ToggleDesktop();
        }
        catch
        {
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
            {
                try { Marshal.FinalReleaseComObject(shell); } catch { }
            }
        }
    }

    private void OnFolderTreeLayoutUpdated(object? sender, EventArgs e)
    {
        if (_finishingTreeRefreshInProgress || FolderTree.Items.Count == 0)
            return;

        if (AllRootBranchesAreVisible())
            return;

        RebuildFullNavigableTree();
    }

    private bool AllRootBranchesAreVisible()
    {
        if (FolderTree.Items.Count == 0 || FolderTree.Items[0] is not TreeViewItem rootItem)
            return false;

        var visibleRoots = rootItem.Items
            .OfType<TreeViewItem>()
            .Select(item => item.Tag as string)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileName(path!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AllowedRootFolderNames.All(visibleRoots.Contains);
    }

    private void RebuildFullNavigableTree()
    {
        _finishingTreeRefreshInProgress = true;
        try
        {
            FolderTree.Items.Clear();

            var rootItem = NewTreeItem(_oneDriveRoot, "☁  OneDrive");
            rootItem.IsExpanded = true;
            FolderTree.Items.Add(rootItem);

            var nodes = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase)
            {
                [_oneDriveRoot] = rootItem
            };

            TreeViewItem? proposedNode = null;
            foreach (var folder in _folders
                         .OrderBy(folder => folder.Depth)
                         .ThenBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase))
            {
                var parentPath = Path.GetDirectoryName(folder.Path) ?? _oneDriveRoot;
                if (!nodes.TryGetValue(parentPath, out var parent))
                    parent = rootItem;

                var node = NewTreeItem(folder.Path);
                node.IsExpanded = !string.IsNullOrWhiteSpace(_proposedFolder)
                                  && IsSameOrChild(_proposedFolder, folder.Path);
                parent.Items.Add(node);
                nodes[folder.Path] = node;

                if (PathsEqualSafe(folder.Path, _proposedFolder))
                    proposedNode = node;
            }

            if (proposedNode is not null)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    () => proposedNode.BringIntoView());
            }
        }
        finally
        {
            _finishingTreeRefreshInProgress = false;
        }
    }
}
