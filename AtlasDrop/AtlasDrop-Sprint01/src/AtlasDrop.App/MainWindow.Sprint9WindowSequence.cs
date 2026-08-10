using System.Runtime.InteropServices;
using System.Windows;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private bool _sprint9YesHandlerAttached;
    private bool _explorerRefinementOrderHandlerAttached;
    private bool _explorerRefinementRevealInProgress;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (SuggestionPanel.Parent is System.Windows.Controls.Grid contentGrid && contentGrid.RowDefinitions.Count >= 2)
            contentGrid.RowDefinitions[0].MinHeight = 0;

        SuggestionPanel.MinHeight = 0;
        MainContentScrollViewer.MinHeight = 0;

        if (!_explorerRefinementOrderHandlerAttached)
        {
            ExplorerRefinementPanel.IsVisibleChanged += OnExplorerRefinementPanelIsVisibleChanged;
            _explorerRefinementOrderHandlerAttached = true;
        }

        if (_sprint9YesHandlerAttached) return;

        YesButton.Click -= OnYes;
        YesButton.Click += OnYesWithVisibleExplorer;
        _sprint9YesHandlerAttached = true;
    }

    private async void OnExplorerRefinementPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ExplorerRefinementPanel.Visibility != Visibility.Visible || _explorerRefinementRevealInProgress)
            return;

        _explorerRefinementRevealInProgress = true;
        try
        {
            Topmost = false;
            Hide();

            for (var attempt = 0; attempt < 40 && _trackedExplorerHwnd is null; attempt++)
                await Task.Delay(50);

            if (_trackedExplorerHwnd is nint hwnd)
            {
                ShowWindow(hwnd, ShowWindowMaximized);
                SetForegroundWindowForSprint9(hwnd);
                await Task.Delay(180);
            }

            Show();
            Topmost = true;
            Activate();
            PositionTopRight();
        }
        finally
        {
            _explorerRefinementRevealInProgress = false;
        }
    }

    private async void OnYesWithVisibleExplorer(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;

        _decisionPath = DecisionPath.Exact;
        _initialSuggestedFolder = _proposedFolder;
        await ExecuteMoveOnceAsync(_proposedFolder);
        await ShowExplorerThenReturnAtlasAsync(_proposedFolder);
    }

    private async Task ShowExplorerThenReturnAtlasAsync(string destination)
    {
        var explorer = await OpenExplorerAndTrackAsync(destination);
        if (explorer is not nint hwnd) return;

        Dispatcher.Invoke(() => Topmost = false);
        ShowWindow(hwnd, ShowWindowMaximized);
        SetForegroundWindowForSprint9(hwnd);

        await Task.Delay(650);

        Dispatcher.Invoke(() =>
        {
            Show();
            Topmost = true;
            Activate();
        });
    }

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindowForSprint9(nint hWnd);
}
