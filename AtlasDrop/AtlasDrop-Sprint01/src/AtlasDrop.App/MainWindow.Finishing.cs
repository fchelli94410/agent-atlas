using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private bool _finishingTreeRefreshInProgress;

    private void OnFinishingWindowLayoutUpdated(object? sender, EventArgs e)
    {
        if (_undoTimer.IsEnabled)
            _undoTimer.Stop();

        if (UndoMoveButton.Visibility != Visibility.Collapsed)
            UndoMoveButton.Visibility = Visibility.Collapsed;
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
