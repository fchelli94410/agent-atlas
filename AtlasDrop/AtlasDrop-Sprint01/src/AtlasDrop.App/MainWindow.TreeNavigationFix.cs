using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private async void OnFolderTreePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_moveInProgress)
            return;

        if (FindAncestor<ToggleButton>(e.OriginalSource as DependencyObject) is not null)
            return;

        var treeItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)
            ?? FindAncestor<TreeViewItem>(e.Source as DependencyObject);
        if (treeItem?.Tag is not string destination || string.IsNullOrWhiteSpace(destination))
            return;

        e.Handled = true;
        treeItem.IsSelected = true;
        treeItem.Focus();
        treeItem.BringIntoView();

        if (!Directory.Exists(destination) || !IsUnderRoot(destination))
        {
            StatusText.Text = "Ce dossier OneDrive n’est plus disponible.";
            return;
        }

        var depth = GetDepth(_oneDriveRoot, destination);
        if (!PathsEqualSafe(destination, _oneDriveRoot) && (depth < 1 || depth > MaxDepth))
        {
            StatusText.Text = "Ce dossier n’est pas une destination autorisée.";
            return;
        }

        _decisionPath = PathsEqualSafe(destination, _oneDriveRoot)
            ? DecisionPath.WrongFolder
            : PathsEqualSafe(GetBranchPath(destination), GetBranchPath(_proposedFolder))
                ? DecisionPath.GoodBranch
                : DecisionPath.WrongFolder;
        _initialSuggestedFolder = _proposedFolder;

        await EnterExplorerRefinementModeAsync(destination);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
