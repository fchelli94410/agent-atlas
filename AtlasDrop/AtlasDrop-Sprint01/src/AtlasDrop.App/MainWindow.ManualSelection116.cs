using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private static readonly bool ManualTreeSelectionHandlerRegistered = RegisterManualTreeSelectionHandler();

    private static bool RegisterManualTreeSelectionHandler()
    {
        EventManager.RegisterClassHandler(
            typeof(TreeView),
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnManualTreeSelectionPreview));
        return true;
    }

    private static void OnManualTreeSelectionPreview(object sender, MouseButtonEventArgs e)
    {
        _ = ManualTreeSelectionHandlerRegistered;
        if (sender is not TreeView tree || Window.GetWindow(tree) is not MainWindow window)
            return;

        window.HandleManualTreeSelection(e);
    }

    private void HandleManualTreeSelection(MouseButtonEventArgs e)
    {
        if (_pendingMove is not null || _moveInProgress || _activePath is null)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        // Un clic sur la flèche doit uniquement ouvrir/fermer la branche.
        if (FindVisualAncestor<ToggleButton>(source) is not null)
            return;

        string? destination = null;
        for (var current = source; current is not null; current = GetUiParent(current))
        {
            if (current is TextBlock { Tag: string textPath })
            {
                destination = textPath;
                break;
            }

            if (current is TreeViewItem { Tag: string itemPath })
            {
                destination = itemPath;
                break;
            }

            if (ReferenceEquals(current, FolderTree))
                break;
        }

        if (string.IsNullOrWhiteSpace(destination))
            return;

        var depth = GetDepth(_oneDriveRoot, destination);
        if (!Directory.Exists(destination) || !IsUnderRoot(destination) || depth is < 1 or > MaxDepth)
        {
            StatusText.Text = "Choisis un dossier OneDrive entre les niveaux 1 et 4.";
            return;
        }

        e.Handled = true;
        _proposedFolder = destination;
        _decisionPath = PathsEqualSafe(GetBranchPath(destination), GetBranchPath(_initialSuggestedFolder ?? destination))
            ? DecisionPath.GoodBranch
            : DecisionPath.WrongFolder;

        ProposedPathText.Text = ToOneDriveDisplayPath(destination);
        ConfidenceLevelText.Text = "MANUEL";
        ConfidenceBadge.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254));
        ConfidenceText.Text = "Dossier choisi manuellement — valide avec C’EST EXACT avant tout déplacement.";
        CurrentMovePreviewText.Text = $"{Path.GetFileName(_activePath)}  →  {ToOneDriveDisplayPath(destination)}";
        YesButton.IsEnabled = true;

        if (_analysis is not null && File.Exists(_activePath))
            PrepareRename(_activePath, _analysis, null);

        RebuildFullNavigableTree();
        StatusText.Text = "Destination manuelle prête. Clique C’EST EXACT pour confirmer.";
    }

    private static T? FindVisualAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var current = start; current is not null; current = GetUiParent(current))
        {
            if (current is T match)
                return match;
        }

        return null;
    }

    private static DependencyObject? GetUiParent(DependencyObject current)
    {
        return current switch
        {
            ContentElement content => ContentOperations.GetParent(content) ??
                                      (content is FrameworkContentElement frameworkContent
                                          ? frameworkContent.Parent
                                          : null),
            Visual or Visual3D => VisualTreeHelper.GetParent(current),
            _ => LogicalTreeHelper.GetParent(current)
        };
    }
}
