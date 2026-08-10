using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116SequentialActivationTests
{
    private static string FindFile(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", name);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(name + " introuvable.");
    }

    [Fact]
    public void Confirmed_classification_releases_busy_state_for_next_activation()
    {
        var finishing = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("_finishingClassificationConfirmed = true", finishing, StringComparison.Ordinal);
        Assert.Contains("_busy = false", finishing, StringComparison.Ordinal);
        Assert.Contains("_closeTimer.Stop()", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipe_activation_reuses_visible_topmost_main_window_without_toggle_off()
    {
        var app = File.ReadAllText(FindFile("App.xaml.cs"));
        var start = app.IndexOf("_activationChannel.FileReceived", StringComparison.Ordinal);
        var end = app.IndexOf("_listenerCts = new CancellationTokenSource", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var block = app[start..end];

        Assert.Contains("mainWindow.ActivateFile(filePath)", block, StringComparison.Ordinal);
        Assert.Contains("mainWindow.Show()", block, StringComparison.Ordinal);
        Assert.Contains("mainWindow.Topmost = true", block, StringComparison.Ordinal);
        Assert.DoesNotContain("mainWindow.Topmost = false", block, StringComparison.Ordinal);
    }
}
