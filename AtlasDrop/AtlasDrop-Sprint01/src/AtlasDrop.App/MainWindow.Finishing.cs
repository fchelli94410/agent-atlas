using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        ConfirmClassificationButton.Click -= OnClassificationConfirmed;
        ConfirmClassificationButton.Click += OnFinishingClassificationConfirmedClicked;
        CorrectClassificationButton.Click -= OnClassificationRejected;
        CorrectClassificationButton.Click += OnFinishingReturnOneDriveClicked;
        ManageLearningButton.Click -= OnManageLearningClicked;
        ManageLearningButton.Click += OnFinishingManageLearningClicked;
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

    private void OnFinishingManageLearningClicked(object sender, RoutedEventArgs e)
    {
        var window = new Window
        {
            Title = "Gérer l’apprentissage Atlas Drop",
            Owner = this,
            Width = 660,
            Height = 540,
            MinWidth = 560,
            MinHeight = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            ShowInTaskbar = false
        };

        var root = new DockPanel { Margin = new Thickness(18) };
        var title = new TextBlock
        {
            Text = "Gérer l’apprentissage",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var help = new TextBlock
        {
            Text = "Coche les apprentissages à supprimer. Les documents OneDrive ne sont jamais modifiés depuis cette fenêtre.",
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        };
        DockPanel.SetDock(help, Dock.Top);
        root.Children.Add(help);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        DockPanel.SetDock(actions, Dock.Bottom);

        var clearAll = new Button
        {
            Content = "TOUT EFFACER",
            Background = Brushes.Firebrick,
            Foreground = Brushes.White,
            MinHeight = 42,
            Margin = new Thickness(0, 0, 7, 0)
        };
        var deleteSelected = new Button
        {
            Content = "SUPPRIMER LA SÉLECTION",
            Background = Brushes.DarkOrange,
            Foreground = Brushes.White,
            MinHeight = 42,
            Margin = new Thickness(7, 0, 7, 0)
        };
        var close = new Button
        {
            Content = "FERMER",
            MinHeight = 42,
            Margin = new Thickness(7, 0, 0, 0)
        };

        Grid.SetColumn(clearAll, 0);
        Grid.SetColumn(deleteSelected, 1);
        Grid.SetColumn(close, 2);
        actions.Children.Add(clearAll);
        actions.Children.Add(deleteSelected);
        actions.Children.Add(close);
        root.Children.Add(actions);

        var list = new StackPanel();
        PopulateFinishingLearningList(list);
        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        root.Children.Add(scroll);
        window.Content = root;

        clearAll.Click += async (_, _) =>
        {
            var confirmation = MessageBox.Show(
                window,
                "Effacer tout l’apprentissage enregistré ?\n\nAucun fichier OneDrive ne sera supprimé ou déplacé.",
                "Tout effacer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
                return;

            await _learningService.ClearAsync();
            _learning.Clear();
            try { File.Delete(Path.Combine(_stateDirectory, "learning-v108.json")); } catch { }
            LearningStatusText.Text = "Tout l’apprentissage a été effacé.";
            list.Children.Clear();
            PopulateFinishingLearningList(list);
        };

        deleteSelected.Click += (_, _) =>
        {
            var selectedKeys = list.Children
                .OfType<CheckBox>()
                .Where(checkBox => checkBox.IsChecked == true)
                .SelectMany(checkBox => (string[])(checkBox.Tag ?? Array.Empty<string>()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (selectedKeys.Length == 0)
            {
                MessageBox.Show(window, "Coche au moins un apprentissage.", "Atlas Drop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var key in selectedKeys)
                _learning.Remove(key);

            SaveLearningDictionary();
            LearningStatusText.Text = $"{selectedKeys.Length} apprentissage(s) supprimé(s).";
            list.Children.Clear();
            PopulateFinishingLearningList(list);
        };

        close.Click += (_, _) => window.Close();
        window.ShowDialog();
    }

    private void PopulateFinishingLearningList(StackPanel list)
    {
        var groups = _learning
            .GroupBy(entry => LearningFolderFromKey(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (groups.Length == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "Aucun apprentissage enregistré.",
                Foreground = Brushes.DimGray,
                Margin = new Thickness(4, 8, 4, 8)
            });
            return;
        }

        foreach (var group in groups)
        {
            var tokens = group
                .Select(entry => LearningTokenFromKey(entry.Key))
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                .Take(12);

            list.Children.Add(new CheckBox
            {
                Content = $"{ToOneDriveDisplayPath(group.Key)}\nMots : {string.Join(", ", tokens)}",
                Tag = group.Select(entry => entry.Key).ToArray(),
                Margin = new Thickness(3, 6, 3, 6),
                Padding = new Thickness(7),
                FontWeight = FontWeights.SemiBold
            });
        }
    }

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
