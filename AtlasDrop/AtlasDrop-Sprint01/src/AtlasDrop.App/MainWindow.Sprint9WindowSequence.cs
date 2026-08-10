using System.Diagnostics;
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
        _explorerPathTimer.Interval = TimeSpan.FromMilliseconds(120);

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

            var expectedFolder = _decisionPath == DecisionPath.WrongFolder
                ? _oneDriveRoot
                : _proposedFolder;

            for (var attempt = 0; attempt < 80 && _trackedExplorerHwnd is null; attempt++)
            {
                if (!string.IsNullOrWhiteSpace(expectedFolder))
                {
                    var match = ReadExplorerWindows()
                        .FirstOrDefault(item => PathsEqualSafe(item.Path, expectedFolder));
                    if (match is not null)
                        _trackedExplorerHwnd = match.Hwnd;
                }

                if (_trackedExplorerHwnd is null)
                    await Task.Delay(25);
            }

            if (_trackedExplorerHwnd is nint hwnd)
            {
                ShowWindow(hwnd, ShowWindowMaximized);
                SetForegroundWindowForSprint9(hwnd);
                DwmFlush();
                await Task.Delay(40);
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
        var explorer = await OpenExplorerFastAsync(destination);
        if (explorer is not nint hwnd) return;

        Dispatcher.Invoke(() => Topmost = false);
        ShowWindow(hwnd, ShowWindowMaximized);
        SetForegroundWindowForSprint9(hwnd);
        DwmFlush();
        await Task.Delay(40);

        Dispatcher.Invoke(() =>
        {
            Show();
            Topmost = true;
            Activate();
        });
    }

    private async Task<nint?> OpenExplorerFastAsync(string destination)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{destination}\"") { UseShellExecute = true });
        }
        catch
        {
            return null;
        }

        for (var attempt = 0; attempt < 80; attempt++)
        {
            var match = ReadExplorerWindows()
                .FirstOrDefault(item => PathsEqualSafe(item.Path, destination));
            if (match is not null)
                return match.Hwnd;

            await Task.Delay(25);
        }

        return null;
    }

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindowForSprint9(nint hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
