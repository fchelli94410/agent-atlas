using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private bool _finishingTreeRefreshInProgress;
    private bool _voiceLevelSubscribed;
    private bool _finishingWorkflowHandlersAttached;
    private bool _finishingClassificationConfirmed;
    private string? _returnSourceFolder;
    private string? _lastHighlightedDestinationText;
    private PendingMove? _finishingConfirmedMove;
    private Panel? _movePreviewOriginalParent;
    private int _movePreviewOriginalIndex = -1;

    private void OnFinishingWindowLayoutUpdated(object? sender, EventArgs e)
    {
        if (_undoTimer.IsEnabled)
            _undoTimer.Stop();

        if (UndoMoveButton.Visibility != Visibility.Collapsed)
            UndoMoveButton.Visibility = Visibility.Collapsed;

        EnsureFinishingWorkflowHandlers();
        SynchronizePostMoveWorkflow();
        HighlightDestinationLeaf();
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

    private void EnsureFinishingWorkflowHandlers()
    {
        if (_finishingWorkflowHandlersAttached)
            return;

        // Les handlers historiques déplaçaient ou restauraient le fichier trop tôt.
        // La 1.1.6 impose d'abord une confirmation, puis seulement les actions annexes.
        ConfirmClassificationButton.Click -= OnClassificationConfirmed;
        ConfirmClassificationButton.Click += OnFinishingClassificationConfirmedClicked;
        CorrectClassificationButton.Click -= OnClassificationRejected;
        CorrectClassificationButton.Click += OnFinishingReturnOneDriveClicked;
        _finishingWorkflowHandlersAttached = true;
    }

    private void SynchronizePostMoveWorkflow()
    {
        var postMoveVisible = PostMovePanel.Visibility == Visibility.Visible;

        if (!postMoveVisible && _pendingMove is null)
        {
            _finishingClassificationConfirmed = false;
            _finishingConfirmedMove = null;
            RestoreMovePreviewToFooter();
            return;
        }

        if (!postMoveVisible)
            return;

        PostMovePanel.VerticalAlignment = VerticalAlignment.Top;
        MovePreviewIntoPostMovePanel();

        if (ConfirmClassificationButton.Parent is UniformGrid confirmationGrid)
            confirmationGrid.Columns = 1;

        ConfirmClassificationButton.Visibility = _finishingClassificationConfirmed
            ? Visibility.Collapsed
            : Visibility.Visible;
        ConfirmClassificationButton.IsEnabled = !_finishingClassificationConfirmed;

        CorrectClassificationButton.Visibility = _finishingClassificationConfirmed
            ? Visibility.Visible
            : Visibility.Collapsed;
        CorrectClassificationButton.IsEnabled = _finishingClassificationConfirmed;

        ExplainChoiceButton.Visibility = _finishingClassificationConfirmed
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExplainChoiceButton.IsEnabled = _finishingClassificationConfirmed;

        // Une fois le classement confirmé, RETOUR ne doit surtout plus annuler le déplacement.
        if (_finishingClassificationConfirmed)
            BackButton.Visibility = Visibility.Collapsed;
    }

    private void MovePreviewIntoPostMovePanel()
    {
        if (PostMovePanel.Child is not StackPanel postMoveStack)
            return;

        if (_movePreviewOriginalParent is null && MovePreviewPanel.Parent is Panel currentParent &&
            !ReferenceEquals(currentParent, postMoveStack))
        {
            _movePreviewOriginalParent = currentParent;
            _movePreviewOriginalIndex = currentParent.Children.IndexOf(MovePreviewPanel);
        }

        if (ReferenceEquals(MovePreviewPanel.Parent, postMoveStack))
            return;

        if (MovePreviewPanel.Parent is Panel parent)
            parent.Children.Remove(MovePreviewPanel);

        postMoveStack.Children.Insert(Math.Min(2, postMoveStack.Children.Count), MovePreviewPanel);
        MovePreviewPanel.Margin = new Thickness(4, 10, 4, 2);
    }

    private void RestoreMovePreviewToFooter()
    {
        if (_movePreviewOriginalParent is null || ReferenceEquals(MovePreviewPanel.Parent, _movePreviewOriginalParent))
            return;

        if (MovePreviewPanel.Parent is Panel parent)
            parent.Children.Remove(MovePreviewPanel);

        var index = Math.Clamp(_movePreviewOriginalIndex, 0, _movePreviewOriginalParent.Children.Count);
        _movePreviewOriginalParent.Children.Insert(index, MovePreviewPanel);
        MovePreviewPanel.Margin = new Thickness(3, 0, 3, 10);
    }

    private void OnFinishingClassificationConfirmedClicked(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is null || _finishingClassificationConfirmed)
            return;

        _closeTimer.Stop();
        _undoTimer.Stop();

        var move = _pendingMove;
        ApplyConfirmedLearning(move.Destination);
        SaveLastMove(move, "CONFIRMED");
        _finishingConfirmedMove = move;
        _finishingClassificationConfirmed = true;

        // Le classement est terminé : une nouvelle activation peut arriver immédiatement.
        // On garde néanmoins le mouvement courant en mémoire tant que l'utilisateur souhaite
        // expliquer son choix ou revenir voir le dossier dans OneDrive.
        _busy = false;

        StatusText.Text = "Classement confirmé. Tu peux revenir à OneDrive ou expliquer ton choix.";
        SynchronizePostMoveWorkflow();
    }

    private async void OnFinishingReturnOneDriveClicked(object sender, RoutedEventArgs e)
    {
        if (!_finishingClassificationConfirmed)
            return;

        var move = _finishingConfirmedMove ?? _pendingMove;
        if (move is null || string.IsNullOrWhiteSpace(move.Destination) || !Directory.Exists(move.Destination))
        {
            StatusText.Text = "Le dossier OneDrive de destination est introuvable.";
            return;
        }

        StatusText.Text = "Ouverture du dossier OneDrive…";
        await BringExplorerImmediatelyBehindAtlasAsync(move.Destination);
        StatusText.Text = "OneDrive est au premier plan derrière Atlas Drop.";
    }

    private async Task BringExplorerImmediatelyBehindAtlasAsync(string destination)
    {
        var explorer = await OpenExplorerAndTrackAsync(destination);
        if (explorer is not nint hwnd)
            return;

        ShowWindow(hwnd, ShowWindowMaximized);
        FinishingSetForegroundWindow(hwnd);
        await Task.Delay(120);

        Show();
        Topmost = true;
        Activate();
        PositionTopRight();
    }

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FinishingSetForegroundWindow(nint hWnd);

    private void HighlightDestinationLeaf()
    {
        var displayPath = ProposedPathText.Text ?? string.Empty;
        if (string.Equals(displayPath, _lastHighlightedDestinationText, StringComparison.Ordinal))
            return;

        _lastHighlightedDestinationText = displayPath;
        ProposedPathText.Inlines.Clear();

        if (string.IsNullOrWhiteSpace(displayPath))
            return;

        var separator = " > ";
        var separatorIndex = displayPath.LastIndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            ProposedPathText.Inlines.Add(new Run(displayPath));
            return;
        }

        var prefix = displayPath[..(separatorIndex + separator.Length)];
        var leaf = displayPath[(separatorIndex + separator.Length)..];
        ProposedPathText.Inlines.Add(new Run(prefix));
        ProposedPathText.Inlines.Add(new Run(leaf)
        {
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)),
            Background = new SolidColorBrush(Color.FromRgb(220, 252, 231))
        });
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

    private void OnRedoVoiceRuleClicked(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is null)
            return;

        _pendingVoiceExplanation = null;
        VoiceTranscriptText.Text = string.Empty;
        VoiceRulePreviewText.Text = string.Empty;
        VoiceStatusText.Text = "Nouvelle explication : parle quand l'écoute démarre.";
        VoiceRulePanel.Visibility = Visibility.Visible;
        OnExplainChoiceClicked(sender, e);
    }

    // Conservé pour compatibilité avec le XAML 1.1.5. La vraie confirmation
    // est désormais traitée par OnFinishingClassificationConfirmedClicked.
    private void OnConfirmClassificationPreview(object sender, MouseButtonEventArgs e)
    {
        _closeTimer.Stop();
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
