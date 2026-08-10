using System.Windows;
using System.Windows.Controls;

namespace AtlasDrop.App;

internal static class MainWindowOverlapStartup
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoaded));
    }

    private static void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        if (window.RenamePanel.Parent is Grid contentGrid && contentGrid.RowDefinitions.Count >= 2)
        {
            contentGrid.RowDefinitions[0].MinHeight = 0;
            contentGrid.ClipToBounds = true;
        }

        window.SuggestionPanel.MinHeight = 0;
        window.MainContentScrollViewer.MinHeight = 0;
    }
}
