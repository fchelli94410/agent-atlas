using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace AtlasDrop.App;

public partial class MainWindow
{
    private const string HiddenIdleInstruction = "Valide explicitement avant tout déplacement.";

    private static readonly DependencyPropertyDescriptor? StatusTextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private bool _statusInstructionFilterAttached;

    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowLoadedForStatusInstructionFilter));
    }

    private static void OnMainWindowLoadedForStatusInstructionFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window || window._statusInstructionFilterAttached)
            return;

        window._statusInstructionFilterAttached = true;
        StatusTextDescriptor?.AddValueChanged(window.StatusText, window.OnStatusInstructionTextChanged);
        window.ApplyStatusInstructionFilter();
    }

    private void OnStatusInstructionTextChanged(object? sender, EventArgs e) =>
        ApplyStatusInstructionFilter();

    private void ApplyStatusInstructionFilter()
    {
        var hideIdleInstruction = string.Equals(
            StatusText.Text,
            HiddenIdleInstruction,
            StringComparison.Ordinal);

        if (hideIdleInstruction)
        {
            StatusText.Visibility = Visibility.Collapsed;
            StatusText.Margin = new Thickness(0);
            return;
        }

        if (StatusText.Visibility == Visibility.Collapsed)
            StatusText.Visibility = Visibility.Visible;

        StatusText.Margin = new Thickness(0, 4, 0, 0);
    }
}
