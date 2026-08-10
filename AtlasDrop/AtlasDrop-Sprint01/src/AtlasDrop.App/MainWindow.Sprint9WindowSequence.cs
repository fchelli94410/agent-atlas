using System.Runtime.InteropServices;
using System.Windows;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private bool _sprint9YesHandlerAttached;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (SuggestionPanel.Parent is System.Windows.Controls.Grid contentGrid && contentGrid.RowDefinitions.Count >= 2)
            contentGrid.RowDefinitions[0].MinHeight = 0;

        SuggestionPanel.MinHeight = 0;
        MainContentScrollViewer.MinHeight = 220;

        if (_sprint9YesHandlerAttached) return;

        YesButton.Click -= OnYes;
        YesButton.Click += OnYesWithVisibleExplorer;
        _sprint9YesHandlerAttached = true;
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
