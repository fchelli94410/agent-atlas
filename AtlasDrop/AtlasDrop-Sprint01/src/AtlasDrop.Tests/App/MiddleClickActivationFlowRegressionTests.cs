using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MiddleClickActivationFlowRegressionTests
{
    [Fact]
    public void Window_is_not_shown_before_item_resolution_finishes()
    {
        var code = File.ReadAllText(FindAppFile("App.xaml.cs"));
        var detectedHandler = Slice(
            code,
            "_middleClickActivation.ExplorerClickDetected +=",
            "_middleClickActivation.ItemActivated +=");

        Assert.DoesNotContain("mainWindow.Show()", detectedHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("mainWindow.Activate()", detectedHandler, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolution_failure_is_dispatched_to_a_terminal_ui_state()
    {
        var appCode = File.ReadAllText(FindAppFile("App.xaml.cs"));
        var failureHandler = Slice(
            appCode,
            "_middleClickActivation.ItemResolutionFailed +=",
            "try\n        {\n            _middleClickActivation.Start()");

        Assert.Contains("Dispatcher.BeginInvoke", failureHandler, StringComparison.Ordinal);
        Assert.Contains("SignalMiddleClickResolutionFailed", failureHandler, StringComparison.Ordinal);

        var windowCode = File.ReadAllText(FindAppFile("MainWindow.xaml.cs"));
        var failureState = Slice(
            windowCode,
            "public void SignalMiddleClickResolutionFailed()",
            "public async void ActivateFile");

        Assert.Contains("ResetOperation()", failureState, StringComparison.Ordinal);
        Assert.Contains("Fichier non détecté", failureState, StringComparison.Ordinal);
        Assert.Contains("Aucun fichier n’a été déplacé", failureState, StringComparison.Ordinal);
    }

    private static string Slice(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Début introuvable : {start}");

        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Fin introuvable : {end}");

        return value[startIndex..endIndex];
    }

    private static string FindAppFile(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                fileName);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(fileName);
    }
}
